namespace Core.Models;

/// <summary>
/// Entrada histórica de operaciones del servicio (instalaciones, rollbacks, escaneos).
/// Registra el contexto completo de cada operación para auditoría y rollback.
/// </summary>
public sealed record OperationLogEntry
{
    public required long Id { get; init; }
    public required string OperationType { get; init; } // "Install", "Rollback", "Scan"
    public required string UpdateId { get; init; }
    public required string DeviceName { get; init; }
    public required string HardwareId { get; init; }
    public required string DriverTitle { get; init; }
    public required string PreviousVersion { get; init; }
    public required string NewVersion { get; init; }
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
    public required DateTime Timestamp { get; init; }
}
