using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Behaviors;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Application.Common.Inventory;
using ERP.Application.Modules.Inventory.Services;
using ERP.Application.Modules.Cash.Services;

namespace ERP.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR, pipeline de suscripciÃ³n SaaS y handlers clÃ¡sicos (scan).
    /// Los <see cref="MediatR.IRequestHandler{TRequest,TResponse}"/> los registra MediatR; no duplicarlos en el scan.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddScoped<ISessionModulesResolver, SessionModulesResolver>();
        services.AddScoped<IKardexService, KardexService>();
        services.AddScoped<IInventoryPostingService, InventoryPostingService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BillingGateBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SubscriptionGateBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CompanyScopeBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

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
