using Agent.Drivers.History;
using Agent.Drivers.Install;
using Agent.Drivers.Inventory;
using Agent.Drivers.Restore;
using Agent.Drivers.Security;
using Agent.Drivers.Updates;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de controladores, firmas, puntos de restauración,
    /// persistencia e instalación en el contenedor de inyección de dependencias.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="databasePath">
    /// Ruta personalizada a la base de datos de historial SQLite.
    /// Si es null, se utiliza <c>%ProgramData%\DriverUpdater\history.db</c>.
    /// </param>
    public static IServiceCollection AddDriverServices(
        this IServiceCollection services,
        string? databasePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDriverInventory, WmiDriverInventory>();
        services.AddSingleton<IMotherboardInfoProvider, WmiMotherboardInfoProvider>();
        services.AddSingleton<IUpdateSource, WuaUpdateSource>();
        services.AddSingleton<ISignatureVerifier, WinTrustSignatureVerifier>();
        services.AddSingleton<IRestorePointService, WmiRestorePointService>();
        services.AddSingleton<IOperationHistory>(sp =>
        {
            var path = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DriverUpdater",
                "history.db");
            return new SqliteOperationHistory(path, sp.GetRequiredService<ILogger<SqliteOperationHistory>>());
        });
        services.AddSingleton<IDriverInstaller, WuaDriverInstaller>();

        return services;
    }
}
