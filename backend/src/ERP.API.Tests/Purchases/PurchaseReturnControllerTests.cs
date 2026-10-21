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
    [Fact]
    public void Historial_no_expone_mutaciones_ni_comandos_independientes_publicos()
    {
        var methods = typeof(PurchaseReturnController).GetMethods();
        methods.Should().NotContain(m => m.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0
            || m.GetCustomAttributes(typeof(HttpPutAttribute), true).Length > 0
            || m.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0);
        var exported = typeof(CreateDraftPurchaseCreditNoteCommand).Assembly.GetExportedTypes();
        exported.Should().NotContain(t => t.Name == "CreatePurchaseReturnDraftCommand"
            || t.Name == "RegisterAndLinkSupplierCreditNoteCommand"
            || t.Name == "AuthorizePurchaseReturnCommand"
            || t.Name == "CancelPurchaseReturnCommand"
            || t.Name == "LinkPurchaseCreditNoteToReturnCommand");
    }
    private static PurchaseReturnController BuildController(Func<object, object> handler)
    {
        var controller = new PurchaseReturnController(new StubMediator(handler));
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
    [InlineData(nameof(PurchaseReturnController.GetById), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseReturnController.GetList), PurchasePermissions.View)]
    [InlineData(nameof(PurchaseReturnController.GetReturnableLines), PurchasePermissions.View)]
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





    // ── PUT /purchases/returns/{id} ─────────────────────────────────────────





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
        var controller = BuildController(_ =>
            Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /purchases/returns ───────────────────────────────────────────────

    [Fact]
    public async Task GetList_retorna_200_con_el_listado_paginado()
    {
        var controller = BuildController(_ =>
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
        var controller = BuildController(_ =>
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
        var controller = BuildController(_ =>
            Result<IReadOnlyList<ReturnableLineDto>>.NotFound("Factura de compra no encontrada.")
        );

        var response = await controller.GetReturnableLines(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /purchases/returns/{id}/authorize ──────────────────────────────





    // ── POST /purchases/returns/{id}/cancel ─────────────────────────────────





    // ── POST /purchases/returns/{id}/credit-note ────────────────────────────




}
