using System.Management;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Inventory;

/// <summary>
/// Lee fabricante y modelo de la placa base desde Win32_BaseBoard.
/// </summary>
public sealed class WmiMotherboardInfoProvider : IMotherboardInfoProvider
{
    private readonly ILogger<WmiMotherboardInfoProvider> _logger;

    public WmiMotherboardInfoProvider(ILogger<WmiMotherboardInfoProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<MotherboardInfo?> GetAsync(CancellationToken ct = default)
    {
        return Task.Run<MotherboardInfo?>(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        ct.ThrowIfCancellationRequested();

                        var manufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                        var product = obj["Product"]?.ToString()?.Trim();

                        if (string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(product))
                        {
                            return null;
                        }

                        return new MotherboardInfo { Manufacturer = manufacturer, Product = product };
                    }
                }

                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo leer la placa base desde WMI.");
                return null;
            }
        }, ct);
    }
}
