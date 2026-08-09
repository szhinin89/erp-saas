using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// FLOW-READY-02C.2 — contrato de <c>PurchaseCreditNoteController</c> con <c>StubMediator</c>, mismo
/// patrón que <c>PurchaseReturnControllerTests</c>: mapeo de Command/Query a HTTP y verificación por
/// reflexión de la policy declarada (sin permisos nuevos, reutiliza <see cref="PurchasePermissions"/>).
/// </summary>
public sealed class PurchaseCreditNoteControllerTests
{
    private static PurchaseCreditNoteController BuildController(Func<object, object> handler)
    {
        var controller = new PurchaseCreditNoteController(new StubMediator(handler));
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

    private static PurchaseCreditNoteDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Discount",
            null,
            "Draft",
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            100m,
            0m,
            15m,
            115m,
            null,
            null,
            null,
            null,
            new List<PurchaseCreditNoteDetailDto>(),
            new List<PurchaseCreditNoteTaxSummaryDto>(),
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa — 6 endpoints, sin permisos nuevos ────────

    [Theory]
    [InlineData(nameof(PurchaseCreditNoteController.CreateDraft), PurchasePermissions.Create)]
    [InlineData(nameof(PurchaseCreditNoteController.UpdateDraft), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseCreditNoteController.GetById), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseCreditNoteController.GetList), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseCreditNoteController.Authorize), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseCreditNoteController.Cancel), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseCreditNoteController.LinkReturn), PurchasePermissions.Update)]
    public void Endpoint_exige_la_policy_de_PurchasePermissions_esperada(
        string methodName,
        string expectedPermission
    )
    {
        var method = typeof(PurchaseCreditNoteController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{expectedPermission}");
    }

    [Fact]
    public void Controller_no_declara_ningun_permiso_fuera_de_PurchasePermissions()
    {
        var methods = typeof(PurchaseCreditNoteController)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any());

        foreach (var method in methods)
        {
            var attr = method
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Single();
            attr.Policy.Should()
                .BeOneOf(
                    $"perm:{PurchasePermissions.View}",
                    $"perm:{PurchasePermissions.Create}",
                    $"perm:{PurchasePermissions.Update}"
                );
        }
    }

    // ── POST /purchases/credit-notes ────────────────────────────────────

    [Fact]
    public async Task CreateDraft_exitoso_retorna_201_y_mapea_el_request_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteDto>.Success(SampleDto(id));
        });
        var invoiceId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new CreatePurchaseCreditNoteDraftRequest(
            clientRequestId,
            invoiceId,
            null,
            ERP.Domain.Modules.Purchases.Enums.PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNoteDraftLineInput("Descuento", 100m, "2", 15m, 15m) }
        );

        var response = await controller.CreateDraft(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sent.Should().BeOfType<CreateDraftPurchaseCreditNoteCommand>();
        var cmd = (CreateDraftPurchaseCreditNoteCommand)sent!;
        cmd.ClientRequestId.Should().Be(clientRequestId);
        cmd.PurchaseInvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public async Task CreateDraft_sobre_factura_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<PurchaseCreditNoteDto>.NotFound("Factura de compra no encontrada.")
        );
        var request = new CreatePurchaseCreditNoteDraftRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            ERP.Domain.Modules.Purchases.Enums.PurchaseCreditNoteApplicationType.Discount,
            "001-001-000000005",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Descuento",
            new[] { new PurchaseCreditNoteDraftLineInput("Descuento", 100m, "2", 15m, 15m) }
        );

        var response = await controller.CreateDraft(request, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PUT /purchases/credit-notes/{id} ────────────────────────────────

    [Fact]
    public async Task UpdateDraft_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteDto>.Success(SampleDto(id));
        });
        var request = new UpdatePurchaseCreditNoteDraftRequest(
            "001-001-000000006",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Nuevo motivo",
            new[] { new PurchaseCreditNoteDraftLineInput("Descuento", 200m, "2", 15m, 30m) }
        );

        var response = await controller.UpdateDraft(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<UpdatePurchaseCreditNoteDraftCommand>();
        ((UpdatePurchaseCreditNoteDraftCommand)sent!).Id.Should().Be(id);
    }

    // ── GET /purchases/credit-notes/{id} ────────────────────────────────

    [Fact]
    public async Task GetById_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /purchases/credit-notes ─────────────────────────────────────

    [Fact]
    public async Task GetList_mapea_filtros_de_query_al_query_de_MediatR()
    {
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteListResultDto>.Success(
                new PurchaseCreditNoteListResultDto(
                    new List<PurchaseCreditNoteListItemDto>(),
                    0,
                    1,
                    20
                )
            );
        });
        var supplierId = Guid.NewGuid();

        var response = await controller.GetList(
            status: "Draft",
            supplierId: supplierId,
            invoiceId: null,
            dateFrom: null,
            dateTo: null,
            page: 2,
            pageSize: 10,
            ct: CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        var q = sent.Should().BeOfType<GetPurchaseCreditNoteListQuery>().Which;
        q.Status.Should().Be("Draft");
        q.SupplierId.Should().Be(supplierId);
        q.Page.Should().Be(2);
        q.PageSize.Should().Be(10);
    }

    // ── POST /purchases/credit-notes/{id}/authorize ─────────────────────

    [Fact]
    public async Task Authorize_exitoso_retorna_200()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteDto>.Success(SampleDto(id));
        });
        var clientRequestId = Guid.NewGuid();

        var response = await controller.Authorize(
            id,
            new AuthorizePurchaseCreditNoteRequest(clientRequestId),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        var cmd = sent.Should().BeOfType<AuthorizePurchaseCreditNoteCommand>().Which;
        cmd.PurchaseCreditNoteId.Should().Be(id);
        cmd.ClientRequestId.Should().Be(clientRequestId);
    }

    [Fact]
    public async Task Authorize_que_excede_saldo_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<PurchaseCreditNoteDto>.ValidationFailure(
                "No puedes autorizar esta nota de crédito porque el total excede el saldo pendiente."
            )
        );

        var response = await controller.Authorize(
            Guid.NewGuid(),
            new AuthorizePurchaseCreditNoteRequest(Guid.NewGuid()),
            CancellationToken.None
        );

        response
            .Should()
            .BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should()
            .Be(422);
    }

    // ── POST /purchases/credit-notes/{id}/cancel ────────────────────────

    [Fact]
    public async Task Cancel_exitoso_retorna_200()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteDto>.Success(SampleDto(id));
        });

        var response = await controller.Cancel(
            id,
            new CancelPurchaseCreditNoteRequest("Motivo", Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<CancelPurchaseCreditNoteCommand>();
    }

    // ── POST /purchases/credit-notes/{id}/link-return ───────────────────

    [Fact]
    public async Task LinkReturn_exitoso_retorna_200_y_mapea_el_request_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseCreditNoteDto>.Success(SampleDto(id));
        });
        var purchaseReturnId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        var response = await controller.LinkReturn(
            id,
            new LinkPurchaseCreditNoteToReturnRequest(purchaseReturnId, clientRequestId),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        var cmd = sent.Should().BeOfType<LinkPurchaseCreditNoteToReturnCommand>().Which;
        cmd.PurchaseCreditNoteId.Should().Be(id);
        cmd.PurchaseReturnId.Should().Be(purchaseReturnId);
        cmd.ClientRequestId.Should().Be(clientRequestId);
    }

    [Fact]
    public async Task LinkReturn_con_tipo_Discount_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Solo una nota de crédito de tipo Devolución se puede vincular a una devolución de compra."
            )
        );

        var response = await controller.LinkReturn(
            Guid.NewGuid(),
            new LinkPurchaseCreditNoteToReturnRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None
        );

        response
            .Should()
            .BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should()
            .Be(422);
    }
}
