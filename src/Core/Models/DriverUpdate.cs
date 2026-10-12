namespace Core.Models;

/// <summary>
/// Representa una actualización disponible de controlador de dispositivo.
/// </summary>
public sealed record DriverUpdate
{
    public required string UpdateId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string NewVersion { get; init; }
    public required string CurrentVersion { get; init; }
    public required HardwareId MatchedHardwareId { get; init; }
    public required string DeviceName { get; init; }
    public required long DownloadSizeBytes { get; init; }
    public required bool IsMandatory { get; init; }
    public required DateTime ReleaseDate { get; init; }
}
