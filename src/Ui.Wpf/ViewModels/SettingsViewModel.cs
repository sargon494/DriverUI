using Agent.Ipc.Grpc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ui.Wpf.Services;

namespace Ui.Wpf.ViewModels;

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly DriverAgentService _agent;

    // ── Ajustes de comprobación ─────────────────────────────────
    [ObservableProperty] private bool _autoCheckEnabled = true;
    [ObservableProperty] private int _checkIntervalDays = 1;

    // ── Notificaciones (el servicio las guarda; la UI aún no tiene control) ──
    [ObservableProperty] private bool _notificationsEnabled = true;

    // ── Seguridad ───────────────────────────────────────────────
    [ObservableProperty] private bool _onlyWhql = true;

    // ── Exclusiones ─────────────────────────────────────────────
    [ObservableProperty] private string _newExclusion = "";
    public System.Collections.ObjectModel.ObservableCollection<string> Exclusions { get; } = [];

    // ── UI ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSaved;
    [ObservableProperty] private string? _saveError;

    public SettingsViewModel(ILogger<SettingsViewModel> logger, DriverAgentService agent)
    {
        _logger = logger;
        _agent = agent;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
        try
        {
            var settings = await _agent.GetSettingsAsync(PageToken);
            AutoCheckEnabled = settings.CheckIntervalHours > 0;
            CheckIntervalDays = settings.CheckIntervalHours >= 168 ? 7 : 1;
            NotificationsEnabled = settings.NotificationsEnabled;

            Exclusions.Clear();
            foreach (var id in settings.ExcludedHardwareIds)
            {
                Exclusions.Add(id);
            }

            SaveError = null;
        }
        catch (Exception ex)
        {
            SaveError = "No se pudieron cargar los ajustes del servicio.";
            _logger.LogWarning(ex, "Error al leer los ajustes del servicio.");
        }
    }

    [RelayCommand]
    private void AddExclusion()
    {
        var exc = NewExclusion.Trim();
        if (string.IsNullOrEmpty(exc) || Exclusions.Contains(exc))
        {
            return;
        }

        Exclusions.Add(exc);
        NewExclusion = "";
    }

    [RelayCommand]
    private void RemoveExclusion(string exclusion) =>
        Exclusions.Remove(exclusion);

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaved = false;
        SaveError = null;

        try
        {
            var request = new SettingsMsg
            {
                // El servicio solo admite 0 (desactivado), 24 o 168 horas.
                CheckIntervalHours = AutoCheckEnabled ? (CheckIntervalDays >= 7 ? 168 : 24) : 0,
                NotificationsEnabled = NotificationsEnabled,
            };
            request.ExcludedHardwareIds.AddRange(Exclusions);

            await _agent.UpdateSettingsAsync(request);
            IsSaved = true;
            _logger.LogInformation("Ajustes guardados en el servicio.");

            // Reset indicador
            await Task.Delay(2000);
            IsSaved = false;
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
            _logger.LogError(ex, "Error al guardar ajustes");
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        AutoCheckEnabled = true;
        CheckIntervalDays = 1;
        OnlyWhql = true;
        Exclusions.Clear();
    }
}
