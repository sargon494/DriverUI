namespace Core.Interfaces;

/// <summary>
/// Servicio para creación y gestión de puntos de restauración del sistema Windows (System Restore).
/// </summary>
public interface IRestorePointService
{
    /// <summary>
    /// Crea un punto de restauración antes de proceder con una operación crítica sobre drivers.
    /// Retorna true si el punto fue creado con éxito.
    /// </summary>
    Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default);
}
