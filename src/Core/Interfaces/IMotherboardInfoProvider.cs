using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Obtiene el fabricante y modelo de la placa base. Devuelve null si no se puede leer.
/// </summary>
public interface IMotherboardInfoProvider
{
    Task<MotherboardInfo?> GetAsync(CancellationToken ct = default);
}
