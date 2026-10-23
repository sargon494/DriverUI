using System.Collections.ObjectModel;
using Agent.Ipc.Grpc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ui.Wpf.Services;

namespace Ui.Wpf.ViewModels;

/// <summary>
/// Estado de la instalación de un driver individual, observable por la UI.
/// </summary>
public sealed partial class DriverInstallItem : ObservableObject
{
    public string UpdateId { get; init; } = "";
    public string HardwareId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string Title { get; init; } = "";
    public string OldVersion { get; init; } = "";
    public string NewVersion { get; init; } = "";
    public long SizeBytes { get; init; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private int _percent;
    [ObservableProperty] private string _phaseLabel = "";
    [ObservableProperty] private bool? _success;          // null = pendiente
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _rebootRequired;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _isFinished;
}

public sealed partial class UpdatesViewModel : PageViewModelBase
{
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly DriverAgentService _agent;

    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _isCancelling;
    [ObservableProperty] private int _updateCount;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _globalStatus = "";
    [ObservableProperty] private int _totalToInstall;
    [ObservableProperty] private int _completedInstalls;

    // ¿Hay al menos uno seleccionado?
    public bool HasSelection => Updates.Any(u => u.IsSelected);
    public bool HasUpdates => Updates.Count > 0;
    public bool AllSelected
    {
        get => Updates.Count > 0 && Updates.All(u => u.IsSelected);
        set { foreach (var u in Updates) { u.IsSelected = value; } OnPropertyChanged(nameof(HasSelection)); }
    }

    public ObservableCollection<DriverInstallItem> Updates { get; } = [];

    private CancellationTokenSource? _installCts;

    public UpdatesViewModel(ILogger<UpdatesViewModel> logger, DriverAgentService agent)
    {
        _logger = logger;
        _agent = agent;

        Updates.CollectionChanged += (_, _) =>
        {
            UpdateCount = Updates.Count;
            OnPropertyChanged(nameof(HasUpdates));
            OnPropertyChanged(nameof(AllSelected));
        };
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
        if (Updates.Count == 0)
        {
            await CheckUpdatesAsync();
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CheckUpdatesAsync(CancellationToken ct = default)
    {
        IsSearching = true;
        ErrorMessage = null;
        Updates.Clear();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, PageToken);
            await foreach (var msg in _agent.CheckUpdatesAsync(linked.Token))
            {
                var item = new DriverInstallItem
                {
                    UpdateId = msg.UpdateId,
                    HardwareId = msg.HardwareId,
                    DeviceName = msg.DeviceName,
                    Title = msg.Title,
                    OldVersion = msg.CurrentVersion,
                    NewVersion = msg.NewVersion,
                    SizeBytes = msg.DownloadSizeBytes,
                };
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DriverInstallItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(HasSelection));
                        OnPropertyChanged(nameof(AllSelected));
                    }
                };
                Updates.Add(item);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al buscar actualizaciones: {ex.Message}";
            _logger.LogError(ex, "Error en CheckUpdates");
        }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var selected = Updates.Where(u => u.IsSelected && u.Success is null).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        IsInstalling = true;
        IsCancelling = false;
        TotalToInstall = selected.Count;
        CompletedInstalls = 0;
        GlobalStatus = $"Instalando 0 de {TotalToInstall}...";

        // Marcar inmediatamente los elementos seleccionados como en proceso para que la barra de progreso aparezca sin retraso
        foreach (var item in selected)
        {
            item.IsInstalling = true;
            item.Percent = 0;
            item.PhaseLabel = "Iniciando instalación...";
            item.ErrorMessage = "";
        }

        _installCts?.Dispose();
        _installCts = CancellationTokenSource.CreateLinkedTokenSource(PageToken);

        try
        {
            var ids = selected.Select(u => u.UpdateId);
            await foreach (var msg in _agent.InstallAsync(ids, _installCts.Token))
            {
                var item = selected.FirstOrDefault(u => u.UpdateId == msg.UpdateId);
                if (item is null)
                {
                    continue;
                }

                item.Percent = msg.PercentComplete;
                item.PhaseLabel = FormatPhase(msg.Phase, msg.StatusMessage);

                if (msg.Finished)
                {
                    item.IsFinished = true;
                    item.IsInstalling = false;
                    item.Success = msg.Success;
                    item.ErrorMessage = msg.ErrorMessage;
                    item.RebootRequired = msg.RebootRequired;
                    CompletedInstalls++;
                    GlobalStatus = IsCancelling
                        ? "Cancelando... esperando a que finalice el controlador en curso"
                        : $"Instalando {CompletedInstalls} de {TotalToInstall}...";
                }
                else
                {
                    item.IsInstalling = true;
                }
            }

            GlobalStatus = CompletedInstalls == TotalToInstall
                ? $"Instalación finalizada ({TotalToInstall} controladores)"
                : $"Completado con errores ({CompletedInstalls}/{TotalToInstall})";
        }
        catch (OperationCanceledException)
        {
            GlobalStatus = "Instalación cancelada por el usuario.";
            foreach (var item in selected.Where(u => !u.IsFinished))
            {
                item.IsInstalling = false;
                item.PhaseLabel = "Cancelado";
            }
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Error inesperado: {ex.Message}";
            _logger.LogError(ex, "Error en Install");
            foreach (var item in selected.Where(u => !u.IsFinished))
            {
                item.IsInstalling = false;
                item.PhaseLabel = "Error";
            }
        }
        finally
        {
            IsInstalling = false;
            IsCancelling = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    [RelayCommand]
    private void CancelInstall()
    {
        if (_installCts is null)
        {
            return;
        }

        IsCancelling = true;
        GlobalStatus = "Cancelando... esperando a que finalice el controlador en curso";
        _installCts.Cancel();
    }

    [RelayCommand]
    private void SelectAll() => AllSelected = true;

    [RelayCommand]
    private void SelectNone() => AllSelected = false;

    /// <summary>
    /// Excluye el dispositivo de la actualización: su hardware ID se guarda en los ajustes
    /// del servicio y la actualización desaparece de la lista.
    /// </summary>
    [RelayCommand]
    private async Task ExcludeAsync(DriverInstallItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.HardwareId))
        {
            return;
        }

        try
        {
            var settings = await _agent.GetSettingsAsync(PageToken);
            if (!settings.ExcludedHardwareIds.Contains(item.HardwareId, StringComparer.OrdinalIgnoreCase))
            {
                var request = settings.Clone();
                request.ExcludedHardwareIds.Add(item.HardwareId);
                await _agent.UpdateSettingsAsync(request, PageToken);
            }

            Updates.Remove(item);
            UpdateCount = Updates.Count;
            OnPropertyChanged(nameof(HasUpdates));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(AllSelected));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo excluir la actualización: {ex.Message}";
            _logger.LogError(ex, "Error al excluir {HardwareId}", item.HardwareId);
        }
    }

    private static string FormatPhase(InstallPhaseProto phase, string statusMsg) =>
        !string.IsNullOrWhiteSpace(statusMsg)
            ? statusMsg
            : phase switch
            {
                InstallPhaseProto.InstallPhaseDownloading => "Descargando...",
                InstallPhaseProto.InstallPhaseVerifying => "Verificando firma...",
                InstallPhaseProto.InstallPhaseCreatingRestorePoint => "Creando punto de restauración...",
                InstallPhaseProto.InstallPhaseInstalling => "Instalando...",
                InstallPhaseProto.InstallPhaseValidating => "Validando...",
                InstallPhaseProto.InstallPhaseComplete => "Completado",
                InstallPhaseProto.InstallPhaseFailed => "Error",
                InstallPhaseProto.InstallPhaseRollingBack => "Deshaciendo...",
                _ => "Esperando...",
            };
}
