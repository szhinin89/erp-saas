using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Finance;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — contrato de FinancePaymentsController con
/// StubMediator, mismo alcance que CompanyUserBranchesControllerTests: mapeo de Command a HTTP y
/// verificación por reflexión de la policy declarada. Antes de este controller, ni
/// RegisterCollectionCommand ni RegisterPaymentCommand tenían ningún endpoint que los invocara.
/// </summary>
public sealed class FinancePaymentsControllerTests
{
    private static FinancePaymentsController BuildController(Func<object, object> handler)
    {
        var controller = new FinancePaymentsController(new StubMediator(handler));
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

    private static PaymentDto SamplePaymentDto(Guid documentId) =>
        new(
            Guid.NewGuid(),
            "Collection",
            Guid.NewGuid(),
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            "Applied",
            DateTime.UtcNow,
            null,
            null,
            new[] { new PaymentApplicationLineDto(Guid.NewGuid(), documentId, null, null, 100m) },
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Fact]
    public void RegisterCollection_exige_perm_finance_create()
    {
        var method = typeof(FinancePaymentsController).GetMethod(
            nameof(FinancePaymentsController.RegisterCollection)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{FinancePermissions.Create}");
    }

    [Fact]
    public void RegisterPayment_exige_perm_finance_create()
    {
        var method = typeof(FinancePaymentsController).GetMethod(
            nameof(FinancePaymentsController.RegisterPayment)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{FinancePermissions.Create}");
    }

    // ── POST /collections ───────────────────────────────────────────────────

    [Fact]
    public async Task RegisterCollection_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        var documentId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PaymentDto>.Success(SamplePaymentDto(documentId));
        });
        var command = new RegisterCollectionCommand(
            Guid.NewGuid(),
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(documentId, null, 100m) }
        );

        var response = await controller.RegisterCollection(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sentRequest.Should().Be(command);
    }

    [Fact]
    public async Task RegisterCollection_con_monto_que_excede_el_saldo_retorna_422()
    {
        var controller = BuildController(
            _ => Result<PaymentDto>.ValidationFailure("El monto del cobro excede el saldo pendiente de la cuenta por cobrar.")
        );
        var command = new RegisterCollectionCommand(
            Guid.NewGuid(),
            999m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(Guid.NewGuid(), null, 999m) }
        );

        var response = await controller.RegisterCollection(command, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task RegisterCollection_sobre_CxC_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<PaymentDto>.NotFound("Cuenta por cobrar no encontrada.")
        );
        var command = new RegisterCollectionCommand(
            Guid.NewGuid(),
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(Guid.NewGuid(), null, 50m) }
        );

        var response = await controller.RegisterCollection(command, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /payments ───────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterPayment_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        var documentId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PaymentDto>.Success(SamplePaymentDto(documentId));
        });
        var command = new RegisterPaymentCommand(
            Guid.NewGuid(),
            100m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(documentId, null, 100m) }
        );

        var response = await controller.RegisterPayment(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sentRequest.Should().Be(command);
    }

    [Fact]
    public async Task RegisterPayment_con_monto_que_excede_el_saldo_retorna_422()
    {
        var controller = BuildController(
            _ => Result<PaymentDto>.ValidationFailure("El monto del pago excede el saldo pendiente de la cuenta por pagar.")
        );
        var command = new RegisterPaymentCommand(
            Guid.NewGuid(),
            999m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(Guid.NewGuid(), null, 999m) }
        );

        var response = await controller.RegisterPayment(command, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task RegisterPayment_sobre_CxP_inexistente_retorna_404()
    {
        var controller = BuildController(
            _ => Result<PaymentDto>.NotFound("Cuenta por pagar no encontrada.")
        );
        var command = new RegisterPaymentCommand(
            Guid.NewGuid(),
            50m,
            new DateOnly(2026, 7, 30),
            null,
            null,
            new[] { new PaymentApplicationLineInput(Guid.NewGuid(), null, 50m) }
        );

        var response = await controller.RegisterPayment(command, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }
}
