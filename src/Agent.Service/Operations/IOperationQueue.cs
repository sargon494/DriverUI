using Core.Models;

namespace Agent.Service.Operations;

/// <summary>
/// Cola que serializa las operaciones privilegiadas del servicio (búsqueda de
/// actualizaciones, instalaciones y rollbacks) para garantizar que solo una
/// se ejecuta a la vez, y expone el estado observable para la UI.
/// </summary>
public interface IOperationQueue
{
    /// <summary>
    /// Estado actual de las operaciones del servicio.
    /// </summary>
    OperationState State { get; }

    /// <summary>
    /// Registra y ejecuta una operación en exclusión mutua: si ya hay otra en
    /// curso, espera (de forma cancelable) a que finalice antes de arrancar.
    /// Al terminar (con éxito o error), la cola queda libre y el estado vuelve a inactivo.
    /// </summary>
    /// <typeparam name="T">Tipo del resultado de la operación.</typeparam>
    /// <param name="operation">Delegado que ejecuta la operación; recibe el token de cancelación de la llamada.</param>
    /// <param name="ct">Token de cancelación; interrumpe tanto la espera como la propia operación.</param>
    /// <returns>Resultado producido por la operación.</returns>
    Task<T> EnqueueAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);

    /// <summary>
    /// Variante sin resultado de <see cref="EnqueueAsync{T}"/>.
    /// </summary>
    Task EnqueueAsync(Func<CancellationToken, Task> operation, CancellationToken ct);

    /// <summary>
    /// Notifica el progreso de la instalación en curso para exponerlo vía <see cref="State"/>.
    /// </summary>
    void UpdateProgress(string updateId, InstallProgress progress);

    /// <summary>
    /// Marca que una instalación solicitó reinicio del equipo. El indicador
    /// permanece activo hasta que el servicio se reinicia.
    /// </summary>
    void MarkPendingReboot();

    /// <summary>
    /// Registra la finalización de un escaneo de inventario.
    /// </summary>
    void MarkScanCompleted();
}
