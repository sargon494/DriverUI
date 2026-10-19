using Core.Models;

namespace Agent.Service.Operations;

/// <summary>
/// Implementación de <see cref="IOperationQueue"/> basada en un semáforo de
/// una entrada con estado observable inmutable e intercambiable de forma segura.
/// </summary>
public sealed class OperationQueue : IOperationQueue, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateLock = new();

    private OperationState _state = OperationState.Initial;

    /// <inheritdoc/>
    public OperationState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<T> EnqueueAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);

        RegisterOperation();

        try
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                lock (_stateLock)
                {
                    _state = _state with { OperationInProgress = true };
                }

                return await operation(ct).ConfigureAwait(false);
            }
            finally
            {
                lock (_stateLock)
                {
                    _state = _state with
                    {
                        OperationInProgress = false,
                        CurrentUpdateId = null,
                        PercentComplete = 0,
                        Phase = null
                    };
                }

                _gate.Release();
            }
        }
        finally
        {
            UnregisterOperation();
        }
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await EnqueueAsync<object?>(async ct2 =>
        {
            await operation(ct2).ConfigureAwait(false);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void UpdateProgress(string updateId, InstallProgress progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updateId);
        ArgumentNullException.ThrowIfNull(progress);

        lock (_stateLock)
        {
            _state = _state with
            {
                CurrentUpdateId = updateId,
                PercentComplete = progress.PercentComplete,
                Phase = progress.Phase
            };
        }
    }

    /// <inheritdoc/>
    public void MarkPendingReboot()
    {
        lock (_stateLock)
        {
            _state = _state with { PendingReboot = true };
        }
    }

    /// <inheritdoc/>
    public void MarkScanCompleted()
    {
        lock (_stateLock)
        {
            _state = _state with { LastScanUtc = DateTimeOffset.UtcNow };
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void RegisterOperation()
    {
        lock (_stateLock)
        {
            _state = _state with { QueuedOperations = _state.QueuedOperations + 1 };
        }
    }

    private void UnregisterOperation()
    {
        lock (_stateLock)
        {
            var queued = Math.Max(0, _state.QueuedOperations - 1);
            _state = _state with { QueuedOperations = queued };
        }
    }
}
