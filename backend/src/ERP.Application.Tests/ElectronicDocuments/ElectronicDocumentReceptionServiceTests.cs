using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentReceptionServiceTests
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

    private sealed class FakeReceptionClient : ISriReceptionClient
    {
        private readonly Func<byte[], string, SriReceptionResult> _behavior;
        public string? CapturedWsdlUrl;
        public byte[]? CapturedBytes;

        public FakeReceptionClient(Func<byte[], string, SriReceptionResult> behavior) =>
            _behavior = behavior;

        public Task<SriReceptionResult> SendAsync(
            byte[] signedXmlBytes,
            string wsdlUrl,
            CancellationToken ct = default
        )
        {
            CapturedBytes = signedXmlBytes;
            CapturedWsdlUrl = wsdlUrl;
            return Task.FromResult(_behavior(signedXmlBytes, wsdlUrl));
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
    public async Task SendAsync_without_sri_settings_fails_with_clear_message_not_exception()
    {
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(null),
            new FakeReceptionClient(
                (_, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );

        var result = await service.SendAsync(Guid.NewGuid(), [1, 2, 3]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("configuración SRI");
    }

    [Fact]
    public async Task SendAsync_without_wsdl_url_fails_with_clear_message()
    {
        var settings = ValidSriSettings(wsdlUrl: "   ");
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(settings),
            new FakeReceptionClient(
                (_, _) => throw new InvalidOperationException("no debería invocarse")
            ),
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );

        var result = await service.SendAsync(Guid.NewGuid(), [1, 2, 3]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("WsdlUrl");
    }

    [Fact]
    public async Task SendAsync_when_client_reports_connection_error_translates_to_failure()
    {
        var settings = ValidSriSettings();
        var client = new FakeReceptionClient(
            (_, _) =>
                new SriReceptionResult
                {
                    Status = "ERROR_CONEXION",
                    Errors = ["No se pudo contactar al servicio de recepción del SRI."],
                }
        );
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );

        var result = await service.SendAsync(Guid.NewGuid(), [1, 2, 3]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contactar");
    }

    [Fact]
    public async Task SendAsync_when_client_reports_malformed_response_translates_to_failure()
    {
        var settings = ValidSriSettings();
        var client = new FakeReceptionClient(
            (_, _) =>
                new SriReceptionResult
                {
                    Status = "ERROR_RESPUESTA_INVALIDA",
                    Errors =
                    [
                        "El SRI respondió con un contenido que no pudo interpretarse como XML válido.",
                    ],
                }
        );
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );

        var result = await service.SendAsync(Guid.NewGuid(), [1, 2, 3]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("XML válido");
    }

    [Fact]
    public async Task SendAsync_when_sri_returns_recibida_succeeds_with_received_true()
    {
        var settings = ValidSriSettings(
            "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl"
        );
        var client = new FakeReceptionClient(
            (_, _) => new SriReceptionResult { Status = "RECIBIDA" }
        );
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes("<factura/>");

        var result = await service.SendAsync(Guid.NewGuid(), xmlBytes);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Received.Should().BeTrue();
        client.CapturedBytes.Should().BeEquivalentTo(xmlBytes);
        client.CapturedWsdlUrl.Should().Be(settings.WsdlUrl);
    }

    [Fact]
    public async Task SendAsync_when_sri_returns_devuelta_succeeds_but_received_is_false()
    {
        var settings = ValidSriSettings();
        var client = new FakeReceptionClient(
            (_, _) =>
                new SriReceptionResult
                {
                    Status = "DEVUELTA",
                    Errors = ["[35] DOCUMENTO INVÁLIDO: estructura incorrecta."],
                }
        );
        var service = new ElectronicDocumentReceptionService(
            new FakeSriSettingsRepository(settings),
            client,
            NullLogger<ElectronicDocumentReceptionService>.Instance
        );

        var result = await service.SendAsync(Guid.NewGuid(), [1, 2, 3]);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Received.Should().BeFalse();
        result.Value.Status.Should().Be("DEVUELTA");
        result.Value.Errors.Should().ContainSingle();
    }
}
