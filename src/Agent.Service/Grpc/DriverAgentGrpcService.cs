using System.Globalization;
using System.Threading.Channels;
using Agent.Ipc.Grpc;
using Agent.Service.Operations;
using Agent.Service.Scheduling;
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
    private readonly ISettingsStore _settings;
    private readonly IMotherboardInfoProvider _motherboard;
    private readonly ScheduledCheckState _scheduledCheck;
    private readonly ILogger<DriverAgentGrpcService> _logger;

    public DriverAgentGrpcService(
        IDriverInventory inventory,
        IUpdateSource updateSource,
        IDriverInstaller installer,
        IOperationHistory history,
        IOperationQueue operationQueue,
        ISettingsStore settings,
        IMotherboardInfoProvider motherboard,
        ScheduledCheckState scheduledCheck,
        ILogger<DriverAgentGrpcService> logger)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _updateSource = updateSource ?? throw new ArgumentNullException(nameof(updateSource));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _operationQueue = operationQueue ?? throw new ArgumentNullException(nameof(operationQueue));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _motherboard = motherboard ?? throw new ArgumentNullException(nameof(motherboard));
        _scheduledCheck = scheduledCheck ?? throw new ArgumentNullException(nameof(scheduledCheck));
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
        // instalaciones simultáneas, por lo que la búsqueda se serializa en la cola.
        // Recopilamos las actualizaciones dentro del lock y las enviamos al stream fuera
        // de él, evitando que un cliente lento retenga la cola y bloquee instalaciones.
        var updates = new List<DriverUpdate>();
        await _operationQueue.EnqueueAsync(async ct =>
        {
            var excluded = (await _settings.GetAsync(ct).ConfigureAwait(false)).ExcludedHardwareIds;
            var installedDrivers = await _inventory.GetInstalledDriversAsync(ct).ConfigureAwait(false);
            _operationQueue.MarkScanCompleted();

            await foreach (var update in _updateSource.FindUpdatesAsync(installedDrivers, ct).ConfigureAwait(false))
            {
                // Las actualizaciones de dispositivos excluidos por el usuario no se muestran.
                if (!IsExcluded(update, excluded))
                {
                    updates.Add(update);
                }
            }
        }, context.CancellationToken).ConfigureAwait(false);

        foreach (var update in updates)
        {
            await responseStream.WriteAsync(GrpcMappers.ToGrpc(update), context.CancellationToken).ConfigureAwait(false);
        }
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
            || !ulong.TryParse(operationId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId)
            || parsedId == 0)
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
        var (lastScheduledCheck, pendingUpdates) = _scheduledCheck.Snapshot;

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
            QueuedOperations = state.QueuedOperations,
            LastScheduledCheck = lastScheduledCheck.HasValue
                ? Timestamp.FromDateTime(lastScheduledCheck.Value.UtcDateTime)
                : null,
            PendingUpdates = pendingUpdates
        });
    }

    /// <inheritdoc/>
    public override async Task<SettingsMsg> GetSettings(SettingsRequest request, ServerCallContext context)
    {
        var settings = await _settings.GetAsync(context.CancellationToken).ConfigureAwait(false);
        return ToMessage(settings);
    }

    /// <inheritdoc/>
    public override async Task<SettingsMsg> UpdateSettings(SettingsMsg request, ServerCallContext context)
    {
        var requested = new AppSettings
        {
            ExcludedHardwareIds = request.ExcludedHardwareIds.ToList(),
            CheckIntervalHours = request.CheckIntervalHours,
            NotificationsEnabled = request.NotificationsEnabled
        }.Normalize();

        await _settings.SaveAsync(requested, context.CancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Ajustes actualizados: {Excluded} exclusión(es), comprobación cada {Hours} h.",
                requested.ExcludedHardwareIds.Count,
                requested.CheckIntervalHours);
        }

        return ToMessage(requested);
    }

    /// <inheritdoc/>
    public override async Task<MotherboardMsg> GetMotherboard(MotherboardRequest request, ServerCallContext context)
    {
        var board = await _motherboard.GetAsync(context.CancellationToken).ConfigureAwait(false);

        return board is null
            ? new MotherboardMsg { Detected = false }
            : new MotherboardMsg { Detected = true, Manufacturer = board.Manufacturer, Product = board.Product };
    }

    private static bool IsExcluded(DriverUpdate update, IReadOnlyList<string> excluded) =>
        excluded.Contains(update.MatchedHardwareId.Value, StringComparer.OrdinalIgnoreCase);

    private static SettingsMsg ToMessage(AppSettings settings)
    {
        var message = new SettingsMsg
        {
            CheckIntervalHours = settings.CheckIntervalHours,
            NotificationsEnabled = settings.NotificationsEnabled
        };
        message.ExcludedHardwareIds.AddRange(settings.ExcludedHardwareIds);
        return message;
    }

    private async Task InstallCoreAsync(
        List<string> updateIds,
        IServerStreamWriter<InstallProgressMsg> responseStream,
        CancellationToken ct)
    {
        // Re-resolver las actualizaciones contra Windows Update evita instalar
        // datos anticuados o manipulados que envíe el cliente: solo se aceptan
        // actualizaciones presentes en la fuente en este instante.
        var excluded = (await _settings.GetAsync(ct).ConfigureAwait(false)).ExcludedHardwareIds;
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

            if (match is null || IsExcluded(match, excluded))
            {
                completedUpdates++;
                var message = match is null
                    ? $"No se encontró la actualización '{updateId}' en Windows Update."
                    : $"El dispositivo '{match.DeviceName}' está excluido de las actualizaciones.";

                var written = await TryWriteFinishedResultAsync(
                    responseStream,
                    updateId,
                    success: false,
                    message,
                    rebootRequired: false,
                    totalUpdates,
                    completedUpdates,
                    ct).ConfigureAwait(false);

                if (!written)
                {
                    break;
                }

                continue;
            }

            InstallResult result;
            bool transportError;
            try
            {
                (result, transportError) = await InstallSingleUpdateAsync(match, responseStream, totalUpdates, completedUpdates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is RpcException or IOException)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "Fallo de transporte con el cliente durante la instalación de {UpdateId}.", match.UpdateId);
                }
                break;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Excepción inesperada al instalar la actualización {UpdateId}.", match.UpdateId);
                }

                var timestamp = DateTime.UtcNow;
                var errorMessage = $"Error inesperado al instalar '{match.UpdateId}': {ex.Message}";

                // Regla 8 de AGENTS.md: Registro obligatorio en el historial si el instalador lanzó excepción
                try
                {
                    await _history.RecordAsync(new OperationLogEntry
                    {
                        Id = 0,
                        OperationType = "Install",
                        UpdateId = match.UpdateId,
                        DeviceName = match.DeviceName,
                        HardwareId = match.MatchedHardwareId.Value,
                        DriverTitle = match.Title,
                        PreviousVersion = match.CurrentVersion,
                        NewVersion = match.NewVersion,
                        Success = false,
                        ErrorMessage = errorMessage,
                        Timestamp = timestamp
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception histEx)
                {
                    _logger.LogError(histEx, "Fallo al registrar la excepción de instalación en el historial para {UpdateId}.", match.UpdateId);
                }

                result = new InstallResult
                {
                    UpdateId = match.UpdateId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    RebootRequired = false,
                    PreviousVersion = match.CurrentVersion,
                    InstalledVersion = string.Empty,
                    Timestamp = timestamp
                };
                transportError = false;
            }

            completedUpdates++;
            if (result.RebootRequired)
            {
                _operationQueue.MarkPendingReboot();
            }

            if (transportError)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("El cliente se desconectó durante la transmisión de progreso de {UpdateId}. Interrumpiendo el lote.", match.UpdateId);
                }
                break;
            }

            if (!await TryWriteFinishedResultAsync(
                responseStream,
                match.UpdateId,
                result.Success,
                result.ErrorMessage,
                result.RebootRequired,
                totalUpdates,
                completedUpdates,
                ct).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<(InstallResult Result, bool TransportError)> InstallSingleUpdateAsync(
        DriverUpdate update,
        IServerStreamWriter<InstallProgressMsg> responseStream,
        int totalUpdates,
        int completedUpdates,
        CancellationToken ct)
    {
        // Enlazamos un CancellationTokenSource que cancela la instalación si el lector de progreso
        // detecta un fallo de transporte con el cliente.
        using var installCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // El progreso se transmite en tiempo real al stream gRPC utilizando un Channel
        // como buffer desacoplado, asegurando que un único lector serializado escribe
        // concurrentemente al responseStream mientras el instalador ejecuta sus fases.
        var channel = Channel.CreateUnbounded<InstallProgress>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        var progress = new InlineProgress(p =>
        {
            channel.Writer.TryWrite(p);
            _operationQueue.UpdateProgress(update.UpdateId, p);
        });

        Exception? transportException = null;

        var readerTask = Task.Run(async () =>
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var buffered))
                    {
                        await responseStream.WriteAsync(
                            GrpcMappers.ToProgress(update.UpdateId, buffered, totalUpdates, completedUpdates), ct)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Cancelación esperada si el cliente cancela la llamada RPC.
            }
            catch (Exception ex)
            {
                // Fallo de transporte (desconexión del cliente, pipe roto o error de gRPC).
                // Lo capturamos para que no escape hacia el catch general del servicio ni manche el historial.
                transportException = ex;
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Fallo de transporte al transmitir progreso de {UpdateId} al cliente: {Message}", update.UpdateId, ex.Message);
                }
                // Si el transporte falla antes de la fase nativa, abortamos el instalador para evitar trabajo innecesario
                installCts.Cancel();
            }
        }, CancellationToken.None);

        Exception? installException = null;
        InstallResult? result = null;

        try
        {
            result = await _installer.InstallAsync(update, progress, installCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Instalación de {Title} cancelada por el cliente.", update.Title);
            }

            throw;
        }
        catch (OperationCanceledException) when (transportException != null)
        {
            // La cancelación fue inducida por el fallo de transporte (installCts fue cancelado por el lector, no por ct).
            // El instalador ya registró el fallo en el historial por cancelación.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Instalación de {Title} interrumpida en fase previa por fallo de transporte con el cliente.", update.Title);
            }

            result = new InstallResult
            {
                UpdateId = update.UpdateId,
                Success = false,
                ErrorMessage = $"Instalación interrumpida por desconexión del cliente ({transportException.Message}).",
                RebootRequired = false,
                PreviousVersion = update.CurrentVersion,
                InstalledVersion = string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            installException = ex;
            throw;
        }
        finally
        {
            channel.Writer.TryComplete();
            try
            {
                await readerTask.ConfigureAwait(false);
            }
            catch
            {
                // readerTask captura internamente sus excepciones en transportException.
            }
        }

        return (result!, transportException != null);
    }

    private async Task<bool> TryWriteFinishedResultAsync(
        IServerStreamWriter<InstallProgressMsg> responseStream,
        string updateId,
        bool success,
        string? errorMessage,
        bool rebootRequired,
        int totalUpdates,
        int completedUpdates,
        CancellationToken ct = default)
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

        try
        {
            await responseStream.WriteAsync(message, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is RpcException or IOException or OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("El cliente se desconectó antes de recibir el resultado de {UpdateId}.", updateId);
            }
            return false;
        }
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
