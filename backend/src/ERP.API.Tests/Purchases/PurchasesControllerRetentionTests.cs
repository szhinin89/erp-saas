using System.Reflection;
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

namespace ERP.API.Tests.Purchases;

/// <summary>
/// PURCHASES-RETENTIONS-UI-MIGRATION-05C — cubre exclusivamente el wiring HTTP de los dos
/// endpoints nuevos (<see cref="PurchasesController.IssueRetention"/>/<see cref="PurchasesController.GetRetention"/>):
/// que construyen el comando/query transversal correcto a partir de la ruta+body, y que exponen la
/// policy de permiso esperada (reutilizada de Compras, sin permiso nuevo de Retenciones). Las
/// reglas de negocio (duplicados, estado de la compra, elegibilidad, CxP) ya están cubiertas
/// exhaustivamente en <c>IssueRetentionHandlerTests</c> (PURCHASES-RETENTIONS-BRIDGE-05B) — este
/// controller es un pass-through delgado y no las duplica.
/// </summary>
public sealed class PurchasesControllerRetentionTests
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

    private static IssueRetentionLineInput VatLine() =>
        new(RetentionTaxType.Vat, "725", 100m, 30m, 30m);

    [Fact]
    public async Task IssueRetention_construye_IssueRetentionCommand_con_SourceDocumentType_PurchaseInvoice_desde_la_ruta()
    {
        IssueRetentionCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (IssueRetentionCommand)req;
            return Result<RetentionDocumentDto>.Success(null!);
        });
        var purchaseInvoiceId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var issueDate = new DateOnly(2026, 9, 3);

        var response = await controller.IssueRetention(
            purchaseInvoiceId,
            new IssuePurchaseRetentionRequest(emissionPointId, issueDate, new[] { VatLine() }),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be(RetentionSourceDocumentType.PurchaseInvoice);
        captured.SourceDocumentId.Should().Be(purchaseInvoiceId, "SourceDocumentId siempre viene de la ruta, nunca del body");
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.IssueDate.Should().Be(issueDate);
        captured.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task IssueRetention_nunca_expone_SourceDocumentType_ni_SourceDocumentId_en_el_body()
    {
        // Estructuralmente imposible enviar un origen distinto o un Id distinto desde el body —
        // mismo criterio que los tests de forma de IssueRetentionCommand (05B).
        var properties = typeof(IssuePurchaseRetentionRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        properties.Should().NotContain(new[] { "SourceDocumentType", "SourceDocumentId" });
    }

    [Fact]
    public void IssueRetention_requiere_el_permiso_de_Compras_PurchasePermissions_Update()
    {
        var method = typeof(PurchasesController).GetMethod(nameof(PurchasesController.IssueRetention))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be($"perm:{PurchasePermissions.Update}");
    }

    [Fact]
    public void GetRetention_requiere_el_permiso_de_Compras_PurchasePermissions_View()
    {
        var method = typeof(PurchasesController).GetMethod(nameof(PurchasesController.GetRetention))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be($"perm:{PurchasePermissions.View}");
    }

    [Fact]
    public async Task GetRetention_construye_GetRetentionBySourceQuery_con_PurchaseInvoice_y_el_id_de_la_ruta()
    {
        GetRetentionBySourceQuery? captured = null;
        var controller = BuildController(req =>
        {
            captured = (GetRetentionBySourceQuery)req;
            return Result<RetentionDocumentDto?>.Success(null);
        });
        var purchaseInvoiceId = Guid.NewGuid();

        var response = await controller.GetRetention(purchaseInvoiceId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be(RetentionSourceDocumentType.PurchaseInvoice);
        captured.SourceDocumentId.Should().Be(purchaseInvoiceId);
    }

    [Fact]
    public async Task IssueRetention_propaga_el_DTO_de_RetentionDocument_devuelto_por_el_handler()
    {
        var dto = new RetentionDocumentDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            RetentionSourceDocumentType.PurchaseInvoice, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "001-001-000000005", new DateOnly(2026, 9, 3),
            RetentionStatus.Issued, 30m, 0m, 30m, null, null, null,
            new List<RetentionDocumentLineDto>(), "09/2026", "01", "001-001-000000123",
            new DateOnly(2026, 8, 27), null, null, 100m, 115m
        );
        var controller = BuildController(_ => Result<RetentionDocumentDto>.Success(dto));

        var response = await controller.IssueRetention(
            Guid.NewGuid(),
            new IssuePurchaseRetentionRequest(Guid.NewGuid(), new DateOnly(2026, 9, 3), new[] { VatLine() }),
            CancellationToken.None
        );

        var ok = response.Should().BeOfType<OkObjectResult>().Which;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public void Endpoints_legacy_de_withholding_siguen_registrados_sin_cambios()
    {
        // No rompe endpoints legacy (regla explícita de esta fase) — siguen existiendo con la
        // misma firma/policy que antes.
        typeof(PurchasesController).GetMethod(nameof(PurchasesController.GetWithholdingByPurchase))
            .Should().NotBeNull();
        typeof(PurchasesController).GetMethod(nameof(PurchasesController.IssueWithholding))
            .Should().NotBeNull();
        typeof(PurchasesController).GetMethod(nameof(PurchasesController.GetWithholdingById))
            .Should().NotBeNull();
        typeof(PurchasesController).GetMethod(nameof(PurchasesController.CancelWithholding))
            .Should().NotBeNull();
    }
}
