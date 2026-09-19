using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-COMPANY-SETTING-05 — <c>OrgSettingKeys.Cash.
/// AllowManualInOutMovements</c> es el SSOT reutilizado (scope Tenant+Company, ya existente desde
/// CONFIG-DYNAMIC-OPERATIONS-01/02, sin consumidor hasta ahora). Estas pruebas cubren el
/// aislamiento multi-tenant/multi-company del resolver para esta key específica — no repiten la
/// cobertura genérica de <c>IOrgSettingsRepository</c>, solo documentan que un valor de una
/// empresa/tenant nunca contamina a otra.
/// </summary>
public sealed class OperationalPreferencesResolverCashTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static OrgSetting Setting(Guid tenantId, Guid companyId, bool value) =>
        OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Cash.AllowManualInOutMovements,
            value.ToString(),
            SettingDataType.Bool,
            UserId
        );

    private static OperationalPreferencesResolver BuildResolver(Mock<IOrgSettingsRepository> orgRepo) =>
        new(
            orgRepo.Object,
            Mock.Of<ICurrentTenant>(),
            Mock.Of<ICurrentCompany>(),
            NullLogger<OperationalPreferencesResolver>.Instance
        );

    [Fact]
    public async Task Empresa_A_habilitado_y_empresa_B_deshabilitado_no_se_contaminan_entre_si()
    {
        var orgRepo = new Mock<IOrgSettingsRepository>();
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyA, OrgScope.Company, CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantA, CompanyA, true) });
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyB, OrgScope.Company, CompanyB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantA, CompanyB, false) });
        var resolver = BuildResolver(orgRepo);

        var prefsA = await resolver.ResolveAsync(TenantA, CompanyA, CancellationToken.None);
        var prefsB = await resolver.ResolveAsync(TenantA, CompanyB, CancellationToken.None);

        prefsA.Cash.AllowManualInOutMovements.Should().BeTrue();
        prefsB.Cash.AllowManualInOutMovements.Should().BeFalse();
    }

    [Fact]
    public async Task Un_tenant_no_ve_el_valor_de_otro_tenant_aunque_use_el_mismo_CompanyId()
    {
        // Escenario defensivo: aunque dos tenants nunca deberían compartir CompanyId en la
        // práctica, el resolver debe consultar SIEMPRE por (TenantId, CompanyId) — nunca solo por
        // CompanyId — así que dos combinaciones de Tenant distintas jamás se resuelven con el
        // mismo mock/valor por accidente.
        var orgRepo = new Mock<IOrgSettingsRepository>();
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyA, OrgScope.Company, CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantA, CompanyA, false) });
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantB, CompanyA, OrgScope.Company, CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantB, CompanyA, true) });
        var resolver = BuildResolver(orgRepo);

        var prefsTenantA = await resolver.ResolveAsync(TenantA, CompanyA, CancellationToken.None);
        var prefsTenantB = await resolver.ResolveAsync(TenantB, CompanyA, CancellationToken.None);

        prefsTenantA.Cash.AllowManualInOutMovements.Should().BeFalse();
        prefsTenantB.Cash.AllowManualInOutMovements.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_setting_persistido_el_default_es_true_preserva_empresas_existentes()
    {
        var orgRepo = new Mock<IOrgSettingsRepository>();
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyA, OrgScope.Company, CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrgSetting>());
        var resolver = BuildResolver(orgRepo);

        var prefs = await resolver.ResolveAsync(TenantA, CompanyA, CancellationToken.None);

        prefs.Cash.AllowManualInOutMovements.Should().BeTrue();
    }

    [Fact]
    public async Task Cambiar_de_empresa_usa_el_valor_propio_de_cada_una_en_llamadas_consecutivas()
    {
        var orgRepo = new Mock<IOrgSettingsRepository>();
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyA, OrgScope.Company, CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantA, CompanyA, true) });
        orgRepo
            .Setup(r => r.GetAllForScopeAsync(TenantA, CompanyB, OrgScope.Company, CompanyB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Setting(TenantA, CompanyB, false) });
        var resolver = BuildResolver(orgRepo);

        // Simula un usuario cambiando de empresa activa dentro de la misma sesión de tenant.
        (await resolver.ResolveAsync(TenantA, CompanyA, CancellationToken.None)).Cash.AllowManualInOutMovements
            .Should().BeTrue();
        (await resolver.ResolveAsync(TenantA, CompanyB, CancellationToken.None)).Cash.AllowManualInOutMovements
            .Should().BeFalse();
        (await resolver.ResolveAsync(TenantA, CompanyA, CancellationToken.None)).Cash.AllowManualInOutMovements
            .Should().BeTrue();
    }
}
