using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra todos los handlers de la capa Application mediante assembly scan.
    /// Cualquier clase cuyo nombre termine en "Handler" queda disponible en el contenedor
    /// sin necesidad de registrarla manualmente en Program.cs.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Handler"));

        foreach (var handlerType in handlerTypes)
            services.AddScoped(handlerType);

        return services;
    }
}
