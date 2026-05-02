using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Behaviors;

namespace ERP.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR, pipeline de suscripción SaaS y handlers clásicos (scan).
    /// Los <see cref="MediatR.IRequestHandler{TRequest,TResponse}"/> los registra MediatR; no duplicarlos en el scan.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SubscriptionGateBehavior<,>));

        var handlerTypes = assembly
            .GetTypes()
            .Where(t => t.IsClass
                     && !t.IsAbstract
                     && t.Name.EndsWith("Handler")
                     && t.Namespace?.Contains("UseCases") == true
                     && !ImplementsIRequestHandlerMarker(t));

        foreach (var handlerType in handlerTypes)
            services.AddScoped(handlerType);

        return services;
    }

    private static bool ImplementsIRequestHandlerMarker(Type t) =>
        t.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));
}
