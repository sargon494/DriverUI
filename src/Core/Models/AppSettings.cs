namespace Core.Models;

/// <summary>
/// Ajustes persistentes del usuario que gestiona el servicio.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Intervalos permitidos para la comprobación programada, en horas. 0 = desactivada.</summary>
    public static readonly int[] AllowedIntervalHours = [0, 24, 168];

    private const int MaxExcludedIds = 500;
    private const int MaxHardwareIdLength = 256;

    /// <summary>Hardware IDs cuyas actualizaciones no se muestran ni se instalan.</summary>
    public IReadOnlyList<string> ExcludedHardwareIds { get; init; } = Array.Empty<string>();

    /// <summary>Periodicidad de la comprobación programada en horas (0 = desactivada).</summary>
    public int CheckIntervalHours { get; init; }

    /// <summary>
    /// Devuelve una copia saneada: elimina entradas vacías o no válidas, deduplica
    /// hardware IDs sin distinguir mayúsculas, limita su número y admite solo intervalos conocidos.
    /// </summary>
    public AppSettings Normalize()
    {
        var ids = ExcludedHardwareIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => id.Length <= MaxHardwareIdLength && !id.Any(char.IsControl))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxExcludedIds)
            .ToList();

        var interval = AllowedIntervalHours.Contains(CheckIntervalHours) ? CheckIntervalHours : 0;

        return this with
        {
            ExcludedHardwareIds = ids,
            CheckIntervalHours = interval
        };
    }
}
