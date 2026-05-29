using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Onboarding;

/// <summary>
/// Tests for Company onboarding domain logic and status transitions.
/// </summary>
public sealed class CompanyDomainOnboardingTests
{
    private static Company NewProvisionalCompany() =>
        Company.CreateManaged(
            subscriberId:       Guid.NewGuid(),
            ruc:                "TMP-EC-abc12345",
            legalName:          "Empresa Test",
            mainAddress:        "—",
            isProvisionalTaxId: true,
            taxIdStatus:        ERP.Domain.Modules.Company.Enums.TaxIdStatus.Pending);

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void New_company_starts_as_PendingSetup_not_completed()
    {
        var company = NewProvisionalCompany();
        company.OnboardingCompleted.Should().BeFalse();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.PendingSetup);
    }

    [Fact]
    public void New_company_has_provisional_ruc_and_pending_tax_status()
    {
        var company = NewProvisionalCompany();
        company.IsProvisionalTaxId.Should().BeTrue();
        company.TaxIdStatus.Should().Be(ERP.Domain.Modules.Company.Enums.TaxIdStatus.Pending);
        company.Ruc.Should().StartWith("TMP-EC-");
    }

    // ── CompleteOnboarding ────────────────────────────────────────────────────

    [Fact]
    public void CompleteOnboarding_sets_completed_and_operational()
    {
        var company = NewProvisionalCompany();
        company.CompleteOnboarding();
        company.OnboardingCompleted.Should().BeTrue();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
    }

    [Fact]
    public void CompleteOnboarding_idempotent_when_called_twice()
    {
        var company = NewProvisionalCompany();
        company.CompleteOnboarding();
        company.CompleteOnboarding(); // second call
        company.OnboardingCompleted.Should().BeTrue();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
    }

    // ── UpdateTaxId ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTaxId_replaces_provisional_with_real_ruc()
    {
        var company = NewProvisionalCompany();
        company.UpdateTaxId("1234567890001", isProvisional: false, ERP.Domain.Modules.Company.Enums.TaxIdStatus.Verified);
        company.Ruc.Should().Be("1234567890001");
        company.IsProvisionalTaxId.Should().BeFalse();
        company.TaxIdStatus.Should().Be(ERP.Domain.Modules.Company.Enums.TaxIdStatus.Verified);
    }

    // ── SuspendOperations ─────────────────────────────────────────────────────

    [Fact]
    public void SuspendOperations_sets_suspended_status()
    {
        var company = NewProvisionalCompany();
        company.CompleteOnboarding();
        company.SuspendOperations();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
        company.OnboardingCompleted.Should().BeTrue(); // flag stays true
    }

    [Fact]
    public void MarkOperational_restores_from_suspended()
    {
        var company = NewProvisionalCompany();
        company.CompleteOnboarding();
        company.SuspendOperations();
        company.MarkOperational();
        company.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
    }

    // ── CompanyOperationalStatus enum ─────────────────────────────────────────

    [Theory]
    [InlineData(CompanyOperationalStatus.PendingSetup, 1)]
    [InlineData(CompanyOperationalStatus.Operational, 2)]
    [InlineData(CompanyOperationalStatus.Suspended, 3)]
    public void OperationalStatus_enum_values_are_stable(CompanyOperationalStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }
}

/// <summary>
/// Validates the onboarding command validator logic.
/// </summary>
public sealed class CompleteOnboardingValidatorTests
{
    private static ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingValidator Validator()
        => new();

    [Fact]
    public async Task Valid_command_passes_validation()
    {
        var cmd = new ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingCommand(
            UseSubscriberData: false,
            TaxId: "1234567890001",
            LegalName: "Empresa XYZ S.A.",
            TradeName: null,
            MainAddress: "Av. Principal 001",
            Phone: null,
            Email: null);

        var result = await Validator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Provisional_TaxId_fails_validation()
    {
        var cmd = new ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingCommand(
            UseSubscriberData: false,
            TaxId: "TMP-EC-abc12345",
            LegalName: "Test",
            TradeName: null,
            MainAddress: "Dirección",
            Phone: null,
            Email: null);

        var result = await Validator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.TaxId));
    }

    [Fact]
    public async Task Placeholder_address_fails_validation()
    {
        var cmd = new ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingCommand(
            UseSubscriberData: false,
            TaxId: "1234567890001",
            LegalName: "Test",
            TradeName: null,
            MainAddress: "—",
            Phone: null,
            Email: null);

        var result = await Validator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.MainAddress));
    }

    [Fact]
    public async Task Empty_TaxId_fails_validation()
    {
        var cmd = new ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingCommand(
            UseSubscriberData: false,
            TaxId: "",
            LegalName: "Test",
            TradeName: null,
            MainAddress: "Dirección",
            Phone: null,
            Email: null);

        var result = await Validator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Invalid_email_fails_validation()
    {
        var cmd = new ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding.CompleteCompanyOnboardingCommand(
            UseSubscriberData: false,
            TaxId: "1234567890001",
            LegalName: "Test",
            TradeName: null,
            MainAddress: "Dirección",
            Phone: null,
            Email: "not-an-email");

        var result = await Validator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Email));
    }
}
