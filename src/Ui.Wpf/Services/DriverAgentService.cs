using Agent.Ipc;
using Agent.Ipc.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Ui.Wpf.Services;

/// <summary>
/// Wrapper sobre el cliente gRPC generado de DriverAgent.
/// Gestiona el ciclo de vida del canal y re-conecta cuando el servicio
/// no está disponible.
/// </summary>
public sealed class DriverAgentService : IAsyncDisposable, IDisposable
{
    private readonly ILogger<DriverAgentService> _logger;
    private GrpcChannel? _channel;
    private DriverAgent.DriverAgentClient? _client;

    public bool IsConnected { get; private set; }

    public DriverAgentService(ILogger<DriverAgentService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Intenta conectar al named pipe del servicio Windows.
    /// Devuelve true si el canal se abrió correctamente.
    /// </summary>
    public async Task<bool> TryConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _channel?.Dispose();
            _channel = DriverAgentChannel.CreateChannel();
            _client = new DriverAgent.DriverAgentClient(_channel);

            // Verificación rápida de conectividad
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await _client.GetStatusAsync(new StatusRequest(), cancellationToken: cts.Token);

            IsConnected = true;
            _logger.LogInformation("Conectado al servicio DriverUpdater.");
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            _logger.LogWarning(ex, "No se pudo conectar al servicio DriverUpdater.");
            return false;
        }
    }

    /// <summary>
    /// Inicia el servicio DriverUpdater si está detenido. El servicio está configurado
    /// como arranque bajo demanda; el instalador concede a usuarios interactivos el
    /// permiso de iniciarlo y detenerlo (no de configurarlo). Devuelve true si queda en ejecución.
    /// </summary>
    public async Task<bool> TryStartServiceAsync(CancellationToken ct = default)
    {
        try
        {
            using var controller = new System.ServiceProcess.ServiceController("DriverUpdater");
            if (controller.Status == System.ServiceProcess.ServiceControllerStatus.Running)
            {
                return true;
            }

            controller.Start();
            await Task.Run(() => controller.WaitForStatus(
                System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(15)), ct);
            _logger.LogInformation("Servicio DriverUpdater iniciado desde la UI.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo iniciar el servicio DriverUpdater.");
            return false;
        }
    }

    public void Disconnect()
    {
        _channel?.Dispose();
        _channel = null;
        _client = null;
        IsConnected = false;
    }

    private DriverAgent.DriverAgentClient Client =>
        _client ?? throw new InvalidOperationException("No hay conexión con el servicio. Llama a TryConnectAsync primero.");

    // ── RPCs ─────────────────────────────────────────────────────────────────

    public async Task<ScanResponse> ScanAsync(CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("RPC Scan");
        }
        return await Client.ScanAsync(new ScanRequest(), cancellationToken: ct);
    }

    public async IAsyncEnumerable<DriverUpdateMsg> CheckUpdatesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("RPC CheckUpdates");
        }
        using var call = Client.CheckUpdates(new CheckUpdatesRequest(), cancellationToken: ct);
        await foreach (var msg in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return msg;
        }
    }

    public async IAsyncEnumerable<InstallProgressMsg> InstallAsync(
        IEnumerable<string> updateIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("RPC Install: {Ids}", string.Join(", ", updateIds));
        }
        var request = new InstallRequest();
        request.UpdateIds.AddRange(updateIds);
        using var call = Client.Install(request, cancellationToken: ct);
        await foreach (var msg in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return msg;
        }
    }

    public async Task<RollbackResultMsg> RollbackAsync(string operationId, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("RPC Rollback: {OpId}", operationId);
        }
        return await Client.RollbackAsync(new RollbackRequest { OperationId = operationId }, cancellationToken: ct);
    }

    public async Task<HistoryResponse> GetHistoryAsync(int limit = 100, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("RPC GetHistory limit={Limit}", limit);
        }
        return await Client.GetHistoryAsync(new HistoryRequest { Limit = limit }, cancellationToken: ct);
    }

    public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        return await Client.GetStatusAsync(new StatusRequest(), cancellationToken: ct);
    }

    public async Task<MotherboardMsg> GetMotherboardAsync(CancellationToken ct = default)
    {
        return await Client.GetMotherboardAsync(new MotherboardRequest(), cancellationToken: ct);
    }

    /// <summary>
    /// Cierre síncrono: lo usa el contenedor de DI al salir de la aplicación (OnExit).
    /// No hace falta apagar el canal de forma asíncrona aquí.
    /// </summary>
    public void Dispose()
    {
        _channel?.Dispose();
        _channel = null;
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.ShutdownAsync();
        }

        _channel?.Dispose();
    }
}
