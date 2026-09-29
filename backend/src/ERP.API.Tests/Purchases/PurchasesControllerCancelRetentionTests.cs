using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// PURCHASES-RETENTIONS-CANCEL-05D — wiring HTTP de <see cref="PurchasesController.CancelRetention"/>.
/// ZH-PURCHASES-RETENTION-OWNERSHIP-01: el controller ya no consulta la retención activa antes de
/// delegar (check-then-act); envía un único <see cref="CancelRetentionCommand"/> con la compra de la
/// RUTA como documento origen, y la pertenencia la exige el handler (ver
/// <c>CancelRetentionHandlerTests</c> y la integración PostgreSQL en ERP.Infrastructure.Tests). La
/// policy de permiso sigue siendo la de Compras.
/// </summary>
public sealed class PurchasesControllerCancelRetentionTests
{
    private static PurchasesController BuildController(Func<object, object> handler)
    {
        var controller = new PurchasesController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
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

    private static RetentionDocumentDto BuildRetentionDto(Guid id, Guid purchaseInvoiceId) =>
        new(
            id, Guid.NewGuid(), Guid.NewGuid(),
            RetentionSourceDocumentType.PurchaseInvoice, purchaseInvoiceId, Guid.NewGuid(),
            Guid.NewGuid(), "001-001-000000005", new DateOnly(2026, 9, 3),
            RetentionStatus.Issued, 30m, 0m, 30m, null, null, null,
            new List<RetentionDocumentLineDto>(), "09/2026", "01", "001-001-000000123",
            new DateOnly(2026, 8, 27), null, null, 100m, 115m
        );

    private static async Task<(IActionResult Response, List<object> Sent)> Cancel(
        Guid purchaseId,
        Guid retentionId,
        Func<CancelRetentionCommand, object> reply
    )
    {
        var sent = new List<object>();
        var controller = BuildController(req =>
        {
            sent.Add(req);
            return req is CancelRetentionCommand cmd
                ? reply(cmd)
                : throw new InvalidOperationException($"Unexpected request: {req.GetType().Name}");
        });
        var response = await controller.CancelRetention(
            purchaseId,
            retentionId,
            new CancelPurchaseRetentionRequest("Error en el cálculo"),
            CancellationToken.None
        );
        return (response, sent);
    }

    [Fact]
    public async Task CancelRetention_envia_un_unico_command_con_la_compra_de_la_ruta_como_origen()
    {
        var purchaseId = Guid.NewGuid();
        var retentionId = Guid.NewGuid();
        var cancelled = BuildRetentionDto(retentionId, purchaseId) with { Status = RetentionStatus.Cancelled };

        var (response, sent) = await Cancel(purchaseId, retentionId, _ => Result<RetentionDocumentDto>.Success(cancelled));

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().ContainSingle("sin consulta previa: la pertenencia la valida el handler");
        sent[0].Should().Be(
            new CancelRetentionCommand(RetentionSourceDocumentType.PurchaseInvoice, purchaseId, retentionId, "Error en el cálculo")
        );
    }

    [Fact]
    public async Task CancelRetention_NotFound_de_Application_responde_404_con_el_mensaje_vigente()
    {
        var (response, _) = await Cancel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _ => Result<RetentionDocumentDto>.NotFound("La retención no existe o no pertenece a esta compra.")
        );

        var notFound = response.Should().BeOfType<NotFoundObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(notFound.Value);
        json.GetProperty("Code").GetString().Should().Be(ApiResponseCodes.Common.NotFound);
        json.GetProperty("Data").GetProperty("errors").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("La retención no existe o no pertenece a esta compra.");
    }

    [Fact]
    public async Task CancelRetention_fallo_de_negocio_de_Application_conserva_su_status()
    {
        var (response, _) = await Cancel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _ => Result<RetentionDocumentDto>.ValidationFailure("La CxP ya tiene pagos aplicados.")
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void CancelRetention_requiere_el_permiso_de_Compras_PurchasePermissions_Update()
    {
        var method = typeof(PurchasesController).GetMethod(nameof(PurchasesController.CancelRetention))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be($"perm:{PurchasePermissions.Update}");
    }

    [Fact]
    public void Endpoint_legacy_CancelWithholding_fue_retirado()
    {
        // PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E
        typeof(PurchasesController)
            .GetMethods()
            .Select(m => m.Name)
            .Should()
            .NotContain("CancelWithholding");
    }
}
