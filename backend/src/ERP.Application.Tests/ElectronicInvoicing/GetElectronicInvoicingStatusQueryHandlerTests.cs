using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicInvoicing;

public sealed class GetElectronicInvoicingStatusQueryHandlerTests
{
    private sealed class FakeSriSettingsRepository : ISriSettingsRepository
    {
        private readonly SriSettings? _settings;

        public FakeSriSettingsRepository(SriSettings? settings) => _settings = settings;

        public Task<SriSettings?> GetByCompanyIdAsync(
            Guid companyId,
            CancellationToken ct = default
        ) => Task.FromResult(_settings);

        public Task<SriSettings?> GetByCompanyIdForUpdateAsync(
            Guid companyId,
            CancellationToken ct = default
        ) => Task.FromResult(_settings);

        public Task AddAsync(SriSettings config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(SriSettings config, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedCurrentCompany : ICurrentCompany
    {
        public FixedCurrentCompany(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FakeCertificateStatusResolver : ISriCertificateStatusResolver
    {
        private readonly SriCertificateStatus _result;

        public FakeCertificateStatusResolver(SriCertificateStatus result) => _result = result;

        public Task<SriCertificateStatus> ResolveAsync(
            SriSettings settings,
            CancellationToken ct = default
        ) => Task.FromResult(_result);
    }

    private sealed class ThrowingCertificateStatusResolver : ISriCertificateStatusResolver
    {
        public Task<SriCertificateStatus> ResolveAsync(
            SriSettings settings,
            CancellationToken ct = default
        ) => throw new InvalidOperationException("boom");
    }

    private sealed class FakeConnectivityChecker : ISriConnectivityChecker
    {
        private readonly bool _reachable;

        public FakeConnectivityChecker(bool reachable) => _reachable = reachable;

        public Task<bool> PingAsync(string wsdlUrl, CancellationToken ct = default) =>
            Task.FromResult(_reachable);
    }

    private static SriCertificateStatus ValidCertExpiringIn(int days) =>
        new(
            Installed: true,
            PasswordCorrect: true,
            Valid: true,
            NotAfterUtc: DateTime.UtcNow.AddDays(days),
            Subject: "CN=Test",
            Issuer: "CN=CA",
            ErrorMessage: null
        );

    private static readonly SriCertificateStatus ValidCert = ValidCertExpiringIn(365);

    private static readonly SriCertificateStatus InvalidCert = new(
        Installed: true,
        PasswordCorrect: false,
        Valid: false,
        NotAfterUtc: null,
        Subject: null,
        Issuer: null,
        ErrorMessage: "contraseña incorrecta"
    );

    private static GetElectronicInvoicingStatusQueryHandler BuildHandler(
        SriSettings? settings,
        SriCertificateStatus certStatus,
        bool sriReachable = true
    ) =>
        new(
            new FakeSriSettingsRepository(settings),
            new FixedCurrentCompany(settings?.CompanyId ?? Guid.NewGuid()),
            new FakeCertificateStatusResolver(certStatus),
            new FakeConnectivityChecker(sriReachable),
            NullLogger<GetElectronicInvoicingStatusQueryHandler>.Instance
        );

    private static SriSettings NewSettings(int environment) =>
        SriSettings.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            environment: environment,
            emissionType: 1,
            wsdlUrl: "https://celcer.sri.gob.ec/wsdl",
            createdBy: Guid.NewGuid()
        );

    [Fact]
    public async Task Handle_without_sri_settings_returns_not_configured()
    {
        var handler = BuildHandler(null, InvalidCert);

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.NotConfigured);
        result.Value.Configured.Should().BeFalse();
        result.Value.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_configured_with_valid_certificate_in_production_is_ready()
    {
        // Ficha Técnica SRI, Tabla 4 "Ambiente": 2 = Producción (ver SriEnvironmentConfiguration).
        var settings = NewSettings(environment: 2);
        var handler = BuildHandler(settings, ValidCert);

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.Ready);
        result.Value.Environment.Should().Be("Production");
        result.Value.EnvironmentName.Should().Be("Producción");
        result.Value.EmissionType.Should().Be("Normal");
        result.Value.SriAvailability.Should().Be(SriAvailability.Available);
        result.Value.CanIssue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_configured_in_test_environment_reports_testing()
    {
        // Ficha Técnica SRI, Tabla 4 "Ambiente": 1 = Pruebas (ver SriEnvironmentConfiguration).
        var settings = NewSettings(environment: 1);
        var handler = BuildHandler(settings, ValidCert);

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.Testing);
        result.Value.Environment.Should().Be("Test");
        result.Value.CanIssue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_configured_but_certificate_invalid_is_incomplete()
    {
        var settings = NewSettings(environment: 1);
        var handler = BuildHandler(settings, InvalidCert);

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.Incomplete);
        result.Value.CertificateInstalled.Should().BeTrue();
        result.Value.CertificateValid.Should().BeFalse();
        result.Value.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_with_expired_certificate_reports_certificate_expired()
    {
        var settings = NewSettings(environment: 1);
        var handler = BuildHandler(settings, ValidCertExpiringIn(-5));

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.CertificateExpired);
        result.Value.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_with_certificate_expiring_soon_reports_certificate_expiring_but_can_still_issue()
    {
        var settings = NewSettings(environment: 1);
        var handler = BuildHandler(settings, ValidCertExpiringIn(10));

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.CertificateExpiring);
        result.Value.CertificateDaysRemaining.Should().Be(10);
        result.Value.CanIssue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_when_sri_unreachable_reports_sri_unavailable()
    {
        var settings = NewSettings(environment: 1);
        var handler = BuildHandler(settings, ValidCert, sriReachable: false);

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.SriUnavailable);
        result.Value.SriAvailability.Should().Be(SriAvailability.Unavailable);
        result.Value.CanIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_when_resolution_throws_returns_error_status_not_exception()
    {
        var settings = NewSettings(environment: 1);
        var handler = new GetElectronicInvoicingStatusQueryHandler(
            new FakeSriSettingsRepository(settings),
            new FixedCurrentCompany(settings.CompanyId),
            new ThrowingCertificateStatusResolver(),
            new FakeConnectivityChecker(true),
            NullLogger<GetElectronicInvoicingStatusQueryHandler>.Instance
        );

        var result = await handler.Handle(
            new GetElectronicInvoicingStatusQuery(),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ElectronicInvoicingStatus.Error);
        result.Value.CanIssue.Should().BeFalse();
    }
}
