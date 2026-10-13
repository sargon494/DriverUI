using System.CommandLine;
using System.Globalization;
using System.Security.Principal;
using Agent.Drivers.DependencyInjection;
using Core.Interfaces;
using Core.Models;
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

            // ─── Comando: scan ───────────────────────────────────────────────
            var scanCommand = new Command("scan", "Lista los controladores instalados en el equipo");
            scanCommand.SetHandler(async (context) =>
            {
                await ExecuteScanAsync(serviceProvider, context.GetCancellationToken());
            });

            // ─── Comando: updates ────────────────────────────────────────────
            var updatesCommand = new Command("updates", "Busca actualizaciones de drivers disponibles vía Windows Update");
            updatesCommand.SetHandler(async (context) =>
            {
                await ExecuteUpdatesAsync(serviceProvider, context.GetCancellationToken());
            });

            // ─── Comando: log ────────────────────────────────────────────────
            var logCommand = new Command("log", "Muestra el historial de operaciones de instalación y reversión");
            logCommand.SetHandler(async (context) =>
            {
                await ExecuteLogAsync(serviceProvider, context.GetCancellationToken());
            });

            // ─── Comando: install <updateId> ─────────────────────────────────
            var updateIdArgument = new Argument<string>("updateId", "Identificador de la actualización a instalar (obtenido de 'updates')");
            var installCommand = new Command("install", "Instala una actualización de controlador específica")
            {
                updateIdArgument
            };
            installCommand.SetHandler(async (context) =>
            {
                var updateId = context.ParseResult.GetValueForArgument(updateIdArgument);
                await ExecuteInstallAsync(serviceProvider, updateId, context.GetCancellationToken());
            });

            // ─── Comando: rollback <operationId> ─────────────────────────────
            var operationIdArgument = new Argument<string>("operationId", "Identificador de la operación a revertir (obtenido de 'log')");
            var rollbackCommand = new Command("rollback", "Revierte una instalación de controlador realizada previamente")
            {
                operationIdArgument
            };
            rollbackCommand.SetHandler(async (context) =>
            {
                var operationId = context.ParseResult.GetValueForArgument(operationIdArgument);
                await ExecuteRollbackAsync(serviceProvider, operationId, context.GetCancellationToken());
            });

            rootCommand.AddCommand(scanCommand);
            rootCommand.AddCommand(updatesCommand);
            rootCommand.AddCommand(logCommand);
            rootCommand.AddCommand(installCommand);
            rootCommand.AddCommand(rollbackCommand);

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

    private static async Task ExecuteUpdatesAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var inventory = serviceProvider.GetRequiredService<IDriverInventory>();
        var updateSource = serviceProvider.GetRequiredService<IUpdateSource>();

        Console.WriteLine("Escaneando controladores instalados...");
        var installedDrivers = await inventory.GetInstalledDriversAsync(ct);
        Console.WriteLine($"Se encontraron {installedDrivers.Count} controladores instalados.");

        Console.WriteLine();
        Console.WriteLine("Buscando actualizaciones disponibles mediante Windows Update...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var updates = new List<DriverUpdate>();
        await foreach (var update in updateSource.FindUpdatesAsync(installedDrivers, ct))
        {
            updates.Add(update);
        }

        stopwatch.Stop();

        if (updates.Count == 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ No se encontraron actualizaciones pendientes. Todos los controladores están al día.");
            Console.ResetColor();
            Console.WriteLine($"(tiempo transcurrido: {stopwatch.Elapsed.TotalSeconds:F2}s)");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{"ID Actualización",-38} {"Dispositivo",-30} {"Versión",-16} {"Tamaño",-10}");
        Console.WriteLine(new string('-', 98));

        foreach (var update in updates.OrderBy(u => u.DeviceName))
        {
            var id = Truncate(update.UpdateId, 36);
            var name = Truncate(update.DeviceName, 28);
            var ver = $"{Truncate(update.CurrentVersion, 7)} -> {Truncate(update.NewVersion, 7)}";
            var size = FormatSize(update.DownloadSizeBytes);

            Console.WriteLine($"{id,-38} {name,-30} {ver,-16} {size,-10}");
        }

        Console.WriteLine(new string('-', 98));
        Console.WriteLine($"Total: {updates.Count} actualización(es) disponible(s) (tiempo transcurrido: {stopwatch.Elapsed.TotalSeconds:F2}s)");
        Console.WriteLine("Para instalar, ejecute: dotnet run --project src/Cli -- install <UpdateId>");
    }

    private static async Task ExecuteLogAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var history = serviceProvider.GetRequiredService<IOperationHistory>();

        Console.WriteLine("Consultando historial de operaciones...");
        var entries = await history.GetAllAsync(ct);

        if (entries.Count == 0)
        {
            Console.WriteLine("No se han registrado operaciones en el historial.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{"ID",-6} {"Tipo",-10} {"Dispositivo",-30} {"Versión Previa",-15} {"Nueva",-15} {"Resultado",-10} {"Fecha UTC",-20}");
        Console.WriteLine(new string('-', 110));

        foreach (var entry in entries)
        {
            var res = entry.Success ? "Éxito" : "Fallo";
            var date = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            Console.WriteLine(
                $"{entry.Id,-6} {Truncate(entry.OperationType, 9),-10} {Truncate(entry.DeviceName, 28),-30} {Truncate(entry.PreviousVersion, 13),-15} {Truncate(entry.NewVersion, 13),-15} {res,-10} {date,-20}");

            if (!entry.Success && !string.IsNullOrEmpty(entry.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"       └ Error: {entry.ErrorMessage}");
                Console.ResetColor();
            }
        }

        Console.WriteLine(new string('-', 110));
        Console.WriteLine($"Total de operaciones registradas: {entries.Count}");
    }

    private static async Task ExecuteInstallAsync(IServiceProvider serviceProvider, string updateId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(updateId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: Debe especificar el ID de la actualización a instalar.");
            Console.ResetColor();
            return;
        }

        if (!IsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ADVERTENCIA: La instalación de controladores y creación de puntos de restauración");
            Console.WriteLine("requiere ejecutar la consola como Administrador o permisos de servicio SYSTEM.");
            Console.WriteLine();
            Console.ResetColor();
        }

        var inventory = serviceProvider.GetRequiredService<IDriverInventory>();
        var updateSource = serviceProvider.GetRequiredService<IUpdateSource>();
        var installer = serviceProvider.GetRequiredService<IDriverInstaller>();

        Console.WriteLine($"Buscando actualización con ID '{updateId}'...");
        var installedDrivers = await inventory.GetInstalledDriversAsync(ct);

        DriverUpdate? targetUpdate = null;
        await foreach (var update in updateSource.FindUpdatesAsync(installedDrivers, ct))
        {
            if (string.Equals(update.UpdateId, updateId, StringComparison.OrdinalIgnoreCase))
            {
                targetUpdate = update;
                break;
            }
        }

        if (targetUpdate == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No se encontró ninguna actualización disponible con ID '{updateId}'.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Iniciando instalación de: {targetUpdate.Title}");
        Console.WriteLine($"Dispositivo: {targetUpdate.DeviceName} ({targetUpdate.CurrentVersion} -> {targetUpdate.NewVersion})");
        Console.WriteLine();

        var progress = new Progress<InstallProgress>(p =>
        {
            Console.WriteLine($"[{p.PercentComplete,3}%] [{p.Phase,-20}] {p.StatusMessage}");
        });

        var result = await installer.InstallAsync(targetUpdate, progress, ct);

        Console.WriteLine();
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ ¡Controlador instalado exitosamente!");
            if (result.RebootRequired)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("! Se requiere reiniciar el equipo para completar la aplicación del controlador.");
            }
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Fallo en la instalación: {result.ErrorMessage}");
            Console.ResetColor();
        }
    }

    private static async Task ExecuteRollbackAsync(IServiceProvider serviceProvider, string operationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: Debe especificar el ID de operación a revertir.");
            Console.ResetColor();
            return;
        }

        if (!IsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ADVERTENCIA: La reversión de controladores requiere consola con privilegios de Administrador.");
            Console.WriteLine();
            Console.ResetColor();
        }

        var installer = serviceProvider.GetRequiredService<IDriverInstaller>();

        Console.WriteLine($"Iniciando rollback de operación #{operationId}...");
        var result = await installer.RollbackAsync(operationId, ct);

        Console.WriteLine();
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Rollback completado con éxito. Controlador revertido a versión {result.InstalledVersion}.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Fallo en la reversión: {result.ErrorMessage}");
            Console.ResetColor();
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "N/D";
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F1} MB";
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
