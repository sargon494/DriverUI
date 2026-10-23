using System.Collections.ObjectModel;
using System.Diagnostics;
using Agent.Ipc.Grpc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Models;
using Core.Services;
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
    [ObservableProperty] private string? _motherboardName;
    [ObservableProperty] private string? _oemVendor;
    [ObservableProperty] private Uri? _oemSupportUrl;

    public ObservableCollection<InstalledDriverMsg> Drivers { get; } = [];
    public ObservableCollection<InstalledDriverMsg> FilteredDrivers { get; } = [];

    /// <summary>Controladores agrupados por clase de dispositivo, para los desplegables de la vista.</summary>
    public ObservableCollection<DriverGroup> Groups { get; } = [];

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

        if (MotherboardName is null)
        {
            await LoadMotherboardAsync();
        }

        // Auto-scan si no hay datos
        if (Drivers.Count == 0)
        {
            await ScanAsync();
        }
    }

    /// <summary>
    /// Muestra la placa base y el soporte oficial de su fabricante. No descarga nada:
    /// solo abre la página oficial en el navegador.
    /// </summary>
    [RelayCommand]
    private void OpenOemSupport()
    {
        if (OemSupportUrl is not { IsAbsoluteUri: true, Scheme: "https" } url)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }

    private async Task LoadMotherboardAsync()
    {
        try
        {
            var board = await _agent.GetMotherboardAsync(PageToken);
            if (!board.Detected)
            {
                return;
            }

            MotherboardName = $"{board.Manufacturer} {board.Product}";
            var entry = OemSupportCatalog.Find(new MotherboardInfo
            {
                Manufacturer = board.Manufacturer,
                Product = board.Product
            });
            OemVendor = entry?.Vendor;
            OemSupportUrl = entry?.SupportUrl;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo obtener la placa base del servicio.");
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

        Groups.Clear();
        foreach (var group in FilteredDrivers
                     .GroupBy(d => string.IsNullOrWhiteSpace(d.DeviceClass) ? "Sin clase" : d.DeviceClass)
                     .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            Groups.Add(new DriverGroup(group.Key, group.ToList()));
        }
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

/// <summary>Un grupo de controladores de la misma clase, para un desplegable.</summary>
public sealed record DriverGroup(string Name, IReadOnlyList<InstalledDriverMsg> Items);
