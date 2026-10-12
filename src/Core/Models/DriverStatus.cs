namespace Core.Models;

/// <summary>
/// Estado operativo del dispositivo/driver según el subsistema PnP de Windows.
/// </summary>
public enum DriverStatus
{
    Ok,
    Error,
    Degraded,
    Unknown
}
