using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra todos los handlers de la capa Application mediante assembly scan.
    /// Filtra por namespace "UseCases" para evitar registrar accidentalmente
    /// otras clases que terminen en "Handler" fuera de los casos de uso.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly
            .GetTypes()
            .Where(t => t.IsClass
                     && !t.IsAbstract
                     && t.Name.EndsWith("Handler")
                     && t.Namespace?.Contains("UseCases") == true);

        foreach (var handlerType in handlerTypes)
            services.AddScoped(handlerType);

        return services;
    }
}
