using ERP.API.Contracts;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — contrato de <see cref="RetentionsController"/> con
/// <see cref="StubMediator"/>: prueba únicamente que el controller transforma
/// <c>Result&lt;ElectronicDocumentXml&gt;</c>/<c>Result&lt;byte[]&gt;</c> a HTTP con el
/// content-type/nombre de archivo correctos, sin abrir Postgres ni el wiring real de Retención.
/// </summary>
public sealed class RetentionsControllerTests
{
    private static RetentionsController BuildController(Func<object, object> handler)
    {
        var controller = new RetentionsController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
            },
        };
        return controller;
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }

    private static ElectronicDocumentXml SampleXml() =>
        new(
            Xml: "<comprobanteRetencion/>",
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ElectronicDocumentType.Retention,
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    // ── GET {id}/electronic/xml ──────────────────────────────────────────

    [Fact]
    public async Task GetElectronicXml_success_returns_application_xml_with_a_safe_filename()
    {
        var id = Guid.NewGuid();
        var controller = BuildController(_ => Result<ElectronicDocumentXml>.Success(SampleXml()));

        var response = await controller.GetElectronicXml(id, CancellationToken.None);

        var file = response.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/xml; charset=utf-8");
        file.FileDownloadName.Should().Be($"retencion-{id:N}.xml");
        file.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetElectronicXml_delegates_exactly_the_retention_id_it_received()
    {
        GenerateRetentionXmlQuery? captured = null;
        var controller = BuildController(req =>
        {
            captured = (GenerateRetentionXmlQuery)req;
            return Result<ElectronicDocumentXml>.Success(SampleXml());
        });
        var id = Guid.NewGuid();

        await controller.GetElectronicXml(id, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RetentionId.Should().Be(id);
    }

    [Fact]
    public async Task GetElectronicXml_failure_returns_400_with_the_error_never_a_file()
    {
        var controller = BuildController(_ =>
            Result<ElectronicDocumentXml>.ValidationFailure(
                "La retención debe estar emitida para generar el documento electrónico."
            )
        );

        var response = await controller.GetElectronicXml(Guid.NewGuid(), CancellationToken.None);

        var badRequest = response.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
        var body = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        System.Text.Json.JsonSerializer.Serialize(body.Data).Should().Contain("emitida");
    }

    [Fact]
    public async Task GetElectronicXml_not_found_still_returns_400_never_enumerating_the_reason()
    {
        // El controller es delgado y usa el mismo mapeo simple que RideController.GetContent —
        // no distingue "no existe" de otros fallos de negocio, mismo criterio de no-enumeración.
        var controller = BuildController(_ =>
            Result<ElectronicDocumentXml>.NotFound("La retención no existe.")
        );

        var response = await controller.GetElectronicXml(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GET {id}/ride/pdf ─────────────────────────────────────────────────

    [Fact]
    public async Task GetRidePdf_success_returns_application_pdf_with_a_safe_filename()
    {
        var id = Guid.NewGuid();
        byte[] pdfBytes = [1, 2, 3, 4];
        var controller = BuildController(_ => Result<byte[]>.Success(pdfBytes));

        var response = await controller.GetRidePdf(id, CancellationToken.None);

        var file = response.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be($"retencion-{id:N}.pdf");
        file.FileContents.Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task GetRidePdf_delegates_exactly_the_retention_id_it_received()
    {
        GenerateRetentionRidePdfQuery? captured = null;
        var controller = BuildController(req =>
        {
            captured = (GenerateRetentionRidePdfQuery)req;
            return Result<byte[]>.Success([1]);
        });
        var id = Guid.NewGuid();

        await controller.GetRidePdf(id, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RetentionId.Should().Be(id);
    }

    [Fact]
    public async Task GetRidePdf_failure_returns_400_with_the_error_never_a_file()
    {
        var controller = BuildController(_ =>
            Result<byte[]>.Failure("No se pudo generar el PDF del comprobante de retención.")
        );

        var response = await controller.GetRidePdf(Guid.NewGuid(), CancellationToken.None);

        var badRequest = response.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }
}
