using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// CONFIG-FOUNDATION-P1-04 — CatalogConfigurationResolver reemplaza la lectura directa de
/// IOrgSettingsRepository que antes vivía en CategoryDepthResolver (CategoryNodeUseCases).
/// FallbackStrategy.SystemDefault: ausencia o valor inválido cae a 3 sin bloquear (no es un
/// setting crítico/fiscal).
/// </summary>
public sealed class CatalogConfigurationResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Mock<IOrgSettingsRepository> MockOrgRepo(OrgSetting? setting)
    {
        var mock = new Mock<IOrgSettingsRepository>();
        mock.Setup(r =>
                r.GetAsync(
                    TenantId,
                    CompanyId,
                    OrgScope.Company,
                    CompanyId,
                    OrgSettingKeys.Catalog.MaxCategoryDepth,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(setting);
        return mock;
    }

    private static OrgSetting Setting(string value) =>
        OrgSetting.Create(
            TenantId,
            CompanyId,
            OrgScope.Company,
            CompanyId,
            OrgSettingKeys.Catalog.MaxCategoryDepth,
            value,
            SettingDataType.Int,
            UserId
        );

    [Fact]
    public async Task Sin_setting_devuelve_default_3()
    {
        var resolver = new CatalogConfigurationResolver(MockOrgRepo(null).Object);

        var maxDepth = await resolver.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, default);

        maxDepth.Should().Be(3);
    }

    [Fact]
    public async Task Setting_valido_devuelve_el_valor_configurado()
    {
        var resolver = new CatalogConfigurationResolver(MockOrgRepo(Setting("5")).Object);

        var maxDepth = await resolver.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, default);

        maxDepth.Should().Be(5);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("no-es-un-entero")]
    public async Task Valor_invalido_o_no_positivo_cae_al_default_3_sin_lanzar(string invalidValue)
    {
        var resolver = new CatalogConfigurationResolver(MockOrgRepo(Setting(invalidValue)).Object);

        var maxDepth = await resolver.ResolveMaxCategoryDepthAsync(TenantId, CompanyId, default);

        maxDepth.Should().Be(3);
    }
}
