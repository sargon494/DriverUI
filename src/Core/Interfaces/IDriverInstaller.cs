using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Gestiona la instalación segura y la reversión (rollback) de controladores.
/// </summary>
public interface IDriverInstaller
{
    /// <summary>
    /// Instala una actualización de controlador siguiendo el flujo de seguridad estricto.
    /// </summary>
    Task<InstallResult> InstallAsync(
        DriverUpdate update,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deshace una instalación de controlador previamente realizada según su identificador de operación.
    /// </summary>
    Task<InstallResult> RollbackAsync(
        string operationId,
        CancellationToken ct = default);
}
