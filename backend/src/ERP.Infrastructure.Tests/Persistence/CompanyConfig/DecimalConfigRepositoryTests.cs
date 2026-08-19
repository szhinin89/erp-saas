using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Persistence.Repositories.CompanyConfig;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Persistence.CompanyConfig;

/// <summary>
/// CONFIG-FOUNDATION-P1-01 — DecimalConfigRepository ahora resuelve/persiste decimales de
/// PRESENTACIÓN vía org_settings (scope Company, namespace presentation.decimal.*), reemplazando
/// el mecanismo paralelo GeneralParameter eliminado en esta entrega. No tiene ninguna relación
/// con FiscalPrecision (constante System, sin tocar).
/// </summary>
public sealed class DecimalConfigRepositoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IOrgSettingsRepository> OrgRepo { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            User.Setup(u => u.UserId).Returns(UserId);
            OrgRepo
                .Setup(r =>
                    r.GetAllForScopeAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<OrgSetting>());
        }

        public DecimalConfigRepository BuildRepo() =>
            new(OrgRepo.Object, User.Object, NullLogger<DecimalConfigRepository>.Instance);

        public void SetStoredSettings(params (string Key, string Value)[] values)
        {
            var settings = values
                .Select(v =>
                    OrgSetting.Create(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        v.Key,
                        v.Value,
                        SettingDataType.Int,
                        UserId
                    )
                )
                .ToList();
            OrgRepo
                .Setup(r =>
                    r.GetAllForScopeAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(settings.AsReadOnly());
        }
    }

    [Fact]
    public async Task Sin_org_settings_devuelve_defaults_de_presentacion()
    {
        var f = new Fixture();

        var cfg = await f.BuildRepo().GetAsync(TenantId, CompanyId, default);

        cfg.SalesUnitPrice.Should().Be(2);
        cfg.PurchaseUnitPrice.Should().Be(4);
        cfg.Quantity.Should().Be(4);
        cfg.Percentage.Should().Be(2);
        cfg.TotalAmount.Should().Be(2);
    }

    [Fact]
    public async Task Con_org_settings_validos_devuelve_los_valores_guardados()
    {
        var f = new Fixture();
        f.SetStoredSettings(
            (OrgSettingKeys.Presentation.DecimalSalesUnitPrice, "3"),
            (OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice, "5"),
            (OrgSettingKeys.Presentation.DecimalQuantity, "1"),
            (OrgSettingKeys.Presentation.DecimalPercentage, "0"),
            (OrgSettingKeys.Presentation.DecimalTotalAmount, "6")
        );

        var cfg = await f.BuildRepo().GetAsync(TenantId, CompanyId, default);

        cfg.SalesUnitPrice.Should().Be(3);
        cfg.PurchaseUnitPrice.Should().Be(5);
        cfg.Quantity.Should().Be(1);
        cfg.Percentage.Should().Be(0);
        cfg.TotalAmount.Should().Be(6);
    }

    [Fact]
    public async Task Valor_corrupto_existente_cae_a_default_seguro_sin_lanzar()
    {
        var f = new Fixture();
        f.SetStoredSettings((OrgSettingKeys.Presentation.DecimalQuantity, "no-es-un-entero"));

        var cfg = await f.BuildRepo().GetAsync(TenantId, CompanyId, default);

        cfg.Quantity.Should().Be(4); // default de Quantity — nunca tumba la lectura
    }

    [Fact]
    public async Task Valor_fuera_de_rango_existente_cae_a_default_seguro()
    {
        var f = new Fixture();
        f.SetStoredSettings((OrgSettingKeys.Presentation.DecimalPercentage, "99"));

        var cfg = await f.BuildRepo().GetAsync(TenantId, CompanyId, default);

        cfg.Percentage.Should().Be(2); // default de Percentage
    }

    [Fact]
    public async Task SaveAsync_escribe_en_scope_Company_con_las_5_keys_de_presentacion()
    {
        var f = new Fixture();
        var writtenKeys = new List<string>();
        f.OrgRepo
            .Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) =>
            {
                s.Scope.Should().Be(OrgScope.Company);
                s.ScopeId.Should().Be(CompanyId);
                s.TenantId.Should().Be(TenantId);
                writtenKeys.Add(s.Key);
            })
            .Returns(Task.CompletedTask);

        await f.BuildRepo().SaveAsync(TenantId, CompanyId, 2, 4, 4, 2, 2, default);

        writtenKeys
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    OrgSettingKeys.Presentation.DecimalSalesUnitPrice,
                    OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice,
                    OrgSettingKeys.Presentation.DecimalQuantity,
                    OrgSettingKeys.Presentation.DecimalPercentage,
                    OrgSettingKeys.Presentation.DecimalTotalAmount,
                }
            );
        f.OrgRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
