using Core.Models;

namespace Core.Services;

/// <summary>
/// Servicio para asociar y seleccionar la mejor actualización disponible para controladores instalados.
/// </summary>
public static class DriverMatcher
{
    /// <summary>
    /// Dado un driver instalado y una lista de actualizaciones candidatas,
    /// selecciona la mejor actualización:
    /// - Debe coincidir con alguno de los Hardware IDs del dispositivo.
    /// - Su versión debe ser estrictamente superior a la versión instalada.
    /// - En caso de múltiples opciones válidas, selecciona la versión más alta (y más reciente en caso de empate).
    /// </summary>
    public static DriverUpdate? FindBestMatch(
        InstalledDriver installed,
        IEnumerable<DriverUpdate>? candidates)
    {
        ArgumentNullException.ThrowIfNull(installed);

        if (candidates is null)
        {
            return null;
        }

        DriverUpdate? bestMatch = null;

        foreach (var candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            // Verificar si el candidato coincide con alguno de los HardwareIds del dispositivo
            var matchesHardware = false;
            foreach (var hwId in installed.HardwareIds)
            {
                if (hwId.Matches(candidate.MatchedHardwareId))
                {
                    matchesHardware = true;
                    break;
                }
            }

            if (!matchesHardware)
            {
                continue;
            }

            // Verificar que la nueva versión sea estrictamente superior a la instalada
            if (!VersionComparer.IsValid(candidate.NewVersion) ||
                !VersionComparer.IsValid(installed.DriverVersion) ||
                !VersionComparer.IsNewer(candidate.NewVersion, installed.DriverVersion))
            {
                continue;
            }

            // Si es la primera coincidencia válida encontrada
            if (bestMatch is null)
            {
                bestMatch = candidate;
                continue;
            }

            // Comparar contra el mejor actual
            var versionDiff = VersionComparer.Compare(candidate.NewVersion, bestMatch.NewVersion);
            if (versionDiff > 0)
            {
                bestMatch = candidate;
            }
            else if (versionDiff == 0 && candidate.ReleaseDate > bestMatch.ReleaseDate)
            {
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Para una lista de controladores instalados y una colección de actualizaciones,
    /// empareja cada driver instalado con su mejor actualización si existe.
    /// </summary>
    public static IReadOnlyDictionary<InstalledDriver, DriverUpdate> FindAllMatches(
        IEnumerable<InstalledDriver> installedDrivers,
        IEnumerable<DriverUpdate> candidates)
    {
        ArgumentNullException.ThrowIfNull(installedDrivers);
        ArgumentNullException.ThrowIfNull(candidates);

        var candidateList = candidates.ToList();
        var results = new Dictionary<InstalledDriver, DriverUpdate>();

        foreach (var driver in installedDrivers)
        {
            var best = FindBestMatch(driver, candidateList);
            if (best is not null)
            {
                results[driver] = best;
            }
        }

        return results;
    }
}
