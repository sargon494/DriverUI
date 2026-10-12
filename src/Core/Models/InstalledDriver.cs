namespace Core.Models;

/// <summary>
/// Representa un controlador de dispositivo actualmente instalado en el sistema.
/// </summary>
public sealed record InstalledDriver
{
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string Manufacturer { get; init; }
    public required string DriverVersion { get; init; }
    public required DateTime DriverDate { get; init; }
    public required string InfName { get; init; }
    public required IReadOnlyList<HardwareId> HardwareIds { get; init; }
    public required DriverStatus Status { get; init; }
    public required string DeviceClass { get; init; }
}
