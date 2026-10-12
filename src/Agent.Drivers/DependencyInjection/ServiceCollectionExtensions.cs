using Agent.Drivers.Inventory;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Drivers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de controladores y acceso al sistema en el contenedor de inyección de dependencias.
    /// </summary>
    public static IServiceCollection AddDriverServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDriverInventory, WmiDriverInventory>();

        return services;
    }
}
