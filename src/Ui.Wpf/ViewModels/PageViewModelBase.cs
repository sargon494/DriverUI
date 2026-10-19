using CommunityToolkit.Mvvm.ComponentModel;

namespace Ui.Wpf.ViewModels;

/// <summary>ViewModel base con soporte de CancellationToken por página.</summary>
public abstract partial class PageViewModelBase : ObservableObject, IDisposable
{
    private CancellationTokenSource? _cts;

    protected CancellationToken PageToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>Llamado cuando la página se activa / navega hacia ella.</summary>
    public virtual Task OnNavigatedToAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return Task.CompletedTask;
    }

    /// <summary>Llamado cuando la página se desactiva / se navega fuera.</summary>
    public virtual void OnNavigatedFrom()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
