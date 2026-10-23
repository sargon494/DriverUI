namespace Agent.Service.Scheduling;

/// <summary>
/// Resultado de la última comprobación programada. Se mantiene en memoria: tras reiniciar
/// el servicio, la primera comprobación se hace en cuanto venza el intervalo.
/// </summary>
public sealed class ScheduledCheckState
{
    private readonly object _sync = new();
    private DateTimeOffset? _lastCheckUtc;
    private int _pendingUpdates;

    public (DateTimeOffset? LastCheckUtc, int PendingUpdates) Snapshot
    {
        get
        {
            lock (_sync)
            {
                return (_lastCheckUtc, _pendingUpdates);
            }
        }
    }

    public void Record(DateTimeOffset whenUtc, int pendingUpdates)
    {
        lock (_sync)
        {
            _lastCheckUtc = whenUtc;
            _pendingUpdates = pendingUpdates;
        }
    }
}
