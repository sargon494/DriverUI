using System.Collections.ObjectModel;
using Agent.Ipc.Grpc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ui.Wpf.Services;

namespace Ui.Wpf.ViewModels;

public sealed partial class HistoryViewModel : PageViewModelBase
{
    private readonly ILogger<HistoryViewModel> _logger;
    private readonly DriverAgentService _agent;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _isRollingBack;
    [ObservableProperty] private string? _rollbackResult;
    [ObservableProperty] private bool _rollbackSuccess;

    public ObservableCollection<OperationLogEntryMsg> Entries { get; } = [];
    public ObservableCollection<OperationLogEntryMsg> Filtered { get; } = [];

    // Primer ítem seleccionado (para rollback)
    [ObservableProperty] private OperationLogEntryMsg? _selectedEntry;

    public bool CanRollback => SelectedEntry is not null
                            && SelectedEntry.Success
                            && SelectedEntry.OperationType == "Install";

    public HistoryViewModel(ILogger<HistoryViewModel> logger, DriverAgentService agent)
    {
        _logger = logger;
        _agent = agent;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        RollbackResult = null;

        try
        {
            var response = await _agent.GetHistoryAsync(200, PageToken);
            Entries.Clear();
            foreach (var e in response.Entries)
            {
                Entries.Add(e);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar historial: {ex.Message}";
            _logger.LogError(ex, "Error en GetHistory");
        }
        finally { IsLoading = false; }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSelectedEntryChanged(OperationLogEntryMsg? value) =>
        OnPropertyChanged(nameof(CanRollback));

    private void ApplyFilter()
    {
        Filtered.Clear();
        var q = FilterText.Trim().ToLowerInvariant();
        var source = string.IsNullOrEmpty(q)
            ? Entries
            : (IEnumerable<OperationLogEntryMsg>)Entries.Where(e =>
                e.DeviceName.Contains(q, StringComparison.InvariantCultureIgnoreCase) ||
                e.DriverTitle.Contains(q, StringComparison.InvariantCultureIgnoreCase) ||
                e.NewVersion.Contains(q, StringComparison.InvariantCultureIgnoreCase));

        foreach (var e in source)
        {
            Filtered.Add(e);
        }
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        if (!CanRollback)
        {
            return;
        }

        IsRollingBack = true;
        RollbackResult = null;

        try
        {
            // El ID de operación es el id numérico de la entrada del log
            var result = await _agent.RollbackAsync(SelectedEntry.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), PageToken);
            RollbackSuccess = result.Success;
            RollbackResult = result.Success
                ? $"Rollback exitoso. Versión restaurada: {result.InstalledVersion}"
                : $"Rollback fallido: {result.ErrorMessage}";

            if (result.Success)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            RollbackResult = $"Error: {ex.Message}";
            RollbackSuccess = false;
            _logger.LogError(ex, "Error en Rollback");
        }
        finally { IsRollingBack = false; }
    }
}
