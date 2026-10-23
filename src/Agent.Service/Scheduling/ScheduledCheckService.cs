using Agent.Service.Operations;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Service.Scheduling;

/// <summary>
/// Ejecuta la búsqueda de actualizaciones cada <see cref="AppSettings.CheckIntervalHours"/> horas.
/// La búsqueda pasa por la misma cola que la UI, así que nunca se solapa con una instalación.
/// Solo se ejecuta mientras el servicio está en marcha (arranque bajo demanda, sin arrancar con Windows).
/// </summary>
public sealed class ScheduledCheckService : BackgroundService
{
    private static readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _tickInterval = TimeSpan.FromMinutes(15);

    private readonly ISettingsStore _settings;
    private readonly IDriverInventory _inventory;
    private readonly IUpdateSource _updateSource;
    private readonly IOperationQueue _operationQueue;
    private readonly ScheduledCheckState _state;
    private readonly ILogger<ScheduledCheckService> _logger;

    public ScheduledCheckService(
        ISettingsStore settings,
        IDriverInventory inventory,
        IUpdateSource updateSource,
        IOperationQueue operationQueue,
        ScheduledCheckState state,
        ILogger<ScheduledCheckService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _updateSource = updateSource ?? throw new ArgumentNullException(nameof(updateSource));
        _operationQueue = operationQueue ?? throw new ArgumentNullException(nameof(operationQueue));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_initialDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(_tickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunIfDueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló la comprobación programada de actualizaciones; se reintentará en el siguiente ciclo.");
            }
        }
    }

    private async Task RunIfDueAsync(CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct).ConfigureAwait(false);
        if (settings.CheckIntervalHours == 0)
        {
            return;
        }

        var (lastCheck, _) = _state.Snapshot;
        var interval = TimeSpan.FromHours(settings.CheckIntervalHours);
        if (lastCheck is not null && DateTimeOffset.UtcNow - lastCheck.Value < interval)
        {
            return;
        }

        var excluded = settings.ExcludedHardwareIds;
        var pending = await _operationQueue.EnqueueAsync(async token =>
        {
            var drivers = await _inventory.GetInstalledDriversAsync(token).ConfigureAwait(false);
            _operationQueue.MarkScanCompleted();

            var count = 0;
            await foreach (var update in _updateSource.FindUpdatesAsync(drivers, token).ConfigureAwait(false))
            {
                if (!excluded.Contains(update.MatchedHardwareId.Value, StringComparer.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }, ct).ConfigureAwait(false);

        _state.Record(DateTimeOffset.UtcNow, pending);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Comprobación programada completada: {Pending} actualización(es) pendiente(s).", pending);
        }
    }
}
