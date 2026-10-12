namespace Core.Models;

/// <summary>
/// Identificador de hardware PnP. Ejemplo: "PCI\VEN_8086&DEV_9A49&REV_03"
/// </summary>
public sealed record HardwareId
{
    public required string Value { get; init; }

    /// <summary>
    /// Compara hardware IDs ignorando diferencias de mayúsculas/minúsculas.
    /// </summary>
    public bool Matches(HardwareId other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compara este ID con una cadena de hardware ID ignorando mayúsculas/minúsculas.
    /// </summary>
    public bool Matches(string rawId)
    {
        ArgumentNullException.ThrowIfNull(rawId);
        return string.Equals(Value, rawId, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Value;
}
