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
/// P0-02 Fase 11 — contrato de <c>PurchaseReturnController</c> con <c>StubMediator</c>, mismo
/// alcance que <c>FinancePaymentsControllerTests</c>: mapeo de Command/Query a HTTP y
/// verificación por reflexión de la policy declarada (§20.2: reutiliza <see cref="PurchasePermissions"/>,
/// sin permisos nuevos). Controller delgado — la lógica ya está probada en Fases 5-10.
/// </summary>
public sealed class PurchaseReturnControllerTests
{
    private static PurchaseReturnController BuildController(Func<object, object> handler)
    {
        var controller = new PurchaseReturnController(new StubMediator(handler));
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

    private static PurchaseReturnDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "00000001",
            "Producto defectuoso",
            "Authorized",
            "PendingSupplierCreditNote",
            null,
            100m,
            12m,
            0m,
            0m,
            112m,
            DateTime.UtcNow,
            null,
            null,
            new List<PurchaseReturnDetailDto>(),
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa — 8 endpoints, §20.2 sin permisos nuevos ──────

    [Theory]
    [InlineData(nameof(PurchaseReturnController.CreateDraft), PurchasePermissions.Create)]
    [InlineData(nameof(PurchaseReturnController.UpdateDraft), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseReturnController.GetById), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseReturnController.GetList), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseReturnController.GetReturnableLines), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseReturnController.Authorize), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseReturnController.Cancel), PurchasePermissions.Update)]
    [InlineData(nameof(PurchaseReturnController.LinkCreditNote), PurchasePermissions.Create)]
    public void Endpoint_exige_la_policy_de_PurchasePermissions_esperada(
        string methodName,
        string expectedPermission
    )
    {
        var method = typeof(PurchaseReturnController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{expectedPermission}");
    }

    // ── POST /purchases/returns ─────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseReturnDto>.Success(SampleDto(id));
        });
        var command = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Producto defectuoso",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1m) }
        );

        var response = await controller.CreateDraft(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sent.Should().Be(command);
    }

    [Fact]
    public async Task CreateDraft_sobre_factura_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<PurchaseReturnDto>.NotFound("Factura de compra no encontrada.")
        );
        var command = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1m) }
        );

        var response = await controller.CreateDraft(command, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PUT /purchases/returns/{id} ─────────────────────────────────────────

    [Fact]
    public async Task UpdateDraft_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseReturnDto>.Success(SampleDto(id));
        });
        var request = new UpdatePurchaseReturnDraftRequest(
            "Motivo actualizado",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 2m) }
        );

        var response = await controller.UpdateDraft(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<UpdatePurchaseReturnDraftCommand>();
        ((UpdatePurchaseReturnDraftCommand)sent!).Id.Should().Be(id);
    }

    [Fact]
    public async Task UpdateDraft_sobre_devolucion_ya_autorizada_retorna_422()
    {
        var controller = BuildController(
            _ => Result<PurchaseReturnDto>.ValidationFailure("Esta devolución ya no está en borrador.")
        );
        var request = new UpdatePurchaseReturnDraftRequest(
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1m) }
        );

        var response = await controller.UpdateDraft(Guid.NewGuid(), request, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── GET /purchases/returns/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GetById_existente_retorna_200()
    {
        var id = Guid.NewGuid();
        var controller = BuildController(_ => Result<PurchaseReturnDto>.Success(SampleDto(id)));

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /purchases/returns ───────────────────────────────────────────────

    [Fact]
    public async Task GetList_retorna_200_con_el_listado_paginado()
    {
        var controller = BuildController(
            _ =>
                Result<PurchaseReturnListResultDto>.Success(
                    new PurchaseReturnListResultDto(new List<PurchaseReturnDto>(), 0, 1, 20)
                )
        );

        var response = await controller.GetList(null, 1, 20, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    // ── GET /purchases/invoices/{invoiceId}/returnable-lines ────────────────

    [Fact]
    public async Task GetReturnableLines_retorna_200_con_las_lineas_devolvibles()
    {
        var controller = BuildController(
            _ =>
                Result<IReadOnlyList<ReturnableLineDto>>.Success(
                    new List<ReturnableLineDto>
                    {
                        new(Guid.NewGuid(), Guid.NewGuid(), "Producto 1", 10m, 3m, 7m, Guid.NewGuid()),
                    }
                )
        );

        var response = await controller.GetReturnableLines(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetReturnableLines_sobre_factura_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<IReadOnlyList<ReturnableLineDto>>.NotFound("Factura de compra no encontrada.")
        );

        var response = await controller.GetReturnableLines(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /purchases/returns/{id}/authorize ──────────────────────────────

    [Fact]
    public async Task Authorize_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseReturnDto>.Success(SampleDto(id));
        });

        var response = await controller.Authorize(
            id,
            new AuthorizePurchaseReturnRequest(Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<AuthorizePurchaseReturnCommand>();
        ((AuthorizePurchaseReturnCommand)sent!).PurchaseReturnId.Should().Be(id);
    }

    [Fact]
    public async Task Authorize_con_stock_insuficiente_retorna_422()
    {
        var controller = BuildController(
            _ => Result<PurchaseReturnDto>.ValidationFailure("Stock insuficiente en la bodega original.")
        );

        var response = await controller.Authorize(
            Guid.NewGuid(),
            new AuthorizePurchaseReturnRequest(Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── POST /purchases/returns/{id}/cancel ─────────────────────────────────

    [Fact]
    public async Task Cancel_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PurchaseReturnDto>.Success(SampleDto(id));
        });

        var response = await controller.Cancel(
            id,
            new CancelPurchaseReturnRequest("Ya no aplica", Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<CancelPurchaseReturnCommand>();
        ((CancelPurchaseReturnCommand)sent!).PurchaseReturnId.Should().Be(id);
    }

    [Fact]
    public async Task Cancel_con_credito_aplicado_retorna_422_PR_011()
    {
        var controller = BuildController(
            _ =>
                Result<PurchaseReturnDto>.ValidationFailure(
                    "No se puede cancelar esta devolución porque su crédito de proveedor ya tiene aplicaciones o reembolsos activos."
                )
        );

        var response = await controller.Cancel(
            Guid.NewGuid(),
            new CancelPurchaseReturnRequest("Motivo", Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── POST /purchases/returns/{id}/credit-note ────────────────────────────

    [Fact]
    public async Task LinkCreditNote_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<SupplierCreditNoteLinkDto>.Success(
                new SupplierCreditNoteLinkDto(
                    id,
                    "SupplierCreditNoteRegistered",
                    Guid.NewGuid(),
                    "AK-1",
                    "001-001-000000099",
                    100m,
                    "USD"
                )
            );
        });
        var request = new LinkSupplierCreditNoteRequest(
            "AK-1",
            "1791352688001",
            "Proveedor Test",
            "001-001-000000099",
            new DateOnly(2026, 6, 15),
            100m,
            0m,
            100m,
            "USD",
            Guid.NewGuid()
        );

        var response = await controller.LinkCreditNote(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<RegisterAndLinkSupplierCreditNoteCommand>();
        ((RegisterAndLinkSupplierCreditNoteCommand)sent!).PurchaseReturnId.Should().Be(id);
    }

    [Fact]
    public async Task LinkCreditNote_ya_vinculada_retorna_422_SC_009()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "Esta devolución ya tiene una Nota de Crédito vinculada."
                )
        );
        var request = new LinkSupplierCreditNoteRequest(
            "AK-1",
            "1791352688001",
            "Proveedor Test",
            "001-001-000000099",
            new DateOnly(2026, 6, 15),
            100m,
            0m,
            100m,
            "USD",
            Guid.NewGuid()
        );

        var response = await controller.LinkCreditNote(Guid.NewGuid(), request, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }
}
