namespace Core.Models;

/// <summary>
/// Fase actual del flujo de instalación o reversión de un controlador.
/// </summary>
public enum InstallPhase
{
    Downloading,
    Verifying,
    CreatingRestorePoint,
    Installing,
    Validating,
    Complete,
    Failed,
    RollingBack
}

/// <summary>
/// Notificación de progreso emitida durante la instalación de un controlador.
/// </summary>
public sealed record InstallProgress
{
    public required int PercentComplete { get; init; }
    public required string StatusMessage { get; init; }
    public required InstallPhase Phase { get; init; }
}
