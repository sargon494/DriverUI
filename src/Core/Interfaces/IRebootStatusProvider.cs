namespace Core.Interfaces;

/// <summary>
/// Indica si Windows tiene un reinicio pendiente, leyendo el estado del sistema
/// (no la memoria del servicio), para que el aviso sobreviva a reinicios del propio servicio.
/// </summary>
public interface IRebootStatusProvider
{
    bool IsRebootRequired();
}
