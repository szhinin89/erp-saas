using ERP.Application.Access;
using ERP.Application.Access.Authorization;
using ERP.Application.Access.Caching;
using ERP.Application.Behaviors;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddScoped<IEffectivePermissionKeysProvider, EffectivePermissionKeysProvider>();
        services.AddScoped<ICompanyContextProvider, CompanyContextProvider>();
        services.AddScoped<ICommunicationQueue, CommunicationQueue>();
        services.AddScoped<IRuntimePermissionAuthorizer, RuntimePermissionAuthorizer>();
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CompanyScopeBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BranchScopeBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        // Carga Inicial (INITIAL-LOAD-ARCH-01): un IImportProcessor por ImportType, resuelto
        // como diccionario. Agregar un import type nuevo es agregar una implementación aquí —
        // el motor genérico (entidades, casos de uso, controller) no cambia.
        services.AddScoped<IImportProcessor, CustomerImportProcessor>();
        services.AddScoped<IImportProcessor, SupplierImportProcessor>();
        services.AddScoped<IImportProcessor, ItemImportProcessor>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            sp.GetServices<IImportProcessor>().ToDictionary(p => p.ImportType, p => p)
        );

        var handlerTypes = assembly
            .GetTypes()
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && t.Name.EndsWith("Handler", StringComparison.Ordinal)
                && t.Namespace?.Contains("UseCases") == true
            );

        foreach (var handlerType in handlerTypes)
            services.AddScoped(handlerType);

        return services;
    }
}
