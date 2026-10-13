using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Interfaces;
using Core.Models;
using Core.Services;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Updates;

/// <summary>
/// Implementación de <see cref="IUpdateSource"/> que consulta Windows Update Agent (COM)
/// para encontrar actualizaciones de controladores disponibles.
/// </summary>
public sealed class WuaUpdateSource : IUpdateSource
{
    private const string WuaSearchCriteria = "IsInstalled=0 AND Type='Driver'";
    private const string WuaProgId = "Microsoft.Update.Session";

    private readonly ILogger<WuaUpdateSource> _logger;

    public WuaUpdateSource(ILogger<WuaUpdateSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DriverUpdate> FindUpdatesAsync(
        IReadOnlyList<InstalledDriver> installedDrivers,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(installedDrivers);

        _logger.LogInformation("Iniciando búsqueda de actualizaciones de drivers mediante Windows Update Agent...");

        var updates = await Task.Run(
            () => SearchForDriverUpdates(installedDrivers, ct), ct).ConfigureAwait(false);

        foreach (var update in updates)
        {
            ct.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    private List<DriverUpdate> SearchForDriverUpdates(
        IReadOnlyList<InstalledDriver> installedDrivers,
        CancellationToken ct)
    {
        var results = new List<DriverUpdate>();

        var sessionType = Type.GetTypeFromProgID(WuaProgId);
        if (sessionType is null)
        {
            _logger.LogError("No se pudo obtener el tipo COM. Windows Update Agent no está disponible.");
            return results;
        }

        dynamic? session = null;
        dynamic? searcher = null;
        dynamic? searchResult = null;

        try
        {
            session = Activator.CreateInstance(sessionType)
                ?? throw new InvalidOperationException(
                    "No se pudo crear la instancia de Microsoft.Update.Session.");

            searcher = session.CreateUpdateSearcher();

            _logger.LogInformation("Ejecutando búsqueda WUA con criterio de drivers no instalados...");

            ct.ThrowIfCancellationRequested();
            searchResult = searcher.Search(WuaSearchCriteria);

            int count = (int)searchResult.Updates.Count;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Windows Update Agent encontró {Count} actualización(es) de tipo driver.", count);
            }

            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();

                dynamic? update = null;
                try
                {
                    update = searchResult.Updates.Item(i);
                    var driverUpdate = MapToDriverUpdate(update, installedDrivers);
                    if (driverUpdate is not null)
                    {
                        results.Add(driverUpdate);
                    }
                }
                catch (COMException ex)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex,
                            "Error COM al procesar la actualización en índice {Index}.", i);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex,
                            "Error inesperado al procesar la actualización en índice {Index}.", i);
                    }
                }
                finally
                {
                    if (update is not null)
                    {
                        Marshal.ReleaseComObject(update);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("La búsqueda de actualizaciones fue cancelada.");
            throw;
        }
        catch (COMException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex,
                    "Error COM al buscar actualizaciones con Windows Update Agent. HRESULT: 0x{HResult:X8}",
                    ex.HResult);
            }

            throw;
        }
        finally
        {
            ReleaseCom(searchResult);
            ReleaseCom(searcher);
            ReleaseCom(session);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Búsqueda finalizada. {Count} actualización(es) de driver válidas encontradas.",
                results.Count);
        }

        return results;
    }

    private DriverUpdate? MapToDriverUpdate(
        dynamic update,
        IReadOnlyList<InstalledDriver> installedDrivers)
    {
        string title = (string)(update.Title ?? string.Empty);
        string updateId = (string)(update.Identity.UpdateID ?? Guid.NewGuid().ToString());
        string description = (string)(update.Description ?? string.Empty);
        long downloadSize = (long)update.MaxDownloadSize;
        bool isMandatory = (bool)update.IsMandatory;
        DateTime releaseDate = (DateTime)update.LastDeploymentChangeTime;

        // Obtener el hardware ID del driver (propiedad de IWindowsDriverUpdate)
        string? driverHardwareId = GetDriverHardwareId(update, title);

        if (string.IsNullOrWhiteSpace(driverHardwareId))
        {
            return null;
        }

        // Extraer versión del título de la actualización
        var newVersion = ExtractVersionFromTitle(title) ?? "0.0.0.0";

        // Cruzar con el inventario instalado por hardware ID
        var matchedHwId = new HardwareId { Value = driverHardwareId };
        var matchedDriver = FindMatchingInstalledDriver(installedDrivers, matchedHwId);

        var currentVersion = matchedDriver?.DriverVersion ?? "0.0.0.0";
        var deviceName = matchedDriver?.DeviceName ?? ExtractDeviceNameFromTitle(title);

        return new DriverUpdate
        {
            UpdateId = updateId,
            Title = title,
            Description = description,
            NewVersion = newVersion,
            CurrentVersion = currentVersion,
            MatchedHardwareId = matchedHwId,
            DeviceName = deviceName,
            DownloadSizeBytes = downloadSize,
            IsMandatory = isMandatory,
            ReleaseDate = releaseDate
        };
    }

    private static string? GetDriverHardwareId(dynamic update, string title)
    {
        try
        {
            string? hwId = update.DriverHardwareID;
            return string.IsNullOrWhiteSpace(hwId) ? null : hwId;
        }
        catch (COMException)
        {
            return null;
        }
        catch (RuntimeBinderException)
        {
            return null;
        }
    }

    private static InstalledDriver? FindMatchingInstalledDriver(
        IReadOnlyList<InstalledDriver> installedDrivers,
        HardwareId targetHwId)
    {
        foreach (var driver in installedDrivers)
        {
            foreach (var hwId in driver.HardwareIds)
            {
                if (hwId.Matches(targetHwId))
                {
                    return driver;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extrae la versión numérica del título de una actualización de driver.
    /// Los títulos de WUA siguen el patrón: "Fabricante - Categoría - Versión".
    /// </summary>
    internal static string? ExtractVersionFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        // Patrón principal: segmentos separados por " - "
        var segments = title.Split(" - ", StringSplitOptions.TrimEntries);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            if (VersionComparer.IsValid(segments[i]))
            {
                return segments[i];
            }
        }

        // Fallback: buscar cualquier token con forma de versión en el título
        var tokens = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            var candidate = tokens[i].Trim(',', '(', ')', '[', ']');
            if (candidate.Contains('.', StringComparison.Ordinal)
                && VersionComparer.IsValid(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ExtractDeviceNameFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Dispositivo desconocido";
        }

        var segments = title.Split(" - ", StringSplitOptions.TrimEntries);
        if (segments.Length >= 2)
        {
            // "Fabricante - Categoría - Versión" → "Fabricante Categoría"
            return $"{segments[0]} {segments[1]}";
        }

        return title;
    }

    private static void ReleaseCom(dynamic? comObject)
    {
        if (comObject is not null)
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
