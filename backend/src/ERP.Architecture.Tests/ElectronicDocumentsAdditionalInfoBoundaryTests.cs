using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D2/D6/D7/D8) — fronteras de la composición de
/// <c>infoAdicional</c>. Ratchet con baseline = 0: ninguna excepción heredada.
/// <list type="bullet">
/// <item>Providers y builders no conocen <see cref="ISystemProviderSettingsRepository"/>.</item>
/// <item>El único consumidor fiscal de ese repositorio es el contributor del RUC Proveedor.</item>
/// <item>Los módulos de negocio no conocen el contributor ni el composer concretos.</item>
/// <item>Solo los orquestadores aprobados (y sus resolutores de wiring) dependen de los builders.</item>
/// <item>Los contributors viven en ElectronicDocuments.</item>
/// </list>
/// </summary>
public sealed class ElectronicDocumentsAdditionalInfoBoundaryTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(IElectronicDocumentAdditionalInfoComposer).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly;

    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(ERP.API.Controllers.SystemProviderSettingsController).Assembly;

    private static readonly string SystemProviderSettingsRepository =
        typeof(ISystemProviderSettingsRepository).FullName!;

    private static IEnumerable<Type> ApplicationTypes() =>
        ApplicationAssembly.GetTypes().Where(t => !IsCompilerGenerated(t));

    private static bool IsCompilerGenerated(Type type) =>
        type.Name.Contains('<') || (type.DeclaringType is not null && type.Name.Contains("d__"));

    private static IEnumerable<Type> Implementing(Type contract) =>
        ApplicationTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(t));

    private static IReadOnlyList<string> Failing(PredicateList predicates) =>
        predicates.GetTypes().Select(t => t.FullName!).Where(n => !n.Contains('<')).ToList();

    [Fact]
    public void Providers_y_builders_no_referencian_SystemProviderSettings()
    {
        var fiscalTypes = Implementing(typeof(IElectronicDocumentDataProvider))
            .Concat(Implementing(typeof(IRetentionElectronicDocumentDataProvider)))
            .Concat(Implementing(typeof(IElectronicDocumentXmlBuilder)))
            .Concat(Implementing(typeof(IRetentionXmlBuilder)))
            .Select(t => t.FullName!)
            .ToHashSet();

        fiscalTypes
            .Should()
            .Contain(
                [
                    "ERP.Application.Modules.Sales.Services.SalesInvoiceElectronicDocumentDataProvider",
                    "ERP.Application.Modules.Sales.Services.SalesReturnCreditNoteDataProvider",
                    "ERP.Application.Modules.Retentions.Services.RetentionElectronicDocumentDataProvider",
                    "ERP.Application.Modules.ElectronicDocuments.XmlBuilders.InvoiceXmlBuilder",
                    "ERP.Application.Modules.ElectronicDocuments.XmlBuilders.CreditNoteXmlBuilder",
                    "ERP.Application.Modules.ElectronicDocuments.XmlBuilders.RetentionXmlBuilder",
                ],
                "el test debe cubrir todos los providers y builders reales"
            );

        var offenders = Failing(
                Types
                    .InAssembly(ApplicationAssembly)
                    .That()
                    .HaveDependencyOn(SystemProviderSettingsRepository)
            )
            .Where(fiscalTypes.Contains);

        offenders
            .Should()
            .BeEmpty("ADR-038 D7: el RUC Proveedor lo aporta solo el contributor de infoAdicional");
    }

    [Fact]
    public void El_unico_consumidor_fiscal_de_SystemProviderSettings_es_el_contributor_del_RUC_Proveedor()
    {
        string[] allowed =
        [
            typeof(SystemProviderRucAdditionalInfoContributor).FullName!,
            // Configuración del singleton (AdminCore): leer/guardar, no emitir.
            "ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings.GetSystemProviderSettingsQueryHandler",
            "ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings.UpsertSystemProviderSettingsCommandHandler",
        ];

        var consumers = Failing(
            Types
                .InAssembly(ApplicationAssembly)
                .That()
                .HaveDependencyOn(SystemProviderSettingsRepository)
        );

        consumers.Except(allowed).Should().BeEmpty();
    }

    [Fact]
    public void Los_modulos_de_negocio_no_conocen_el_contributor_ni_el_composer_concretos()
    {
        string[] businessModules =
        [
            "ERP.Application.Modules.Sales",
            "ERP.Application.Modules.Retentions",
            "ERP.Application.Modules.Purchases",
            "ERP.Application.Modules.Expenses",
        ];

        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(businessModules[0])
            .Or()
            .ResideInNamespace(businessModules[1])
            .Or()
            .ResideInNamespace(businessModules[2])
            .Or()
            .ResideInNamespace(businessModules[3])
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(SystemProviderRucAdditionalInfoContributor).FullName!,
                typeof(ElectronicDocumentAdditionalInfoComposer).FullName!
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Solo_los_orquestadores_aprobados_dependen_de_los_builders()
    {
        string[] builderContracts =
        [
            typeof(IElectronicDocumentXmlBuilder).FullName!,
            typeof(IRetentionXmlBuilder).FullName!,
            typeof(InvoiceXmlBuilder).FullName!,
            typeof(CreditNoteXmlBuilder).FullName!,
            typeof(RetentionXmlBuilder).FullName!,
        ];
        string[] allowed =
        [
            // Orquestadores de ensamblado fiscal (ADR-038 D6): provider → composer → builder.
            typeof(CommercialElectronicDocumentXmlSupplier).FullName!,
            typeof(RetentionElectronicDocumentXmlService).FullName!,
            // Wiring: resuelven el builder por tipo y lo entregan al orquestador; no lo invocan.
            typeof(ElectronicDocumentXmlSupplierResolver).FullName!,
            typeof(ElectronicDocumentXmlBuilderResolver).FullName!,
            typeof(IElectronicDocumentXmlBuilderResolver).FullName!,
            // Los propios builders y sus contratos.
            .. builderContracts,
            // Registro DI.
            "ERP.Application.DependencyInjection",
            "ERP.Infrastructure.DependencyInjection",
        ];

        var dependents = new[] { ApplicationAssembly, InfrastructureAssembly, ApiAssembly }
            .SelectMany(assembly =>
                Failing(Types.InAssembly(assembly).That().HaveDependencyOnAny(builderContracts))
            )
            .Distinct();

        dependents
            .Except(allowed)
            .Should()
            .BeEmpty(
                "ADR-038 D6: un modelo fiscal llega a un builder solo a través de un orquestador que compone infoAdicional"
            );
    }

    [Fact]
    public void Los_contributors_de_infoAdicional_viven_en_ElectronicDocuments()
    {
        var contributors = Implementing(typeof(IElectronicDocumentAdditionalInfoContributor))
            .ToList();

        contributors.Should().Contain(typeof(SystemProviderRucAdditionalInfoContributor));
        contributors
            .Where(t =>
                !t.Namespace!.StartsWith(
                    "ERP.Application.Modules.ElectronicDocuments",
                    StringComparison.Ordinal
                )
            )
            .Select(t => t.FullName)
            .Should()
            .BeEmpty();
        Implementing(typeof(IElectronicDocumentAdditionalInfoComposer))
            .Should()
            .Equal(
                [typeof(ElectronicDocumentAdditionalInfoComposer)],
                "ADR-038 D6: un solo composer"
            );
    }
}
