namespace Core.Services;

/// <summary>
/// Compara versiones de controladores según sus segmentos numéricos (ej: "31.0.101.5590").
/// Trata segmentos omitidos como 0 (ej: "10.0" equivale a "10.0.0.0").
/// </summary>
public sealed class VersionComparer : IComparer<string>
{
    public static VersionComparer Default { get; } = new();

    int IComparer<string>.Compare(string? x, string? y) => Compare(x, y);

    /// <summary>
    /// Compara dos versiones de controlador.
    /// Retorna:
    /// - Menor que 0 si version1 &lt; version2
    /// - 0 si son numéricamente equivalentes
    /// - Mayor que 0 si version1 &gt; version2
    /// </summary>
    public static int Compare(string? version1, string? version2)
    {
        ArgumentNullException.ThrowIfNull(version1);
        ArgumentNullException.ThrowIfNull(version2);

        if (string.IsNullOrWhiteSpace(version1))
        {
            throw new ArgumentException("La versión no puede estar vacía.", nameof(version1));
        }

        if (string.IsNullOrWhiteSpace(version2))
        {
            throw new ArgumentException("La versión no puede estar vacía.", nameof(version2));
        }

        var segs1 = ParseSegments(version1, nameof(version1));
        var segs2 = ParseSegments(version2, nameof(version2));

        var maxLen = Math.Max(segs1.Length, segs2.Length);
        for (var i = 0; i < maxLen; i++)
        {
            var val1 = i < segs1.Length ? segs1[i] : 0UL;
            var val2 = i < segs2.Length ? segs2[i] : 0UL;

            var cmp = val1.CompareTo(val2);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    /// <summary>
    /// Retorna true si la versión candidata es estrictamente más nueva que la versión actual.
    /// </summary>
    public static bool IsNewer(string candidate, string current) =>
        Compare(candidate, current) > 0;

    /// <summary>
    /// Valida si una cadena tiene una sintaxis de versión válida (números separados por puntos).
    /// </summary>
    public static bool IsValid(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Trim().Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!ulong.TryParse(part, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static ulong[] ParseSegments(string version, string paramName)
    {
        var rawParts = version.Trim().Split('.', StringSplitOptions.TrimEntries);
        if (rawParts.Length == 0)
        {
            throw new FormatException($"Formato de versión inválido para '{paramName}': '{version}'.");
        }

        var result = new ulong[rawParts.Length];
        for (var i = 0; i < rawParts.Length; i++)
        {
            if (!ulong.TryParse(rawParts[i], out var number))
            {
                throw new FormatException($"El segmento '{rawParts[i]}' en la versión '{version}' no es un número entero válido.");
            }
            result[i] = number;
        }

        return result;
    }
}
