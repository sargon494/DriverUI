using Core.Models;

namespace Agent.Service.Operations;

/// <summary>
/// Instantánea inmutable del estado observable de las operaciones del servicio:
/// operación en curso, progreso, reinicio pendiente y último escaneo.
/// </summary>
public sealed record OperationState
{
    public static readonly OperationState Initial = new()
    {
        OperationInProgress = false,
        PendingReboot = false,
        QueuedOperations = 0
    };

    /// <summary>Indica si hay una operación privilegiada en ejecución (búsqueda, instalación o rollback).</summary>
    public required bool OperationInProgress { get; init; }

    /// <summary>Identificador de la actualización que se está instalando, si aplica.</summary>
    public string? CurrentUpdateId { get; init; }

    /// <summary>Porcentaje de progreso de la operación actual (0-100).</summary>
    public int PercentComplete { get; init; }

    /// <summary>Fase actual de la instalación en curso, si aplica.</summary>
    public InstallPhase? Phase { get; init; }

    /// <summary>Indica que una instalación anterior solicitó reiniciar el equipo.</summary>
    public required bool PendingReboot { get; init; }

    /// <summary>Instante (UTC) del último escaneo de inventario completado.</summary>
    public DateTimeOffset? LastScanUtc { get; init; }

    /// <summary>Número de operaciones registradas en la cola, incluida la que está en ejecución.</summary>
    public required int QueuedOperations { get; init; }
}
