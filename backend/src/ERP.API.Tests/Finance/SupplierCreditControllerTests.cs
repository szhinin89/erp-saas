using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Finance;

/// <summary>
/// P0-02 Fase 11 — contrato de <c>SupplierCreditController</c> con <c>StubMediator</c>, mismo
/// alcance que <c>FinancePaymentsControllerTests</c>: mapeo de Command/Query a HTTP y
/// verificación por reflexión de la policy declarada (§20.2: reutiliza <see cref="FinancePermissions"/>,
/// sin permisos nuevos, separado deliberadamente de <see cref="PurchasePermissions"/>). Controller
/// delgado — la lógica ya está probada en Fases 2, 7, 8.
/// </summary>
public sealed class SupplierCreditControllerTests
{
    private static SupplierCreditController BuildController(Func<object, object> handler)
    {
        var controller = new SupplierCreditController(new StubMediator(handler));
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

    private static SupplierCreditDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "USD",
            Guid.NewGuid(),
            100m,
            100m,
            true,
            new List<SupplierCreditMovementDto>()
        );

    private static SupplierCreditRefundTransactionDto SampleRefundDto(Guid id) =>
        new(
            id,
            "Received",
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "TRANSFER",
            50m,
            "USD",
            new DateOnly(2026, 7, 1),
            null,
            null,
            null,
            null
        );

    // ── Autorización declarativa — 6 endpoints, §20.2 sin permisos nuevos ──────

    [Theory]
    [InlineData(nameof(SupplierCreditController.GetById), FinancePermissions.View)]
    [InlineData(nameof(SupplierCreditController.GetList), FinancePermissions.View)]
    [InlineData(nameof(SupplierCreditController.Apply), FinancePermissions.Update)]
    [InlineData(nameof(SupplierCreditController.ReverseApplication), FinancePermissions.Update)]
    [InlineData(nameof(SupplierCreditController.Refund), FinancePermissions.Update)]
    [InlineData(nameof(SupplierCreditController.ReverseRefund), FinancePermissions.Update)]
    public void Endpoint_exige_la_policy_de_FinancePermissions_esperada(
        string methodName,
        string expectedPermission
    )
    {
        var method = typeof(SupplierCreditController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{expectedPermission}");
    }

    // ── GET /finance/supplier-credits/{id} ──────────────────────────────────

    [Fact]
    public async Task GetById_existente_retorna_200()
    {
        var id = Guid.NewGuid();
        var controller = BuildController(_ => Result<SupplierCreditDto>.Success(SampleDto(id)));

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<SupplierCreditDto>.NotFound("Crédito de proveedor no encontrado.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET /finance/supplier-credits ───────────────────────────────────────

    [Fact]
    public async Task GetList_retorna_200_con_el_listado_paginado()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditListResultDto>.Success(
                    new SupplierCreditListResultDto(new List<SupplierCreditDto>(), 0, 1, 20)
                )
        );

        var response = await controller.GetList(1, 20, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    // ── POST /finance/supplier-credits/{id}/apply ───────────────────────────

    [Fact]
    public async Task Apply_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<SupplierCreditDto>.Success(SampleDto(id));
        });

        var response = await controller.Apply(
            id,
            new ApplySupplierCreditRequest(Guid.NewGuid(), 40m, Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<ApplySupplierCreditCommand>();
        ((ApplySupplierCreditCommand)sent!).SupplierCreditId.Should().Be(id);
    }

    [Fact]
    public async Task Apply_sobre_CxP_cancelled_retorna_422_SC_002()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditDto>.ValidationFailure(
                    "No se puede aplicar un crédito de proveedor sobre una cuenta por pagar anulada."
                )
        );

        var response = await controller.Apply(
            Guid.NewGuid(),
            new ApplySupplierCreditRequest(Guid.NewGuid(), 40m, Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Apply_sobre_credito_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<SupplierCreditDto>.NotFound("Crédito de proveedor no encontrado.")
        );

        var response = await controller.Apply(
            Guid.NewGuid(),
            new ApplySupplierCreditRequest(Guid.NewGuid(), 40m, Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /finance/supplier-credits/{id}/apply/{movementId}/reverse ─────

    [Fact]
    public async Task ReverseApplication_exitoso_retorna_200_con_Id_y_movementId_de_ruta_inyectados()
    {
        var id = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<SupplierCreditDto>.Success(SampleDto(id));
        });

        var response = await controller.ReverseApplication(
            id,
            movementId,
            new ReverseSupplierCreditApplicationRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<ReverseSupplierCreditApplicationCommand>();
        var cmd = (ReverseSupplierCreditApplicationCommand)sent!;
        cmd.SupplierCreditId.Should().Be(id);
        cmd.OriginalMovementId.Should().Be(movementId);
    }

    [Fact]
    public async Task ReverseApplication_sobre_CxP_destino_cancelled_retorna_422_SC_014()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditDto>.ValidationFailure(
                    "No se puede revertir la aplicación porque la cuenta por pagar destino ya fue anulada."
                )
        );

        var response = await controller.ReverseApplication(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ReverseSupplierCreditApplicationRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── POST /finance/supplier-credits/{id}/refund ──────────────────────────

    [Fact]
    public async Task Refund_exitoso_retorna_200_con_el_Id_de_ruta_inyectado_al_command()
    {
        var id = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<SupplierCreditRefundTransactionDto>.Success(SampleRefundDto(Guid.NewGuid()));
        });

        var response = await controller.Refund(
            id,
            new RegisterSupplierCreditRefundRequest(
                Guid.NewGuid(),
                "TRANSFER",
                50m,
                new DateOnly(2026, 7, 1),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<RegisterSupplierCreditRefundCommand>();
        ((RegisterSupplierCreditRefundCommand)sent!).SupplierCreditId.Should().Be(id);
    }

    [Fact]
    public async Task Refund_sobre_destino_financiero_inexistente_retorna_404_SC_020()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditRefundTransactionDto>.NotFound(
                    "El destino financiero indicado no existe."
                )
        );

        var response = await controller.Refund(
            Guid.NewGuid(),
            new RegisterSupplierCreditRefundRequest(
                Guid.NewGuid(),
                "TRANSFER",
                50m,
                new DateOnly(2026, 7, 1),
                null,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /finance/supplier-credits/{id}/refund/{movementId}/reverse ────

    [Fact]
    public async Task ReverseRefund_exitoso_retorna_200_con_Id_y_movementId_de_ruta_inyectados()
    {
        var id = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<SupplierCreditRefundTransactionDto>.Success(SampleRefundDto(Guid.NewGuid()));
        });

        var response = await controller.ReverseRefund(
            id,
            movementId,
            new ReverseSupplierCreditRefundRequest("Motivo", new DateOnly(2026, 7, 2), Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<ReverseSupplierCreditRefundCommand>();
        var cmd = (ReverseSupplierCreditRefundCommand)sent!;
        cmd.SupplierCreditId.Should().Be(id);
        cmd.OriginalRefundTransactionId.Should().Be(movementId);
    }

    [Fact]
    public async Task ReverseRefund_ya_revertido_retorna_422_SC_011()
    {
        var controller = BuildController(
            _ =>
                Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "Se intenta revertir un movimiento ya revertido."
                )
        );

        var response = await controller.ReverseRefund(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ReverseSupplierCreditRefundRequest("Motivo", new DateOnly(2026, 7, 2), Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }
}
