using System.CommandLine;
using System.Globalization;
using Agent.Drivers.DependencyInjection;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/cli-.log", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddSerilog(dispose: true);
            });
            services.AddDriverServices();

            var serviceProvider = services.BuildServiceProvider();

            var rootCommand = new RootCommand("DriverUpdater CLI — Diagnóstico y operaciones de controladores");

            var scanCommand = new Command("scan", "Lista los controladores instalados en el equipo");
            scanCommand.SetHandler(async (context) =>
            {
                await ExecuteScanAsync(serviceProvider, context.GetCancellationToken());
            });

            rootCommand.AddCommand(scanCommand);

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error fatal en la ejecución de la CLI.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error fatal: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static async Task ExecuteScanAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var inventory = serviceProvider.GetRequiredService<IDriverInventory>();

        Console.WriteLine("Escaneando controladores instalados en el sistema...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var drivers = await inventory.GetInstalledDriversAsync(ct);
        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"{"Nombre del dispositivo",-45} {"Fabricante",-25} {"Versión",-18} {"Fecha",-12} {"Estado",-8}");
        Console.WriteLine(new string('-', 112));

        foreach (var driver in drivers.OrderBy(d => d.DeviceClass).ThenBy(d => d.DeviceName))
        {
            var name = Truncate(driver.DeviceName, 43);
            var mfg = Truncate(driver.Manufacturer, 23);
            var ver = Truncate(driver.DriverVersion, 16);
            var date = driver.DriverDate == DateTime.MinValue ? "Desconocida" : driver.DriverDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var status = driver.Status.ToString();

            Console.WriteLine($"{name,-45} {mfg,-25} {ver,-18} {date,-12} {status,-8}");
        }

        Console.WriteLine(new string('-', 112));
        Console.WriteLine($"Total: {drivers.Count} controladores instalados (tiempo transcurrido: {stopwatch.Elapsed.TotalSeconds:F2}s)");
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }
        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}
