namespace Core.Models;

/// <summary>
/// Fabricante y modelo de la placa base, leídos de Win32_BaseBoard.
/// </summary>
public sealed record MotherboardInfo
{
    public required string Manufacturer { get; init; }
    public required string Product { get; init; }
}
