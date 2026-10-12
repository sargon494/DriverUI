using System.Management;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Inventory;

/// <summary>
/// Implementación de <see cref="IDriverInventory"/> que consulta el subsistema WMI mediante Win32_PnPSignedDriver.
/// </summary>
public sealed class WmiDriverInventory : IDriverInventory
{
    private readonly ILogger<WmiDriverInventory> _logger;

    public WmiDriverInventory(ILogger<WmiDriverInventory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<InstalledDriver>> GetInstalledDriversAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando escaneo de controladores instalados mediante WMI...");

        return await Task.Run(() =>
        {
            var results = new List<InstalledDriver>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DeviceID, DeviceName, Description, Caption, Manufacturer, DriverVersion, DriverDate, InfName, HardwareID, Status, DeviceClass FROM Win32_PnPSignedDriver");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var deviceId = obj["DeviceID"]?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(deviceId))
                        {
                            continue;
                        }

                        var deviceName = obj["DeviceName"]?.ToString()
                            ?? obj["Description"]?.ToString()
                            ?? obj["Caption"]?.ToString()
                            ?? "Dispositivo desconocido";

                        var manufacturer = obj["Manufacturer"]?.ToString() ?? "Desconocido";
                        var driverVersion = obj["DriverVersion"]?.ToString() ?? "0.0.0.0";
                        var infName = obj["InfName"]?.ToString() ?? "unknown.inf";
                        var deviceClass = obj["DeviceClass"]?.ToString() ?? "Unknown";

                        var rawDate = obj["DriverDate"]?.ToString();
                        var driverDate = ParseWmiDate(rawDate);

                        var rawHardwareId = obj["HardwareID"]?.ToString();
                        var hardwareIds = ParseHardwareIds(rawHardwareId, deviceId);

                        var rawStatus = obj["Status"]?.ToString();
                        var status = ParseStatus(rawStatus);

                        results.Add(new InstalledDriver
                        {
                            DeviceId = deviceId,
                            DeviceName = deviceName,
                            Manufacturer = manufacturer,
                            DriverVersion = driverVersion,
                            DriverDate = driverDate,
                            InfName = infName,
                            HardwareIds = hardwareIds,
                            Status = status,
                            DeviceClass = deviceClass
                        });
                    }
                    finally
                    {
                        obj.Dispose();
                    }
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Escaneo WMI finalizado exitosamente. Total de controladores encontrados: {Count}", results.Count);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("El escaneo de controladores fue cancelado.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar Win32_PnPSignedDriver mediante WMI.");
                throw;
            }

            return results;
        }, ct).ConfigureAwait(false);
    }

    private static DateTime ParseWmiDate(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return DateTime.MinValue;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(rawDate);
        }
        catch
        {
            if (DateTime.TryParse(rawDate, out var parsed))
            {
                return parsed;
            }
            return DateTime.MinValue;
        }
    }

    private static List<HardwareId> ParseHardwareIds(string? rawHardwareId, string deviceId)
    {
        var list = new List<HardwareId>();

        if (!string.IsNullOrWhiteSpace(rawHardwareId))
        {
            list.Add(new HardwareId { Value = rawHardwareId.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(deviceId) && !list.Any(h => h.Matches(deviceId)))
        {
            list.Add(new HardwareId { Value = deviceId.Trim() });
        }

        return list;
    }

    private static DriverStatus ParseStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus) || string.Equals(rawStatus, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return DriverStatus.Ok;
        }

        if (string.Equals(rawStatus, "Error", StringComparison.OrdinalIgnoreCase))
        {
            return DriverStatus.Error;
        }

        if (string.Equals(rawStatus, "Degraded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawStatus, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return DriverStatus.Degraded;
        }

        return DriverStatus.Unknown;
    }
}
