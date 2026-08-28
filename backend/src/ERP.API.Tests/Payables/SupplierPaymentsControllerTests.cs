using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — contrato de <c>SupplierPaymentsController</c> con StubMediator,
/// mismo patrón que <c>PayablesControllerTests</c>. Único endpoint de escritura: registrar/confirmar
/// un pago a proveedor en una sola operación (sin Draft).
/// </summary>
public sealed class SupplierPaymentsControllerTests
{
    private static SupplierPaymentsController BuildController(Func<object, object> handler)
    {
        var controller = new SupplierPaymentsController(new StubMediator(handler));
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
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static RegisterSupplierPaymentRequest SampleRequest(Guid supplierId, Guid installmentId) =>
        new(
            supplierId,
            new DateOnly(2026, 8, 28),
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(Guid.NewGuid(), Guid.NewGuid(), 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(installmentId, 300m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
        );

    private static SupplierPaymentDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 28),
            300m,
            "00000001",
            null,
            "00000001",
            "Confirmed",
            Array.Empty<SupplierPaymentMethodLineDto>(),
            Array.Empty<SupplierPaymentApplicationLineDto>(),
            Array.Empty<SupplierPaymentAllocationLineDto>(),
            DateTime.UtcNow
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Fact]
    public void Register_exige_perm_supplier_payments_create()
    {
        var method = typeof(SupplierPaymentsController).GetMethod(
            nameof(SupplierPaymentsController.Register)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{SupplierPaymentsPermissions.Create}");
    }

    // ── POST / ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_retorna_201_y_mapea_la_request_al_command_correcto()
    {
        var supplierId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var request = SampleRequest(supplierId, installmentId);
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SupplierPaymentDto>.Success(SampleDto(Guid.NewGuid()));
        });

        var response = await controller.Register(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sentRequest
            .Should()
            .Be(
                new RegisterSupplierPaymentCommand(
                    request.SupplierId,
                    request.PaymentDate,
                    request.TotalAmount,
                    request.ReceiptNumber,
                    request.MethodLines,
                    request.ApplicationLines,
                    request.Allocations
                )
            );
    }

    [Fact]
    public async Task Register_con_error_de_validacion_retorna_422()
    {
        var request = SampleRequest(Guid.NewGuid(), Guid.NewGuid());
        var controller = BuildController(_ =>
            Result<SupplierPaymentDto>.ValidationFailure("El pago no está balanceado.")
        );

        var response = await controller.Register(request, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Register_con_receipt_number_duplicado_retorna_409()
    {
        var request = SampleRequest(Guid.NewGuid(), Guid.NewGuid());
        var controller = BuildController(_ =>
            Result<SupplierPaymentDto>.Conflict("Ya existe un pago con ese número de comprobante.")
        );

        var response = await controller.Register(request, CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>();
    }

    // ── GET /{id} y GET / ────────────────────────────────────────────────────

    [Fact]
    public void GetById_exige_perm_supplier_payments_view()
    {
        var method = typeof(SupplierPaymentsController).GetMethod(
            nameof(SupplierPaymentsController.GetById)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{SupplierPaymentsPermissions.View}");
    }

    [Fact]
    public void GetList_exige_perm_supplier_payments_view()
    {
        var method = typeof(SupplierPaymentsController).GetMethod(
            nameof(SupplierPaymentsController.GetList)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{SupplierPaymentsPermissions.View}");
    }

    [Fact]
    public async Task GetById_retorna_200_y_envia_la_query_correcta()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SupplierPaymentDto>.Success(SampleDto(id));
        });

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetSupplierPaymentByIdQuery(id));
    }

    [Fact]
    public async Task GetById_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<SupplierPaymentDto>.NotFound("Pago a proveedor no encontrado.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetList_usa_valores_por_defecto_cuando_no_se_especifican()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SupplierPaymentsListResponse>.Success(
                new SupplierPaymentsListResponse(Array.Empty<SupplierPaymentListItemDto>(), 0, 1, 25)
            );
        });

        await controller.GetList(ct: CancellationToken.None);

        sentRequest.Should().Be(new GetSupplierPaymentsListQuery());
    }

    [Fact]
    public async Task GetList_envia_filtros_de_supplierId_y_status()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SupplierPaymentsListResponse>.Success(
                new SupplierPaymentsListResponse(Array.Empty<SupplierPaymentListItemDto>(), 0, 2, 10)
            );
        });

        var supplierId = Guid.NewGuid();

        var response = await controller.GetList(
            supplierId: supplierId,
            status: "Confirmed",
            page: 2,
            pageSize: 10,
            ct: CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest
            .Should()
            .Be(new GetSupplierPaymentsListQuery(supplierId, "Confirmed", 2, 10));
    }
}
