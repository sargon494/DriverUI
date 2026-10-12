using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Repositorio de persistencia para el historial de operaciones de actualización y reversión.
/// </summary>
public interface IOperationHistory
{
    /// <summary>
    /// Registra una nueva entrada de operación en el historial.
    /// </summary>
    Task RecordAsync(OperationLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el registro completo de operaciones ordenadas cronológicamente.
    /// </summary>
    Task<IReadOnlyList<OperationLogEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene una entrada específica por su identificador único.
    /// </summary>
    Task<OperationLogEntry?> GetByIdAsync(long id, CancellationToken ct = default);
}
