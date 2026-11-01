using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using ERP.Application.Modules.ElectronicInvoicing.Health;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriServiceHealth;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ERP.Application.Tests.ElectronicInvoicing;

public sealed class GetSriServiceHealthQueryHandlerTests
{
    private const string TestingUrl =
        "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";
    private const string ProductionUrl =
        "https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";

    /// <summary>Solo devuelve la configuración de la empresa pedida — como el repositorio real (filtro por CompanyId).</summary>
    private sealed class CompanyKeyedSriSettingsRepository : ISriSettingsRepository
    {
        private readonly Dictionary<Guid, SriSettings> _byCompany;

        public CompanyKeyedSriSettingsRepository(params SriSettings[] settings) =>
            _byCompany = settings.ToDictionary(s => s.CompanyId);

        public Task<SriSettings?> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default) =>
            Task.FromResult(_byCompany.GetValueOrDefault(companyId));

        public Task<SriSettings?> GetByCompanyIdForUpdateAsync(Guid companyId, CancellationToken ct = default) =>
            Task.FromResult(_byCompany.GetValueOrDefault(companyId));

        public Task AddAsync(SriSettings config, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(SriSettings config, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedCurrentCompany : ICurrentCompany
    {
        public FixedCurrentCompany(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private readonly SriServiceHealthMonitor _monitor =
        new(Options.Create(new SriHealthOptions()), TimeProvider.System);

    private static SriSettings NewSettings(Guid companyId, int environment, string wsdlUrl) =>
        SriSettings.Create(
            Guid.NewGuid(),
            companyId,
            environment: environment,
            emissionType: 1,
            wsdlUrl: wsdlUrl,
            createdBy: Guid.NewGuid()
        );

    private async Task<SriServiceHealthDto> HandleAsync(Guid companyId, params SriSettings[] settings)
    {
        var handler = new GetSriServiceHealthQueryHandler(
            new CompanyKeyedSriSettingsRepository(settings),
            new FixedCurrentCompany(companyId),
            _monitor
        );
        var result = await handler.Handle(new GetSriServiceHealthQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    [Fact]
    public async Task Without_sri_configuration_both_services_are_unverified_not_configured()
    {
        var dto = await HandleAsync(Guid.NewGuid());

        dto.Environment.Should().BeNull();
        dto.Reception.Reason.Should().Be(SriServiceHealthReason.NotConfigured);
        dto.Authorization.Reason.Should().Be(SriServiceHealthReason.NotConfigured);
    }

    [Fact]
    public async Task Each_company_reads_only_its_own_environment()
    {
        var testingCompany = Guid.NewGuid();
        var productionCompany = Guid.NewGuid();
        var settings = new[]
        {
            NewSettings(testingCompany, 1, TestingUrl),
            NewSettings(productionCompany, 2, ProductionUrl),
        };
        _monitor.Record(TestingUrl, SriServiceKind.Reception, SriObservationOutcome.TransportFailure, null);
        _monitor.Record(TestingUrl, SriServiceKind.Reception, SriObservationOutcome.TransportFailure, null);
        _monitor.Record(ProductionUrl, SriServiceKind.Authorization, SriObservationOutcome.Responded, TimeSpan.FromSeconds(1));

        var testing = await HandleAsync(testingCompany, settings);
        testing.Environment.Should().Be("Test");
        testing.EnvironmentName.Should().Be("Pruebas");
        testing.Reception.State.Should().Be(SriServiceHealthState.Unavailable);
        testing.Authorization.State.Should().Be(SriServiceHealthState.Unverified);

        var production = await HandleAsync(productionCompany, settings);
        production.Environment.Should().Be("Production");
        production.Reception.State.Should().Be(SriServiceHealthState.Unverified);
        production.Authorization.State.Should().Be(SriServiceHealthState.Available);
    }

    [Fact]
    public async Task A_url_that_contradicts_the_environment_is_not_configured()
    {
        var company = Guid.NewGuid();
        _monitor.Record(ProductionUrl, SriServiceKind.Reception, SriObservationOutcome.Responded, TimeSpan.FromSeconds(1));

        var dto = await HandleAsync(company, NewSettings(company, 1, ProductionUrl));

        dto.Reception.Reason.Should().Be(SriServiceHealthReason.NotConfigured);
    }

    [Fact]
    public void The_contract_carries_no_sensitive_fields()
    {
        typeof(SriServiceHealthDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            nameof(SriServiceHealthDto.Environment),
            nameof(SriServiceHealthDto.EnvironmentName),
            nameof(SriServiceHealthDto.Reception),
            nameof(SriServiceHealthDto.Authorization)
        );
        typeof(SriServiceStatusDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            nameof(SriServiceStatusDto.State),
            nameof(SriServiceStatusDto.Reason),
            nameof(SriServiceStatusDto.Evidence),
            nameof(SriServiceStatusDto.LastObservedAtUtc)
        );
    }
}
