using System.Collections.ObjectModel;
using Agent.Ipc.Grpc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ui.Wpf.Services;

namespace Ui.Wpf.ViewModels;

public sealed partial class DashboardViewModel : PageViewModelBase
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly DriverAgentService _agent;

    // ── Estado del servicio ───────────────────────────────────────
    [ObservableProperty] private bool _operationInProgress;
    [ObservableProperty] private bool _pendingReboot;
    [ObservableProperty] private string _lastScan = "Nunca";
    [ObservableProperty] private int _queuedOperations;
    [ObservableProperty] private string _currentPhaseLabel = "";
    [ObservableProperty] private int _percentComplete;

    // ── Inventario ───────────────────────────────────────────────
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _totalDrivers;
    [ObservableProperty] private int _okDrivers;
    [ObservableProperty] private int _errorDrivers;
    [ObservableProperty] private int _degradedDrivers;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasDrivers;

    public ObservableCollection<InstalledDriverMsg> Drivers { get; } = [];
    public ObservableCollection<InstalledDriverMsg> FilteredDrivers { get; } = [];

    // ── Resumen visual ────────────────────────────────────────────
    public string HealthSummary => ErrorDrivers + DegradedDrivers == 0
        ? "Todo en orden"
        : $"{ErrorDrivers + DegradedDrivers} controlador(es) con problemas";

    public bool IsHealthy => ErrorDrivers + DegradedDrivers == 0;

    public DashboardViewModel(ILogger<DashboardViewModel> logger, DriverAgentService agent)
    {
        _logger = logger;
        _agent = agent;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
        await RefreshStatusAsync();

        // Auto-scan si no hay datos
        if (Drivers.Count == 0)
        {
            await ScanAsync();
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ScanAsync(CancellationToken ct = default)
    {
        IsScanning = true;
        ErrorMessage = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, PageToken);
            var response = await _agent.ScanAsync(linked.Token);

            Drivers.Clear();
            foreach (var d in response.Drivers.OrderBy(d => d.DeviceName))
            {
                Drivers.Add(d);
            }

            ApplyFilter();
            UpdateStats();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al escanear: {ex.Message}";
            _logger.LogError(ex, "Error en Scan");
        }
        finally { IsScanning = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredDrivers.Clear();
        var q = SearchText.Trim().ToLowerInvariant();
        var source = string.IsNullOrEmpty(q)
            ? Drivers
            : Drivers.Where(d =>
                d.DeviceName.Contains(q, StringComparison.InvariantCultureIgnoreCase) ||
                d.Manufacturer.Contains(q, StringComparison.InvariantCultureIgnoreCase) ||
                d.DeviceClass.Contains(q, StringComparison.InvariantCultureIgnoreCase));

        foreach (var d in source)
        {
            FilteredDrivers.Add(d);
        }

        HasDrivers = FilteredDrivers.Count > 0;
    }

    private void UpdateStats()
    {
        TotalDrivers = Drivers.Count;
        OkDrivers = Drivers.Count(d => d.Status == DriverStatusProto.DriverStatusOk);
        ErrorDrivers = Drivers.Count(d => d.Status == DriverStatusProto.DriverStatusError);
        DegradedDrivers = Drivers.Count(d => d.Status == DriverStatusProto.DriverStatusDegraded);
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(IsHealthy));
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var status = await _agent.GetStatusAsync(PageToken);
            OperationInProgress = status.OperationInProgress;
            PendingReboot = status.PendingReboot;
            QueuedOperations = status.QueuedOperations;
            PercentComplete = status.PercentComplete;

            LastScan = status.LastScan is { } ts && ts.Seconds > 0
                ? ts.ToDateTimeOffset().LocalDateTime.ToString("dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : "Nunca";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener estado");
        }
    }
}
