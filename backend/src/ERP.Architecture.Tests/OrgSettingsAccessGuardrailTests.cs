using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// CONFIG-FOUNDATION-P1-04: cierra la regla arquitectónica "ningún handler/use case operativo
/// debe leer IOrgSettingsRepository directamente para decidir comportamiento de negocio". Este
/// test falla si cualquier tipo nuevo en ERP.Application referencia IOrgSettingsRepository fuera
/// del allowlist explícito — evita que la deuda vuelva a crecer sin que nadie se dé cuenta.
///
/// Allowlist (todos documentados en docs/architecture/configuration-engine-target-architecture.md
/// como escritura protegida por ConfigurationDefinitionCatalog o lectura administrativa de UI de
/// settings, nunca lectura operativa de negocio):
/// - UpsertBranchInvoiceOrgSettingsCommandHandler / GetBranchInvoiceOrgSettingsQueryHandler
/// - UpsertCompanyInvoiceOrgSettingsCommandHandler / GetCompanyInvoiceOrgSettingsQueryHandler
/// - UpdateConsumerFinalMaxAmountCommandHandler (escribe sales.consumer_final.max_amount)
/// - UpdateCompanyBrandingHandler (escribe company.branding.*)
///
/// Todo lo demás que necesite leer configuración operativa debe depender de un resolver tipado
/// (IInvoiceDefaultsResolver, ICompanyBrandingResolver, ICatalogConfigurationResolver,
/// ISalesFiscalPolicyResolver, IOrgConfigResolver) — nunca de IOrgSettingsRepository.
/// </summary>
public sealed class OrgSettingsAccessGuardrailTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    private static readonly string[] Allowlist =
    [
        "ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings.UpsertBranchInvoiceOrgSettingsCommandHandler",
        "ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings.GetBranchInvoiceOrgSettingsQueryHandler",
        "ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings.UpsertCompanyInvoiceOrgSettingsCommandHandler",
        "ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings.GetCompanyInvoiceOrgSettingsQueryHandler",
        "ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount.UpdateConsumerFinalMaxAmountCommandHandler",
        "ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding.UpdateCompanyBrandingHandler",
    ];

    [Fact]
    public void Application_types_depending_on_IOrgSettingsRepository_are_limited_to_the_allowlist()
    {
        var offenders = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveDependencyOn("ERP.Domain.Configuration.Interfaces.IOrgSettingsRepository")
            .GetTypes()
            .Select(t => t.FullName!)
            .Except(Allowlist)
            .ToList();

        offenders
            .Should()
            .BeEmpty(
                "todo handler operativo debe depender de un resolver tipado, no de IOrgSettingsRepository directamente — "
                    + $"encontrado(s) fuera del allowlist: {string.Join(", ", offenders)}"
            );
    }

    [Fact]
    public void Allowlist_entries_still_exist_and_still_reference_IOrgSettingsRepository()
    {
        // Si un handler del allowlist se elimina/renombra sin actualizar este archivo, este test
        // lo detecta (en vez de que el allowlist quede sobredimensionado en silencio).
        var actualDependents = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveDependencyOn("ERP.Domain.Configuration.Interfaces.IOrgSettingsRepository")
            .GetTypes()
            .Select(t => t.FullName!)
            .ToList();

        foreach (var allowed in Allowlist)
            actualDependents.Should().Contain(allowed, $"{allowed} ya no depende de IOrgSettingsRepository — quitarlo del allowlist");
    }
}
