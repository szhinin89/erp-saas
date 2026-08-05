using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Sales;

/// <summary>
/// Contrato de <see cref="SalesReturnController"/> con StubMediator — mismo alcance que
/// FinancePaymentsControllerTests/PurchasePayablesControllerTests: mapeo de Command/Query a HTTP
/// y verificación por reflexión de la policy declarada (reutiliza <see cref="SalesPermissions"/>,
/// sin permisos nuevos). El controller no tiene lógica propia — estos tests solo confirman el
/// mapeo HTTP↔MediatR↔Result, no repiten reglas de negocio ya cubiertas en ERP.Application.Tests.
/// </summary>
public sealed class SalesReturnControllerTests
{
    private static SalesReturnController BuildController(Func<object, object> handler)
    {
        var controller = new SalesReturnController(new StubMediator(handler));
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

    private static SalesReturnDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DEV-000001",
            "Producto en mal estado",
            "Draft",
            0m,
            0m,
            0m,
            0m,
            0m,
            Array.Empty<SalesReturnDetailDto>(),
            Array.Empty<SalesReturnRefundAllocationDto>(),
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(SalesReturnController.CreateDraft), SalesPermissions.Create)]
    [InlineData(nameof(SalesReturnController.UpdateDraft), SalesPermissions.Update)]
    [InlineData(nameof(SalesReturnController.CancelDraft), SalesPermissions.Update)]
    [InlineData(nameof(SalesReturnController.GetById), SalesPermissions.View)]
    [InlineData(nameof(SalesReturnController.GetList), SalesPermissions.View)]
    [InlineData(nameof(SalesReturnController.GetReturnableLines), SalesPermissions.View)]
    [InlineData(nameof(SalesReturnController.Authorize), SalesPermissions.Update)]
    public void Endpoint_exige_el_permiso_de_Sales_correspondiente(
        string methodName,
        string expectedPermission
    )
    {
        var method = typeof(SalesReturnController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{expectedPermission}");
    }

    // ── POST /sales/returns ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnDto>.Success(SampleDto(id));
        });
        var command = new CreateSalesReturnDraftCommand(
            Guid.NewGuid(),
            "Producto en mal estado",
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 2m) }
        );

        var response = await controller.CreateDraft(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sentRequest.Should().Be(command);
    }

    [Fact]
    public async Task CreateDraft_sobre_factura_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.NotFound("Factura no encontrada.")
        );
        var command = new CreateSalesReturnDraftCommand(
            Guid.NewGuid(),
            "Motivo",
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
        );

        var response = await controller.CreateDraft(command, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateDraft_con_cantidad_que_excede_el_remanente_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.ValidationFailure(
                "La cantidad solicitada excede el remanente devolvible."
            )
        );
        var command = new CreateSalesReturnDraftCommand(
            Guid.NewGuid(),
            "Motivo",
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 999m) }
        );

        var response = await controller.CreateDraft(command, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── PUT /sales/returns/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateDraft_exitoso_retorna_200_y_envia_el_command_recibido()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnDto>.Success(SampleDto(id));
        });
        var command = new UpdateSalesReturnDraftCommand(
            id,
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
        );

        var response = await controller.UpdateDraft(id, command, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(command);
    }

    [Fact]
    public async Task UpdateDraft_con_id_de_ruta_distinto_al_del_body_retorna_400_sin_llamar_mediator()
    {
        var called = false;
        var controller = BuildController(_ =>
        {
            called = true;
            return Result<SalesReturnDto>.Success(SampleDto(Guid.NewGuid()));
        });
        var command = new UpdateSalesReturnDraftCommand(
            Guid.NewGuid(),
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
        );

        var response = await controller.UpdateDraft(
            Guid.NewGuid(),
            command,
            CancellationToken.None
        );

        response.Should().BeOfType<BadRequestObjectResult>();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateDraft_sobre_devolucion_ya_autorizada_retorna_422()
    {
        var id = Guid.NewGuid();
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.ValidationFailure("Esta devolución ya no está en borrador.")
        );
        var command = new UpdateSalesReturnDraftCommand(
            id,
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
        );

        var response = await controller.UpdateDraft(id, command, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── DELETE /sales/returns/{id} ───────────────────────────────────────────

    [Fact]
    public async Task CancelDraft_exitoso_retorna_200_y_envia_el_id_correcto()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnDto>.Success(SampleDto(id));
        });

        var response = await controller.CancelDraft(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new CancelSalesReturnDraftCommand(id));
    }

    [Fact]
    public async Task CancelDraft_sobre_devolucion_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.NotFound("Devolución no encontrada.")
        );

        var response = await controller.CancelDraft(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /sales/returns/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetById_retorna_200_y_envia_la_query_correcta()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnDto>.Success(SampleDto(id));
        });

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetSalesReturnByIdQuery(id));
    }

    [Fact]
    public async Task GetById_de_devolucion_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.NotFound("Devolución no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /sales/returns (listado) ─────────────────────────────────────────

    [Fact]
    public async Task GetList_retorna_200_y_envia_los_filtros_correctos()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnListResponse>.Success(
                new SalesReturnListResponse(Array.Empty<SalesReturnListDto>(), 0, 2, 10)
            );
        });

        var response = await controller.GetList(
            search: "DEV-000001",
            status: "Draft",
            pageNumber: 2,
            pageSize: 10,
            ct: CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetSalesReturnListQuery("DEV-000001", "Draft", 2, 10));
    }

    [Fact]
    public async Task GetList_usa_valores_por_defecto_cuando_no_se_especifican()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnListResponse>.Success(
                new SalesReturnListResponse(Array.Empty<SalesReturnListDto>(), 0, 1, 25)
            );
        });

        await controller.GetList(ct: CancellationToken.None);

        sentRequest.Should().Be(new GetSalesReturnListQuery(null, null, 1, 25));
    }

    // ── GET /sales/invoices/{invoiceId}/returnable-lines ─────────────────────

    [Fact]
    public async Task GetReturnableLines_retorna_200_y_envia_la_query_correcta()
    {
        var invoiceId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<IReadOnlyList<ReturnableLineDto>>.Success(
                Array.Empty<ReturnableLineDto>()
            );
        });

        var response = await controller.GetReturnableLines(invoiceId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetReturnableLinesByInvoiceQuery(invoiceId));
    }

    [Fact]
    public async Task GetReturnableLines_de_factura_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<IReadOnlyList<ReturnableLineDto>>.NotFound("Factura no encontrada.")
        );

        var response = await controller.GetReturnableLines(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /sales/returns/{id}/authorize ────────────────────────────────────

    [Fact]
    public async Task Authorize_exitoso_retorna_200_y_envia_el_command_con_las_asignaciones()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SalesReturnDto>.Success(SampleDto(id));
        });
        var request = new AuthorizeSalesReturnRequest(
            new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", 23m) }
        );

        var response = await controller.Authorize(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new AuthorizeSalesReturnCommand(id, request.RefundAllocations));
    }

    [Fact]
    public async Task Authorize_sobre_devolucion_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.NotFound("Devolución no encontrada.")
        );
        var request = new AuthorizeSalesReturnRequest(
            new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", 23m) }
        );

        var response = await controller.Authorize(Guid.NewGuid(), request, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Authorize_con_asignaciones_que_no_suman_el_total_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<SalesReturnDto>.ValidationFailure(
                "El total de las asignaciones de reembolso no coincide con el total devuelto."
            )
        );
        var request = new AuthorizeSalesReturnRequest(
            new List<AuthorizeSalesReturnRefundAllocationInput> { new("Cash", 5m) }
        );

        var response = await controller.Authorize(Guid.NewGuid(), request, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }
}
