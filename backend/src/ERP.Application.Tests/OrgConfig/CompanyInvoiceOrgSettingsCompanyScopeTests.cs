using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.OrgConfig;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — GetCompanyInvoiceOrgSettingsQueryHandler y
/// UpsertCompanyInvoiceOrgSettingsCommandHandler no reciben ningún CompanyId externo: ambos
/// resuelven exclusivamente vía ICurrentTenant/ICurrentCompany (contexto autenticado). Estos
/// tests prueban que, con org_settings seedeados para dos empresas distintas, cada handler solo
/// lee/escribe el scope (tenantId, companyId) de la empresa activa — nunca el de otra empresa,
/// aunque el repo fake tenga ambas.
/// </summary>
public sealed class CompanyInvoiceOrgSettingsCompanyScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActiveCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GetCompanyInvoiceOrgSettings_solo_lee_el_scope_de_la_empresa_activa()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(ActiveCompanyId);

        repo.Setup(r =>
                r.GetAllForScopeAsync(
                    TenantId,
                    ActiveCompanyId,
                    OrgScope.Company,
                    ActiveCompanyId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<OrgSetting>());
        // Otra empresa tiene un DefaultDocTypeCode configurado — nunca debe filtrarse.
        repo.Setup(r =>
                r.GetAllForScopeAsync(
                    TenantId,
                    OtherCompanyId,
                    OrgScope.Company,
                    OtherCompanyId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[]
                {
                    OrgSetting.Create(
                        TenantId,
                        OtherCompanyId,
                        OrgScope.Company,
                        OtherCompanyId,
                        OrgSettingKeys.Invoice.DefaultDocTypeCode,
                        "01-OTHER",
                        SettingDataType.String,
                        UserId
                    ),
                }
            );

        var handler = new GetCompanyInvoiceOrgSettingsQueryHandler(
            repo.Object,
            tenant.Object,
            company.Object
        );

        var result = await handler.Handle(
            new GetCompanyInvoiceOrgSettingsQuery(),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultDocTypeCode.Should().BeNull();
        result.Value.DefaultDocTypeCode.Should().NotBe("01-OTHER");
    }

    [Fact]
    public async Task UpsertCompanyInvoiceOrgSettings_escribe_siempre_con_el_CompanyId_activo()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(ActiveCompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        var writtenScopeIds = new List<Guid>();
        repo.Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) =>
            {
                s.TenantId.Should().Be(TenantId);
                s.CompanyId.Should().Be(ActiveCompanyId);
                writtenScopeIds.Add(s.ScopeId);
            })
            .Returns(Task.CompletedTask);

        var handler = new UpsertCompanyInvoiceOrgSettingsCommandHandler(
            repo.Object,
            tenant.Object,
            company.Object,
            user.Object
        );

        var result = await handler.Handle(
            new UpsertCompanyInvoiceOrgSettingsCommand("01", "20", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        writtenScopeIds.Should().OnlyContain(id => id == ActiveCompanyId);
        writtenScopeIds.Should().NotContain(OtherCompanyId);
    }
}
