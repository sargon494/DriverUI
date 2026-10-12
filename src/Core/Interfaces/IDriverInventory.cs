using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Provee acceso al inventario de controladores instalados en el sistema operativo.
/// </summary>
public interface IDriverInventory
{
    /// <summary>
    /// Obtiene la lista completa de controladores instalados y su estado.
    /// </summary>
    Task<IReadOnlyList<InstalledDriver>> GetInstalledDriversAsync(CancellationToken ct = default);
}
