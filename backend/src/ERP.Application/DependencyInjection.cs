using ERP.Application.Access;
using ERP.Application.Access.Authorization;
using ERP.Application.Access.Caching;
using ERP.Application.Behaviors;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Retentions.Services;
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
        services.AddScoped<IExternalEntitlementService, NoOpExternalEntitlementService>();
        // RETENTIONS-ELIGIBILITY-01 — solo orquesta repos ya registrados (Company, BusinessPartnerRole,
        // IRetentionCodeResolver), sin EF directo, por eso vive/registra en Application, no Infrastructure.
        services.AddScoped<IRetentionEligibilityService, RetentionEligibilityService>();
        // RETENTIONS-EXPENSES-INTEGRATION-01D-1 — operación interna reutilizable de emisión de
        // RetentionDocument, consumida por IssueRetentionHandler (emisión aislada) y por
        // ConfirmExpenseDocumentHandler/CreateConfirmedExpenseHandler (emisión transaccional).
        services.AddScoped<IRetentionIssuer, RetentionIssuer>();
        // RETENTIONS-EXPENSES-INTEGRATION-01D-3 — operación interna reutilizable de anulación de
        // RetentionDocument (+ reversa de AP si corresponde), consumida por CancelRetentionHandler
        // (anulación aislada) y por CancelExpenseDocumentHandler (anulación transaccional).
        services.AddScoped<IRetentionCanceller, RetentionCanceller>();
        // RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A — construye el modelo canónico
        // RetentionElectronicDocumentData desde un RetentionDocument ya Issued. Deliberadamente
        // NO forma parte del registro genérico de IElectronicDocumentDataProvider (motor de
        // ElectronicDocuments) en esta fase — ver comentario de tipo del provider.
        services.AddScoped<
            IRetentionElectronicDocumentDataProvider,
            RetentionElectronicDocumentDataProvider
        >();
        // RETENTIONS-SRI-XML-MAPPER-03B — construye el XML desde RetentionElectronicDocumentData.
        // Registrado con su propio contrato (IRetentionXmlBuilder), deliberadamente NO agregado a
        // IElectronicDocumentXmlBuilderResolver (motor genérico) — el wiring final queda pendiente.
        services.AddScoped<IRetentionXmlBuilder, RetentionXmlBuilder>();
        // RETENTIONS-ELECTRONIC-WIRING-03E — orquesta IRetentionElectronicDocumentDataProvider +
        // IRetentionXmlBuilder (ambos ya registrados arriba) en un único punto de entrada. Pipeline
        // paralelo pequeño y explícito para Retención — deliberadamente NO se registra en ningún
        // resolver genérico de ElectronicDocuments.
        services.AddScoped<
            IRetentionElectronicDocumentXmlService,
            RetentionElectronicDocumentXmlService
        >();
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
        services.AddScoped<IImportProcessor, InitialStockImportProcessor>();
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
