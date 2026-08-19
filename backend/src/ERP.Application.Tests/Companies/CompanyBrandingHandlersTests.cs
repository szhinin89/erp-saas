using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.GetCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// CONFIG-FOUNDATION-P1-02 — GetCompanyBrandingQueryHandler lee vía ICompanyBrandingResolver
/// (nunca org_settings directamente); UpdateCompanyBrandingHandler escribe directamente en
/// org_settings (ya no toca la entidad Company, que perdió BrandingConfiguration).
/// </summary>
public sealed class CompanyBrandingHandlersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CompanyAccessContext AccessContext() =>
        new(UserId, TenantId, CompanyId, "Owner", true, true);

    [Fact]
    public async Task GetCompanyBranding_delega_en_ICompanyBrandingResolver()
    {
        var accessGuard = new Mock<ICompanyAccessGuard>();
        accessGuard
            .Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(AccessContext()));
        var resolver = new Mock<ICompanyBrandingResolver>();
        resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CompanyBrandingSettings(
                    LogoStoragePath: "company/logo.png",
                    PrimaryColor: "#112233",
                    SecondaryColor: "#445566",
                    Slogan: "Confianza y calidad",
                    DocumentFooterText: "Gracias por su compra"
                )
            );
        var handler = new GetCompanyBrandingQueryHandler(accessGuard.Object, resolver.Object);

        var result = await handler.Handle(new GetCompanyBrandingQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PrimaryColor.Should().Be("#112233");
        result.Value.SecondaryColor.Should().Be("#445566");
        result.Value.Slogan.Should().Be("Confianza y calidad");
        result.Value.DocumentFooterText.Should().Be("Gracias por su compra");
    }

    [Fact]
    public async Task UpdateCompanyBranding_escribe_las_4_keys_en_scope_Company_via_org_settings()
    {
        var accessGuard = new Mock<ICompanyAccessGuard>();
        accessGuard
            .Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(AccessContext()));
        var orgRepo = new Mock<IOrgSettingsRepository>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(UserId);

        var writtenKeys = new List<string>();
        orgRepo
            .Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) =>
            {
                s.Scope.Should().Be(OrgScope.Company);
                s.ScopeId.Should().Be(CompanyId);
                s.TenantId.Should().Be(TenantId);
                writtenKeys.Add(s.Key);
            })
            .Returns(Task.CompletedTask);

        var handler = new UpdateCompanyBrandingHandler(
            accessGuard.Object,
            orgRepo.Object,
            currentUser.Object
        );

        var result = await handler.Handle(
            new UpdateCompanyBrandingCommand("#112233", "#445566", "Confianza", "Gracias"),
            default
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.PrimaryColor.Should().Be("#112233");
        writtenKeys
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    OrgSettingKeys.CompanyBranding.PrimaryColor,
                    OrgSettingKeys.CompanyBranding.SecondaryColor,
                    OrgSettingKeys.CompanyBranding.Slogan,
                    OrgSettingKeys.CompanyBranding.DocumentFooterText,
                }
            );
        orgRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
