using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentAuthorizationServiceTests
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

    private sealed class FakeAuthorizationClient : ISriAuthorizationClient
    {
        private readonly Func<string, string, SriAuthorizationResult> _behavior;
        public string? CapturedAccessKey;
        public string? CapturedWsdlUrl;

        public FakeAuthorizationClient(Func<string, string, SriAuthorizationResult> behavior) =>
            _behavior = behavior;

        public Task<SriAuthorizationResult> CheckAsync(
            string accessKey,
            string wsdlUrl,
            CancellationToken ct = default
        )
        {
            CapturedAccessKey = accessKey;
            CapturedWsdlUrl = wsdlUrl;
            return Task.FromResult(_behavior(accessKey, wsdlUrl));
        }
    }

    private static SriSettings ValidSriSettings(
        string wsdlUrl =
            "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl"
    ) =>
        SriSettings.Create(
            tenantId: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            environment: 1,
            emissionType: 1,
            wsdlUrl: wsdlUrl,
            createdBy: Guid.NewGuid()
        );

    [Fact]
    public async Task CheckAsync_without_sri_settings_fails_with_clear_message_not_exception()
    {
        var service = new ElectronicDocumentAuthorizationService(
            new FakeSriSettingsRepository(null),
            new FakeAuthorizationClient(
                (_, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            NullLogger<ElectronicDocumentAuthorizationService>.Instance
        );

        var result = await service.CheckAsync(Guid.NewGuid(), new string('1', 49));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("configuración SRI");
    }

    [Fact]
    public async Task CheckAsync_without_wsdl_url_fails_with_clear_message()
    {
        var settings = ValidSriSettings(wsdlUrl: "   ");
        var service = new ElectronicDocumentAuthorizationService(
            new FakeSriSettingsRepository(settings),
            new FakeAuthorizationClient(
                (_, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            NullLogger<ElectronicDocumentAuthorizationService>.Instance
        );

        var result = await service.CheckAsync(Guid.NewGuid(), new string('1', 49));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("WsdlUrl");
    }

    [Theory]
    [InlineData("ERROR_CONEXION")]
    [InlineData("ERROR_RESPUESTA_INVALIDA")]
    [InlineData("SIN_RESPUESTA")]
    [InlineData("DESCONOCIDO")]
    public async Task CheckAsync_on_non_terminal_statuses_translates_to_failure(string status)
    {
        var settings = ValidSriSettings();
        var client = new FakeAuthorizationClient(
            (_, _) =>
                new SriAuthorizationResult { Status = status, ErrorMessage = "detalle del fallo" }
        );
        var service = new ElectronicDocumentAuthorizationService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentAuthorizationService>.Instance
        );

        var result = await service.CheckAsync(Guid.NewGuid(), new string('1', 49));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("detalle del fallo");
    }

    [Theory]
    [InlineData("AUTORIZADO")]
    [InlineData("NO AUTORIZADO")]
    [InlineData("TIMEOUT")]
    public async Task CheckAsync_on_terminal_statuses_succeeds_and_forwards_status(string status)
    {
        var settings = ValidSriSettings();
        var client = new FakeAuthorizationClient(
            (_, _) => new SriAuthorizationResult { Status = status }
        );
        var service = new ElectronicDocumentAuthorizationService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentAuthorizationService>.Instance
        );

        var result = await service.CheckAsync(Guid.NewGuid(), new string('1', 49));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(status);
    }

    [Fact]
    public async Task CheckAsync_passes_access_key_and_wsdl_url_to_client()
    {
        var settings = ValidSriSettings(
            "https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl"
        );
        var accessKey = new string('7', 49);
        var client = new FakeAuthorizationClient(
            (_, _) => new SriAuthorizationResult { Status = "AUTORIZADO" }
        );
        var service = new ElectronicDocumentAuthorizationService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentAuthorizationService>.Instance
        );

        await service.CheckAsync(Guid.NewGuid(), accessKey);

        client.CapturedAccessKey.Should().Be(accessKey);
        client.CapturedWsdlUrl.Should().Be(settings.WsdlUrl);
    }
}
