using System.Management;
using Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Restore;

/// <summary>
/// Implementación de <see cref="IRestorePointService"/> mediante WMI (clase <c>SystemRestore</c> en <c>root\default</c>).
/// </summary>
/// <remarks>
/// <para>Esta operación requiere privilegios de administrador o cuenta SYSTEM (Windows Service).</para>
/// <para>Si la Protección del sistema está desactivada en Windows, la llamada puede retornar fallo.</para>
/// </remarks>
public sealed class WmiRestorePointService : IRestorePointService
{
    private const int DeviceDriverInstall = 10;
    private const int BeginSystemChange = 100;

    private readonly ILogger<WmiRestorePointService> _logger;

    public WmiRestorePointService(ILogger<WmiRestorePointService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return Task.Run(() => CreateInternal(description), ct);
    }

    private bool CreateInternal(string description)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Creando punto de restauración del sistema: '{Description}'...", description);
        }

        try
        {
            var scope = new ManagementScope(@"\\.\root\default");
            scope.Connect();

            using var managementClass = new ManagementClass(scope, new ManagementPath("SystemRestore"), new ObjectGetOptions());
            using var inParams = managementClass.GetMethodParameters("CreateRestorePoint");

            inParams["Description"] = description;
            inParams["RestorePointType"] = DeviceDriverInstall;
            inParams["EventType"] = BeginSystemChange;

            using var outParams = managementClass.InvokeMethod("CreateRestorePoint", inParams, new InvokeMethodOptions());

            if (outParams != null && outParams["ReturnValue"] != null)
            {
                var returnValue = Convert.ToUInt32(outParams["ReturnValue"], System.Globalization.CultureInfo.InvariantCulture);
                if (returnValue == 0)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Punto de restauración creado exitosamente.");
                    }

                    return true;
                }

                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("SystemRestore retornó código de error: {ReturnValue}", returnValue);
                }

                return false;
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("SystemRestore no retornó parámetros de salida.");
            }

            return false;
        }
        catch (ManagementException mex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(mex, "Error de WMI al intentar crear punto de restauración (ErrorCode: {ErrorCode})", mex.ErrorCode);
            }

            return false;
        }
        catch (UnauthorizedAccessException uex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(uex, "Permisos insuficientes para crear punto de restauración. Se requiere elevación (SYSTEM/Administrador).");
            }

            return false;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Excepción inesperada al crear punto de restauración.");
            }

            return false;
        }
    }
}
