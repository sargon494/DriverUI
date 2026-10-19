using System.Collections.Concurrent;
using Agent.Ipc.Grpc;
using Agent.Service.Operations;
using Core.Interfaces;
using Core.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Service.Grpc;

/// <summary>
/// Implementación del contrato IPC <see cref="DriverAgent"/> expuesto por el
/// servicio de Windows. Traduce las peticiones de la UI a las interfaces de
/// dominio de <c>Core</c>; toda operación privilegiada se ejecuta a través de
/// <see cref="IOperationQueue"/> para garantizar una operación a la vez.
/// </summary>
public sealed class DriverAgentGrpcService : DriverAgent.DriverAgentBase
{
    private const int MaxUpdateIdLength = 256;
    private const int MaxOperationIdLength = 32;

    private readonly IDriverInventory _inventory;
    private readonly IUpdateSource _updateSource;
    private readonly IDriverInstaller _installer;
    private readonly IOperationHistory _history;
    private readonly IOperationQueue _operationQueue;
    private readonly ILogger<DriverAgentGrpcService> _logger;

    public DriverAgentGrpcService(
        IDriverInventory inventory,
        IUpdateSource updateSource,
        IDriverInstaller installer,
        IOperationHistory history,
        IOperationQueue operationQueue,
        ILogger<DriverAgentGrpcService> logger)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _updateSource = updateSource ?? throw new ArgumentNullException(nameof(updateSource));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _operationQueue = operationQueue ?? throw new ArgumentNullException(nameof(operationQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override async Task<ScanResponse> Scan(ScanRequest request, ServerCallContext context)
    {
        var drivers = await _inventory.GetInstalledDriversAsync(context.CancellationToken).ConfigureAwait(false);
        _operationQueue.MarkScanCompleted();

        var response = new ScanResponse();
        response.Drivers.AddRange(drivers.Select(GrpcMappers.ToGrpc));
        return response;
    }

    /// <inheritdoc/>
    public override async Task CheckUpdates(
        CheckUpdatesRequest request,
        IServerStreamWriter<DriverUpdateMsg> responseStream,
        ServerCallContext context)
    {
        // La sesión COM de Windows Update Agent no debe atender búsquedas e
        // instalaciones simultáneas, por lo que la búsqueda también se serializa.
        await _operationQueue.EnqueueAsync(async ct =>
        {
            var installedDrivers = await _inventory.GetInstalledDriversAsync(ct).ConfigureAwait(false);
            _operationQueue.MarkScanCompleted();

            await foreach (var update in _updateSource.FindUpdatesAsync(installedDrivers, ct).ConfigureAwait(false))
            {
                await responseStream.WriteAsync(GrpcMappers.ToGrpc(update), ct).ConfigureAwait(false);
            }
        }, context.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task Install(
        InstallRequest request,
        IServerStreamWriter<InstallProgressMsg> responseStream,
        ServerCallContext context)
    {
        var updateIds = request.UpdateIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (updateIds.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "No se especificó ninguna actualización a instalar."));
        }

        if (updateIds.Any(id => id.Length > MaxUpdateIdLength))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Identificador de actualización no válido."));
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Solicitud de instalación de {Count} actualización(es): {Ids}", updateIds.Count, string.Join(", ", updateIds));
        }

        await _operationQueue.EnqueueAsync(
            ct => InstallCoreAsync(updateIds, responseStream, ct),
            context.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<RollbackResultMsg> Rollback(RollbackRequest request, ServerCallContext context)
    {
        var operationId = (request.OperationId ?? string.Empty).Trim();

        if (operationId.Length == 0
            || operationId.Length > MaxOperationIdLength
            || !long.TryParse(operationId, out _))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Identificador de operación no válido."));
        }

        return await _operationQueue.EnqueueAsync(async ct =>
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Solicitud de rollback de la operación {OperationId}", operationId);
            }

            var result = await _installer.RollbackAsync(operationId, ct).ConfigureAwait(false);
            return GrpcMappers.ToGrpc(result);
        }, context.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<HistoryResponse> GetHistory(HistoryRequest request, ServerCallContext context)
    {
        var entries = await _history.GetAllAsync(context.CancellationToken).ConfigureAwait(false);

        if (request.Limit > 0)
        {
            entries = entries.TakeLast(request.Limit).ToList();
        }

        var response = new HistoryResponse();
        response.Entries.AddRange(entries.Select(GrpcMappers.ToGrpc));
        return response;
    }

    /// <inheritdoc/>
    public override Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
    {
        var state = _operationQueue.State;

        return Task.FromResult(new StatusResponse
        {
            OperationInProgress = state.OperationInProgress,
            CurrentUpdateId = state.CurrentUpdateId ?? string.Empty,
            PercentComplete = state.PercentComplete,
            Phase = state.Phase.HasValue ? GrpcMappers.ToGrpc(state.Phase.Value) : InstallPhaseProto.InstallPhaseUnspecified,
            PendingReboot = state.PendingReboot,
            LastScan = state.LastScanUtc.HasValue
                ? Timestamp.FromDateTime(state.LastScanUtc.Value.UtcDateTime)
                : null,
            QueuedOperations = state.QueuedOperations
        });
    }

    private async Task InstallCoreAsync(
        List<string> updateIds,
        IServerStreamWriter<InstallProgressMsg> responseStream,
        CancellationToken ct)
    {
        // Re-resolver las actualizaciones contra Windows Update evita instalar
        // datos anticuados o manipulados que envíe el cliente: solo se aceptan
        // actualizaciones presentes en la fuente en este instante.
        var installedDrivers = await _inventory.GetInstalledDriversAsync(ct).ConfigureAwait(false);
        _operationQueue.MarkScanCompleted();

        var availableUpdates = new List<DriverUpdate>();
        await foreach (var update in _updateSource.FindUpdatesAsync(installedDrivers, ct).ConfigureAwait(false))
        {
            availableUpdates.Add(update);
        }

        var totalUpdates = updateIds.Count;
        var completedUpdates = 0;

        foreach (var updateId in updateIds)
        {
            ct.ThrowIfCancellationRequested();

            var match = availableUpdates.FirstOrDefault(
                update => string.Equals(update.UpdateId, updateId, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                completedUpdates++;
                await WriteFinishedResultAsync(
                    responseStream,
                    updateId,
                    success: false,
                    $"No se encontró la actualización '{updateId}' en Windows Update.",
                    rebootRequired: false,
                    totalUpdates,
                    completedUpdates).ConfigureAwait(false);
                continue;
            }

            var result = await InstallSingleUpdateAsync(match, responseStream, totalUpdates, completedUpdates, ct)
                .ConfigureAwait(false);

            completedUpdates++;
            if (result.RebootRequired)
            {
                _operationQueue.MarkPendingReboot();
            }

            await WriteFinishedResultAsync(
                responseStream,
                match.UpdateId,
                result.Success,
                result.ErrorMessage,
                result.RebootRequired,
                totalUpdates,
                completedUpdates).ConfigureAwait(false);
        }
    }

    private async Task<InstallResult> InstallSingleUpdateAsync(
        DriverUpdate update,
        IServerStreamWriter<InstallProgressMsg> responseStream,
        int totalUpdates,
        int completedUpdates,
        CancellationToken ct)
    {
        // Los progresos se encolan sin contención y se vuelcan al stream gRPC
        // desde un único escritor (este método) una vez finalizada la llamada
        // al instalador, evitando escrituras concurrentes en el stream.
        var pendingProgress = new ConcurrentQueue<InstallProgress>();
        var progress = new InlineProgress(p =>
        {
            pendingProgress.Enqueue(p);
            _operationQueue.UpdateProgress(update.UpdateId, p);
        });

        InstallResult result;
        try
        {
            result = await _installer.InstallAsync(update, progress, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Instalación de {Title} cancelada por el cliente.", update.Title);
            }

            throw;
        }

        while (pendingProgress.TryDequeue(out var buffered))
        {
            await responseStream.WriteAsync(
                GrpcMappers.ToProgress(update.UpdateId, buffered, totalUpdates, completedUpdates), ct)
                .ConfigureAwait(false);
        }

        return result;
    }

    private async Task WriteFinishedResultAsync(
        IServerStreamWriter<InstallProgressMsg> responseStream,
        string updateId,
        bool success,
        string? errorMessage,
        bool rebootRequired,
        int totalUpdates,
        int completedUpdates)
    {
        var message = new InstallProgressMsg
        {
            UpdateId = updateId,
            PercentComplete = 100,
            Phase = success ? InstallPhaseProto.InstallPhaseComplete : InstallPhaseProto.InstallPhaseFailed,
            StatusMessage = success
                ? (rebootRequired ? "Instalación finalizada con éxito. Se requiere reiniciar el equipo." : "Instalación finalizada con éxito.")
                : errorMessage ?? string.Empty,
            Finished = true,
            Success = success,
            ErrorMessage = errorMessage ?? string.Empty,
            RebootRequired = rebootRequired,
            TotalUpdates = totalUpdates,
            CompletedUpdates = completedUpdates
        };

        if (!success && _logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError("Fallo al instalar la actualización {UpdateId}: {Error}", updateId, errorMessage);
        }

        await responseStream.WriteAsync(message).ConfigureAwait(false);
    }

    /// <summary>
    /// Implementación de <see cref="IProgress{T}"/> que notifica de forma
    /// síncrona en el hilo que reporta (el propio instalador), evitando el
    /// marshaling asíncrono de <see cref="Progress{T}"/>.
    /// </summary>
    private sealed class InlineProgress(Action<InstallProgress> report) : IProgress<InstallProgress>
    {
        private readonly Action<InstallProgress> _report = report ?? throw new ArgumentNullException(nameof(report));

        public void Report(InstallProgress value)
        {
            _report(value);
        }
    }
}
