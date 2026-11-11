using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;
using ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;
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
    private readonly Company entity = Company.CreateManaged(
        Guid.NewGuid(),
        "TMP-EC-ceb18408",
        "Original",
        isTemporaryTaxIdentification: true
    );

    public UpdateCompanyForAdminCoreTests()
    {
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.Role).Returns("Admin");
        companies
            .Setup(x => x.GetTrackedByIdForAdminCoreAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
    }

    private Task<Result<CompanyDetailDto>> Update(string? ruc) =>
        new UpdateCompanyForAdminCoreHandler(companies.Object, user.Object, tenant.Object).Handle(
            new(entity.Id, "Nueva razon", "Comercial", false, ruc),
            CancellationToken.None
        );

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
        guard
            .Setup(x => x.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(
                        Guid.NewGuid(),
                        entity.TenantId,
                        entity.Id,
                        "Admin",
                        true,
                        true
                    )
                )
            );
        companies
            .Setup(x => x.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var profileResult = await new GetCompanyProfileHandler(
            guard.Object,
            companies.Object,
            new Mock<IMediaService>().Object
        ).Handle(new GetCompanyProfileQuery(), CancellationToken.None);
        profileResult.IsSuccess.Should().BeTrue();
        var profile = profileResult.Value!;
        profile.TaxIdentificationNumber.Should().Be("1790016919001");
        profile.LegalName.Should().Be("Nueva razon");
        entity.TradeName.Should().Be("Comercial");
        entity.IsActive.Should().BeFalse();
        companies.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        companies.Verify(
            x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// ZH-COMPANY-IDENTITY-SSOT-01 — el formato de la identidad completa (RUC + nombres) lo valida el
    /// pipeline con <c>CompanyIdentityRules</c>. El rechazo HTTP (422 sin mutar) está cubierto en
    /// CompanyIdentityUpdateHttpTests.
    /// </summary>
    [Theory]
    [InlineData("1234567890123", "Nueva razon")]
    [InlineData("1760000070000", "Nueva razon")] // entidad pública con establecimiento 0000 (RucValidatorTests)
    [InlineData("TMP-EC-other", "Nueva razon")]
    [InlineData("1790016919001", "   ")]
    public void Invalid_identity_is_rejected(string ruc, string legalName)
    {
        var result = new UpdateCompanyForAdminCoreCommandValidator().Validate(
            new UpdateCompanyForAdminCoreCommand(entity.Id, legalName, "Comercial", true, ruc)
        );

        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Razón social y nombre comercial tienen una sola regla (<c>CompanyNamesRules</c>): el comando de
    /// Admin Global y el perfil de la empresa activa rechazan los mismos valores con la misma
    /// propiedad y el mismo mensaje.
    /// </summary>
    [Theory]
    [InlineData("   ", "Comercial")]
    [InlineData("", null)]
    public void Invalid_names_are_rejected_with_the_same_rule_from_both_contexts(
        string legalName,
        string? tradeName
    )
    {
        var global = new UpdateCompanyForAdminCoreCommandValidator().Validate(
            new UpdateCompanyForAdminCoreCommand(entity.Id, legalName, tradeName, true, null)
        );
        var profile = new UpdateCompanyProfileCommandValidator().Validate(
            new UpdateCompanyProfileCommand(
                legalName,
                tradeName,
                null,
                null,
                null,
                "USD",
                "America/Guayaquil",
                null,
                null,
                null,
                null,
                null
            )
        );

        global.IsValid.Should().BeFalse();
        profile
            .Errors.Select(e => (e.PropertyName, e.ErrorMessage))
            .Should()
            .BeEquivalentTo(global.Errors.Select(e => (e.PropertyName, e.ErrorMessage)));
    }

    [Fact]
    public async Task Missing_company_is_NOT_FOUND()
    {
        companies
            .Setup(x =>
                x.GetTrackedByIdForAdminCoreAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Company?)null);

        var result = await Update("1790016919001");

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        companies.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Duplicate_in_other_tenant_is_rejected()
    {
        companies
            .Setup(x =>
                x.GetByTaxIdentificationNumberAsync("1790016919001", It.IsAny<CancellationToken>())
            )
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
    public async Task Non_global_sessions_cannot_read_or_edit(
        bool authenticated,
        string role,
        bool hasTenant
    )
    {
        user.SetupGet(x => x.IsAuthenticated).Returns(authenticated);
        user.SetupGet(x => x.Role).Returns(role);
        tenant.SetupGet(x => x.TenantId).Returns(hasTenant ? Guid.NewGuid() : Guid.Empty);
        (await Update("1790016919001")).IsSuccess.Should().BeFalse();
        companies.Verify(
            x => x.GetTrackedByIdForAdminCoreAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Empty_RUC_preserves_pending_identity_while_editing_names()
    {
        (await Update(null)).IsSuccess.Should().BeTrue();
        entity.TaxIdentificationNumber.Should().Be("TMP-EC-ceb18408");
        entity.IsTemporaryTaxIdentification.Should().BeTrue();
    }
}
