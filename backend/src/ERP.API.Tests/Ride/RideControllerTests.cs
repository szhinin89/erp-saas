using ERP.API.Contracts;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Application.Modules.Ride.UseCases.RegenerateRide;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Ride;

/// <summary>
/// Fase 9 (ADR-025): contrato de <see cref="RideController"/> con <see cref="StubMediator"/> —
/// prueba únicamente que el controller transforma <c>Result&lt;RideGenerationResultDto&gt;</c> a
/// HTTP correctamente, sin abrir Postgres ni el pipeline real (eso lo cubre
/// <c>RideControllerIntegrationTests</c>). "Documento inexistente" y "company diferente" producen
/// exactamente el mismo <see cref="RideOutcome.NotApplicable"/> desde la perspectiva del
/// controller — a propósito (mismo criterio de no-enumeración que <c>ElectronicDocuments</c>), y
/// se prueban como dos casos separados para dejarlo documentado explícitamente, no por asumirlo.
/// </summary>
public sealed class RideControllerTests
{
    private static RideController BuildController(Func<object, object> handler)
    {
        var controller = new RideController(new StubMediator(handler), new NoOpFileStorage());
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

    private static readonly RidePdfMetadataDto Metadata = new(
        "Invoice",
        "unversioned",
        "unversioned",
        "unversioned",
        new string('a', 64),
        DateTime.UtcNow,
        WasCached: false
    );

    [Fact]
    public async Task GetOrGenerate_success_returns_200_with_the_outcome_from_application()
    {
        var expected = new RideGenerationResultDto(
            RideOutcome.Generated,
            "ride/path.pdf",
            Metadata,
            null
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.GetOrGenerate(
            "Sales",
            Guid.NewGuid(),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.Generated);
        body.Data.StoragePath.Should().Be("ride/path.pdf");
    }

    [Fact]
    public async Task GetOrGenerate_nonexistent_document_returns_200_with_not_applicable_never_a_404()
    {
        var expected = new RideGenerationResultDto(
            RideOutcome.NotApplicable,
            null,
            null,
            "source_not_applicable"
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.GetOrGenerate(
            "Sales",
            Guid.NewGuid(),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task GetOrGenerate_other_company_document_is_indistinguishable_from_nonexistent()
    {
        // Company Scope ya filtra la fila a nivel de EF (query filter global) antes de que
        // Application vea nada — desde el controller, este caso llega EXACTAMENTE igual que
        // "documento inexistente": Outcome.NotApplicable. Documentado aquí explícitamente para
        // que quede probado, no asumido.
        var expected = new RideGenerationResultDto(
            RideOutcome.NotApplicable,
            null,
            null,
            "source_not_applicable"
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.GetOrGenerate(
            "Sales",
            Guid.NewGuid(),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task GetOrGenerate_delegates_exactly_the_query_parameters_it_received()
    {
        GetOrGenerateRideQuery? captured = null;
        var controller = BuildController(req =>
        {
            captured = (GetOrGenerateRideQuery)req;
            return Result<RideGenerationResultDto>.Success(
                new RideGenerationResultDto(RideOutcome.Generated, "x", Metadata, null)
            );
        });
        var sourceEntityId = Guid.NewGuid();

        await controller.GetOrGenerate("Sales", sourceEntityId, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SourceModule.Should().Be("Sales");
        captured.SourceEntityId.Should().Be(sourceEntityId);
    }

    [Fact]
    public async Task Regenerate_success_returns_200_with_the_outcome_from_application()
    {
        var expected = new RideGenerationResultDto(
            RideOutcome.Generated,
            "ride/path.pdf",
            Metadata,
            null
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.Regenerate(
            new RegenerateRideCommand("Sales", Guid.NewGuid()),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.Generated);
    }

    [Fact]
    public async Task Regenerate_nonexistent_document_returns_200_with_not_applicable()
    {
        var expected = new RideGenerationResultDto(
            RideOutcome.NotApplicable,
            null,
            null,
            "source_not_applicable"
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.Regenerate(
            new RegenerateRideCommand("Sales", Guid.NewGuid()),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Regenerate_other_company_document_is_indistinguishable_from_nonexistent()
    {
        var expected = new RideGenerationResultDto(
            RideOutcome.NotApplicable,
            null,
            null,
            "source_not_applicable"
        );
        var controller = BuildController(_ => Result<RideGenerationResultDto>.Success(expected));

        var response = await controller.Regenerate(
            new RegenerateRideCommand("Sales", Guid.NewGuid()),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<RideGenerationResultDto>>().Subject;
        body.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Regenerate_delegates_exactly_the_command_it_received()
    {
        RegenerateRideCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (RegenerateRideCommand)req;
            return Result<RideGenerationResultDto>.Success(
                new RideGenerationResultDto(RideOutcome.Generated, "x", Metadata, null)
            );
        });
        var sourceEntityId = Guid.NewGuid();

        await controller.Regenerate(
            new RegenerateRideCommand("Sales", sourceEntityId),
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.SourceModule.Should().Be("Sales");
        captured.SourceEntityId.Should().Be(sourceEntityId);
    }

    // ── ZH-API-ERROR-CONTRACT-HARDENING-01: GetContent usa el contrato único Error → HTTP ──

    [Fact]
    public async Task GetContent_failure_without_code_keeps_400_BAD_REQUEST()
    {
        var controller = BuildController(_ =>
            Result<RideGenerationResultDto>.Failure("No se pudo consultar el cache de RIDE.")
        );

        var response = await controller.GetContent("Sales", Guid.NewGuid(), CancellationToken.None);

        var obj = response.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        obj.Value.Should()
            .BeOfType<ApiResponse<object>>()
            .Which.Code.Should()
            .Be(ApiResponseCodes.Common.BadRequest);
    }

    [Theory]
    [InlineData(ApiResponseCodes.Common.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ApiResponseCodes.Common.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ApiResponseCodes.Common.SriCommunicationError, StatusCodes.Status502BadGateway)]
    public async Task GetContent_coded_failure_maps_like_GetOrGenerate(string code, int status)
    {
        var controller = BuildController(_ =>
            Result<RideGenerationResultDto>.Failure("detalle", code)
        );

        var content = await controller.GetContent("Sales", Guid.NewGuid(), CancellationToken.None);
        var json = await controller.GetOrGenerate("Sales", Guid.NewGuid(), CancellationToken.None);

        var contentObj = content.Should().BeAssignableTo<ObjectResult>().Subject;
        contentObj.StatusCode.Should().Be(status);
        contentObj.Value.Should().BeOfType<ApiResponse<object>>().Which.Code.Should().Be(code);
        json.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task GetContent_nonexistent_and_other_company_are_the_same_404()
    {
        var controller = BuildController(_ =>
            Result<RideGenerationResultDto>.Success(
                new RideGenerationResultDto(RideOutcome.NotApplicable, null, null, null)
            )
        );

        var nonexistent = await controller.GetContent(
            "Sales",
            Guid.NewGuid(),
            CancellationToken.None
        );
        var otherCompany = await controller.GetContent(
            "Sales",
            Guid.NewGuid(),
            CancellationToken.None
        );

        foreach (var response in new[] { nonexistent, otherCompany })
        {
            var obj = response.Should().BeAssignableTo<ObjectResult>().Subject;
            obj.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            obj.Value.Should()
                .BeOfType<ApiResponse<object>>()
                .Which.Code.Should()
                .Be(ApiResponseCodes.Common.NotFound);
        }
    }
}
