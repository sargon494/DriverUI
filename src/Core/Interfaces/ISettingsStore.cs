using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Almacén de los ajustes del usuario. Las implementaciones deben escribir de forma atómica.
/// </summary>
public interface ISettingsStore
{
    Task<AppSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Guarda los ajustes. Se espera que el llamador ya haya normalizado los valores.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
