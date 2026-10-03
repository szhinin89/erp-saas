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
        services.AddScoped<ERP.Application.Modules.Communications.Templates.ICommunicationTemplateResolver, ERP.Application.Modules.Communications.Templates.CommunicationTemplateResolver>();
        services.AddScoped<IRuntimePermissionAuthorizer, RuntimePermissionAuthorizer>();
        services.AddScoped<IExternalEntitlementService, NoOpExternalEntitlementService>();
        // RETENTIONS-ELIGIBILITY-01 — solo orquesta repos ya registrados (Company, BusinessPartnerRole,
        // IRetentionCodeResolver), sin EF directo, por eso vive/registra en Application, no Infrastructure.
        services.AddScoped<IRetentionEligibilityService, RetentionEligibilityService>();
        // RETENTIONS-EXPENSES-INTEGRATION-01D-1 — operación interna reutilizable de emisión de
        // RetentionDocument, consumida solo dentro de la confirmación transaccional del documento
        // origen: ConfirmExpenseDocumentHandler/CreateConfirmedExpenseHandler y ConfirmPurchaseHandler
        // (ZH-PURCHASE-RETENTION-CONFIRM-01).
        services.AddScoped<IRetentionIssuer, RetentionIssuer>();
        services.AddScoped<
            ERP.Application.Modules.Payables.Services.ISupplierPaymentRegistrar,
            ERP.Application.Modules.Payables.Services.SupplierPaymentRegistrar
        >();
        // RETENTIONS-EXPENSES-INTEGRATION-01D-3 — operación interna reutilizable de anulación de
        // RetentionDocument (+ reversa de AP si corresponde), consumida SOLO por la anulación del
        // documento origen: CancelExpenseDocumentHandler y CancelPurchaseHandler
        // (ZH-RETENTION-CANCELLATION-LIFECYCLE-01 — no existe anulación aislada de la retención).
        services.AddScoped<IRetentionCanceller, RetentionCanceller>();
        // ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036) — gate de ciclo de vida del origen
        // "Retentions" (solo Issued procesa), única entrada de transmisión electrónica (confirmación,
        // recuperación y acción manual) y autorización derivada del documento origen.
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentSourceLifecycleGuard,
            RetentionElectronicSourceLifecycleGuard
        >();
        services.AddScoped<IRetentionElectronicTransmission, RetentionElectronicTransmission>();
        services.AddScoped<IRetentionSourceAccess, RetentionSourceAccess>();
        // ZH-RETENTION-SRI-ANNULMENT-01/01B — anulación ante el SRI de retenciones autorizadas (solicitud
        // asistida; verificación automática vía ISriDocumentStatusQuery / ConsultaComprobante, registrado en
        // Infrastructure). La finalización reutiliza el flujo oficial de anulación de
        // cada origen vía IRetentionOriginCancellation (los handlers se resuelven por tipo concreto).
        services.AddScoped<IRetentionAnnulmentRequester, RetentionAnnulmentRequester>();
        services.AddScoped<IRetentionAnnulmentService, RetentionAnnulmentService>();
        services.AddScoped<ERP.Application.Modules.Retentions.UseCases.RetentionAnnulmentAccess>();
        services.AddScoped<
            ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentSriAnnulment,
            ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentSriAnnulment
        >();
        services.AddTransient<ERP.Application.Modules.Purchases.UseCases.CancelPurchaseHandler>();
        services.AddTransient<ERP.Application.Modules.Expenses.UseCases.Documents.CancelExpenseDocumentHandler>();
        services.AddScoped<
            IRetentionOriginCancellation,
            ERP.Application.Modules.Purchases.Services.PurchaseRetentionOriginCancellation
        >();
        services.AddScoped<
            IRetentionOriginCancellation,
            ERP.Application.Modules.Expenses.Services.ExpenseRetentionOriginCancellation
        >();
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
        // ZH-DOMAIN-RULE-ERROR-SSOT-01: antes de Caching (más externo) para que un fallo por regla
        // de negocio nunca se guarde en caché. Solo se aplica a respuestas IDomainRuleResult (Result<T>).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
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
