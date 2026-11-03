using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// SRI-TOGGLE-06/07 (ADR-023) — inventario cerrado de las rutas que llegan al SRI. Con
/// <c>electronic_documents.emission_enabled</c> en OFF NINGUNA llamada de la empresa sale al SRI (comprobantes
/// propios, documentos de proveedores y diagnóstico); todo consumidor que origina llamadas lo hace dentro de
/// <see cref="IElectronicEmissionSwitch.EnterSriCallAsync"/> y tras leer el interruptor. Un consumidor nuevo
/// rompe este test hasta que se decida y documente su control: no puede quedar una ruta desprotegida en silencio.
/// </summary>
public sealed class SriAccessEmissionSwitchBoundaryTests
{
    private static readonly System.Reflection.Assembly[] Assemblies =
    [
        typeof(IElectronicEmissionSwitch).Assembly,
        typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly,
        typeof(ERP.API.Controllers.RetentionsController).Assembly,
    ];

    /// <summary>Contratos que originan una llamada SOAP/HTTP al SRI.</summary>
    private static readonly Type[] SriContracts =
    [
        typeof(ISriReceptionClient),
        typeof(ISriAuthorizationClient),
        typeof(ISriDocumentStatusQuery),
        typeof(ISriConnectivityChecker),
        typeof(IElectronicDocumentReceptionService),
        typeof(IElectronicDocumentAuthorizationService),
        typeof(ISriReceptionXmlProvider),
    ];

    /// <summary>Consumidor → control frente al interruptor (o motivo por el que no aplica).</summary>
    private static readonly Dictionary<string, string> AllowedConsumers = new()
    {
        // Comprobantes propios.
        ["ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentIssuer"] =
            "ámbito SRI en pipeline/reintento/retransmisión + EmissionBlockedAsync antes de envío, consulta e historial R12",
        ["ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentReceptionService"] =
            "envoltorio por empresa; único consumidor: ElectronicDocumentIssuer",
        ["ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentAuthorizationService"] =
            "envoltorio por empresa; único consumidor: ElectronicDocumentIssuer",
        ["ERP.Application.Modules.Retentions.Services.RetentionAnnulmentService"] =
            "ámbito SRI + AllowsSriAccess antes de ConsultaComprobante (manual y job)",
        // Diagnóstico (SRI-TOGGLE-07).
        ["ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus.GetElectronicInvoicingStatusQueryHandler"] =
            "ámbito SRI; ping omitido con la emisión deshabilitada (estado Disabled)",
        ["ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration.ValidateSriConfigurationQueryHandler"] =
            "ámbito SRI; ping omitido con la emisión deshabilitada (conectividad 'no verificada')",
        // Documentos de proveedores (SRI-TOGGLE-07).
        ["ERP.Application.Modules.Purchases.UseCases.PurchaseCreditNoteReprocessingGuard"] =
            "ámbito SRI; deshabilitada = reprocesamiento bloqueado sin consultar (fail-closed)",
        ["ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml.DownloadPurchaseReceptionXmlHandler"] =
            "ámbito SRI; deshabilitada = ELECTRONIC_DOCUMENT_EMISSION_DISABLED sin consultar (documento sigue Importado)",
        // Adaptadores, decoradores de salud y registro DI (no originan llamadas por sí mismos).
        ["ERP.Infrastructure.Services.Sri.SriReceptionClient"] = "adaptador",
        ["ERP.Infrastructure.Services.Sri.SriAuthorizationClient"] = "adaptador",
        ["ERP.Infrastructure.Services.Sri.SriDocumentStatusQuery"] = "adaptador",
        ["ERP.Infrastructure.Services.Sri.SriConnectivityChecker"] = "adaptador",
        ["ERP.Infrastructure.Services.Sri.Health.ObservedSriReceptionClient"] = "decorador de salud",
        ["ERP.Infrastructure.Services.Sri.Health.ObservedSriAuthorizationClient"] = "decorador de salud",
        ["ERP.Infrastructure.Services.Sri.Health.ObservedSriConnectivityChecker"] = "decorador de salud",
        ["ERP.Infrastructure.Modules.Purchases.PurchaseReception.SriReceptionXmlProvider"] =
            "adaptador; único consumidor: DownloadPurchaseReceptionXmlHandler",
        ["ERP.Infrastructure.DependencyInjection"] = "registro DI",
    };

    [Fact]
    public void Todo_consumidor_de_un_cliente_SRI_esta_inventariado_frente_al_interruptor_de_emision()
    {
        var consumers = SriContracts
            .SelectMany(contract =>
                Types
                    .InAssemblies(Assemblies)
                    .That()
                    .HaveDependencyOn(contract.FullName!)
                    .GetTypes()
            )
            .Where(t => !t.IsInterface && !SriContracts.Contains(t))
            .Select(t => OutermostType(t).FullName!)
            .Where(name => !name.Contains('<'))
            .ToHashSet();

        consumers
            .Except(AllowedConsumers.Keys)
            .Should()
            .BeEmpty(
                "toda ruta nueva al SRI debe pasar por el interruptor de emisión (SRI-TOGGLE-06) o justificar aquí por qué no aplica"
            );
        AllowedConsumers
            .Keys.Except(consumers)
            .Should()
            .BeEmpty("el inventario no conserva consumidores que ya no existen");
    }

    /// <summary>
    /// SRI-TOGGLE-07 — todo consumidor de Application que origina llamadas (no los envoltorios por empresa, cuyo
    /// único consumidor es el issuer) depende del interruptor de emisión.
    /// </summary>
    [Fact]
    public void Todo_consumidor_de_Application_que_origina_llamadas_SRI_depende_del_interruptor()
    {
        string[] originators =
        [
            "ERP.Application.Modules.ElectronicDocuments.Services.ElectronicDocumentIssuer",
            "ERP.Application.Modules.Retentions.Services.RetentionAnnulmentService",
            "ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus.GetElectronicInvoicingStatusQueryHandler",
            "ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration.ValidateSriConfigurationQueryHandler",
            "ERP.Application.Modules.Purchases.UseCases.PurchaseCreditNoteReprocessingGuard",
            "ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml.DownloadPurchaseReceptionXmlHandler",
        ];
        var withSwitch = Types
            .InAssembly(typeof(IElectronicEmissionSwitch).Assembly)
            .That()
            .HaveDependencyOn(typeof(IElectronicEmissionSwitch).FullName!)
            .GetTypes()
            .Select(t => OutermostType(t).FullName!)
            .ToHashSet();

        originators.Except(withSwitch).Should().BeEmpty();
    }

    private static Type OutermostType(Type type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }
}
