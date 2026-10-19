using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Ui.Wpf.ViewModels;

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly ILogger<SettingsViewModel> _logger;

    // ── Ajustes de comprobación ─────────────────────────────────
    [ObservableProperty] private bool _autoCheckEnabled = true;
    [ObservableProperty] private int _checkIntervalDays = 1;

    // ── Seguridad ───────────────────────────────────────────────
    [ObservableProperty] private bool _onlyWhql = true;

    // ── Exclusiones ─────────────────────────────────────────────
    [ObservableProperty] private string _newExclusion = "";
    public System.Collections.ObjectModel.ObservableCollection<string> Exclusions { get; } = [];

    // ── UI ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSaved;
    [ObservableProperty] private string? _saveError;

    public SettingsViewModel(ILogger<SettingsViewModel> logger)
    {
        _logger = logger;
        // TODO: cargar desde config.json en %ProgramData%\DriverUpdater\
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
            // TODO: enviar GetSettings/UpdateSettings al servicio vía gRPC (Sprint futuro)
            await Task.Delay(200); // placeholder
            IsSaved = true;
            _logger.LogInformation("Ajustes guardados (placeholder).");

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
