using ERP.Application.Common;
using ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;
using ERP.Application.Modules.Media;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

public sealed class UpdateCompanyForAdminCoreTests
{
    private readonly Mock<ICompanyRepository> companies = new();
    private readonly Mock<ICurrentUser> user = new();
    private readonly Mock<ICurrentTenant> tenant = new();
    private readonly Company entity = Company.CreateManaged(Guid.NewGuid(), "TMP-EC-ceb18408", "Original", isTemporaryTaxIdentification: true);

    public UpdateCompanyForAdminCoreTests()
    {
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.Role).Returns("Admin");
        companies.Setup(x => x.GetTrackedByIdForAdminCoreAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
    }

    private Task<Result<CompanyDetailDto>> Update(string? ruc) =>
        new UpdateCompanyForAdminCoreHandler(companies.Object, user.Object, tenant.Object).Handle(
            new(entity.Id, "Nueva razon", "Comercial", false, ruc), CancellationToken.None);

    [Fact]
    public async Task Global_replaces_temporary_RUC_and_profile_uses_same_entity()
    {
        var id = entity.Id;
        var result = await Update("1790016919001");
        result.IsSuccess.Should().BeTrue();
        entity.Id.Should().Be(id);
        entity.IsTemporaryTaxIdentification.Should().BeFalse();
        entity.TaxIdentificationStatus.Should().Be(TaxIdentificationStatus.Verified);
        var guard = new Mock<ICompanyAccessGuard>();
        guard.Setup(x => x.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(
                new CompanyAccessContext(Guid.NewGuid(), entity.TenantId, entity.Id, "Admin", true, true)));
        companies.Setup(x => x.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var profileResult = await new GetCompanyProfileHandler(guard.Object, companies.Object, new Mock<IMediaService>().Object)
            .Handle(new GetCompanyProfileQuery(), CancellationToken.None);
        profileResult.IsSuccess.Should().BeTrue();
        var profile = profileResult.Value!;
        profile.TaxIdentificationNumber.Should().Be("1790016919001");
        profile.LegalName.Should().Be("Nueva razon");
        entity.TradeName.Should().Be("Comercial");
        entity.IsActive.Should().BeFalse();
        companies.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        companies.Verify(x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("1790016918001")]
    [InlineData("TMP-EC-other")]
    public async Task Invalid_RUC_does_not_mutate_or_save(string ruc)
    {
        (await Update(ruc)).IsSuccess.Should().BeFalse();
        entity.LegalName.Should().Be("Original");
        entity.TaxIdentificationNumber.Should().Be("TMP-EC-ceb18408");
        companies.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Duplicate_in_other_tenant_is_rejected()
    {
        companies.Setup(x => x.GetByTaxIdentificationNumberAsync("1790016919001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.CreateManaged(Guid.NewGuid(), "1790016919001", "Otra"));
        var result = await Update("1790016919001");
        result.IsSuccess.Should().BeFalse();
        entity.TaxIdentificationNumber.Should().Be("TMP-EC-ceb18408");
        companies.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, "Admin", true)]
    [InlineData(true, "User", false)]
    [InlineData(false, "Admin", false)]
    public async Task Non_global_sessions_cannot_read_or_edit(bool authenticated, string role, bool hasTenant)
    {
        user.SetupGet(x => x.IsAuthenticated).Returns(authenticated);
        user.SetupGet(x => x.Role).Returns(role);
        tenant.SetupGet(x => x.TenantId).Returns(hasTenant ? Guid.NewGuid() : Guid.Empty);
        (await Update("1790016919001")).IsSuccess.Should().BeFalse();
        companies.Verify(x => x.GetTrackedByIdForAdminCoreAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_RUC_preserves_pending_identity_while_editing_names()
    {
        (await Update(null)).IsSuccess.Should().BeTrue();
        entity.TaxIdentificationNumber.Should().Be("TMP-EC-ceb18408");
        entity.IsTemporaryTaxIdentification.Should().BeTrue();
    }
}
