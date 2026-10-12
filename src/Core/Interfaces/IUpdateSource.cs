using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Fuente proveedora de actualizaciones de controladores (ej: Windows Update Agent).
/// </summary>
public interface IUpdateSource
{
    /// <summary>
    /// Busca actualizaciones disponibles comparando contra los controladores instalados.
    /// </summary>
    IAsyncEnumerable<DriverUpdate> FindUpdatesAsync(
        IReadOnlyList<InstalledDriver> installedDrivers,
        CancellationToken ct = default);
}
