using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ui.Wpf.Services;

namespace Ui.Wpf.ViewModels;

public enum AppPage { Dashboard, Updates, History, Settings }

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ShellViewModel> _logger;
    private readonly DriverAgentService _agent;
    private readonly DashboardViewModel _dashboard;
    private readonly UpdatesViewModel _updates;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;

    [ObservableProperty]
    private PageViewModelBase _currentPage;

    [ObservableProperty]
    private AppPage _activePage = AppPage.Dashboard;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _pendingReboot;

    [ObservableProperty]
    private int _updatesBadge;

    private System.Timers.Timer? _statusTimer;

    public ShellViewModel(
        ILogger<ShellViewModel> logger,
        DriverAgentService agent,
        DashboardViewModel dashboard,
        UpdatesViewModel updates,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _logger = logger;
        _agent = agent;
        _dashboard = dashboard;
        _updates = updates;
        _history = history;
        _settings = settings;

        _currentPage = dashboard;

        // Propagar badge de actualizaciones desde el VM de Updates
        _updates.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdatesViewModel.UpdateCount))
            {
                UpdatesBadge = _updates.UpdateCount;
            }
        };
    }

    public async Task InitializeAsync()
    {
        IsConnecting = true;
        IsConnected = await ConnectOrStartAsync();
        IsConnecting = false;

        if (IsConnected)
        {
            await CurrentPage.OnNavigatedToAsync();
            StartStatusPolling();
        }
    }

    [RelayCommand]
    private async Task RetryConnectionAsync()
    {
        IsConnecting = true;
        IsConnected = await ConnectOrStartAsync();
        IsConnecting = false;

        if (IsConnected)
        {
            await CurrentPage.OnNavigatedToAsync();
            StartStatusPolling();
        }
    }

    [RelayCommand]
    private async Task NavigateAsync(AppPage page)
    {
        if (ActivePage == page && !(page == AppPage.Dashboard))
        {
            return;
        }

        CurrentPage.OnNavigatedFrom();
        ActivePage = page;

        CurrentPage = page switch
        {
            AppPage.Dashboard => _dashboard,
            AppPage.Updates => _updates,
            AppPage.History => _history,
            AppPage.Settings => _settings,
            _ => _dashboard,
        };

        if (IsConnected)
        {
            await CurrentPage.OnNavigatedToAsync();
        }
    }

    /// <summary>
    /// Conecta al servicio; si está detenido, lo inicia (arranque bajo demanda) y vuelve a conectar.
    /// </summary>
    private async Task<bool> ConnectOrStartAsync()
    {
        if (await _agent.TryConnectAsync())
        {
            return true;
        }

        return await _agent.TryStartServiceAsync() && await _agent.TryConnectAsync();
    }

    private void StartStatusPolling()
    {
        _statusTimer?.Dispose();
        _statusTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5));
        _statusTimer.Elapsed += async (_, _) => await PollStatusAsync();
        _statusTimer.AutoReset = true;
        _statusTimer.Start();
    }

    private async Task PollStatusAsync()
    {
        try
        {
            var status = await _agent.GetStatusAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                PendingReboot = status.PendingReboot;
                IsConnected = true;
            });
        }
        catch
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = false);
        }
    }

    public void Shutdown()
    {
        _statusTimer?.Dispose();
        CurrentPage.OnNavigatedFrom();
    }

    public void Dispose()
    {
        Shutdown();
    }
}
