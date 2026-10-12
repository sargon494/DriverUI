namespace Core.Models;

/// <summary>
/// Resultado tras la instalación o intento de instalación de una actualización de driver.
/// </summary>
public sealed record InstallResult
{
    public required string UpdateId { get; init; }
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
    public required bool RebootRequired { get; init; }
    public required string PreviousVersion { get; init; }
    public required string InstalledVersion { get; init; }
    public required DateTime Timestamp { get; init; }
}
