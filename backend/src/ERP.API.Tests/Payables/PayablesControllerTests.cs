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
/// PAYABLES-READ-API-11 — contrato de <c>PayablesController</c> con StubMediator, mismo patrón
/// que <c>PurchasePayablesControllerTests</c>/<c>FinancePaymentsControllerTests</c>. Sin endpoints
/// de escritura: solo lectura de la CxP genérica (Compras + Gastos).
/// </summary>
public sealed class PayablesControllerTests
{
    private static PayablesController BuildController(Func<object, object> handler)
    {
        var controller = new PayablesController(new StubMediator(handler));
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

    private static AccountsPayableListItemDto SampleListItem(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            "Proveedor de Prueba S.A.",
            "PurchaseInvoice",
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            100m,
            0m,
            100m,
            "pending"
        );

    private static AccountsPayableDetailDto SampleDetail(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            "Proveedor de Prueba S.A.",
            "PurchaseInvoice",
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            100m,
            0m,
            0m,
            0m,
            0m,
            0m,
            100m,
            "pending",
            Array.Empty<AccountsPayableInstallmentDetailDto>(),
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Fact]
    public void GetById_exige_perm_payables_view()
    {
        var method = typeof(PayablesController).GetMethod(nameof(PayablesController.GetById))!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{PayablesPermissions.View}");
    }

    [Fact]
    public void GetList_exige_perm_payables_view()
    {
        var method = typeof(PayablesController).GetMethod(nameof(PayablesController.GetList))!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{PayablesPermissions.View}");
    }

    // ── GET /{id} ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_retorna_200_y_envia_la_query_correcta()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<AccountsPayableDetailDto>.Success(SampleDetail(id));
        });

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetAccountsPayableByIdQuery(id));
    }

    [Fact]
    public async Task GetById_de_CxP_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<AccountsPayableDetailDto>.NotFound("Cuenta por pagar no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET / (listado) ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_usa_valores_por_defecto_cuando_no_se_especifican()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<AccountsPayablesListResponse>.Success(
                new AccountsPayablesListResponse(Array.Empty<AccountsPayableListItemDto>(), 0, 1, 25)
            );
        });

        await controller.GetList(ct: CancellationToken.None);

        sentRequest.Should().Be(new GetAccountsPayablesListQuery());
    }

    [Fact]
    public async Task GetList_envia_todos_los_filtros_al_query()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<AccountsPayablesListResponse>.Success(
                new AccountsPayablesListResponse(Array.Empty<AccountsPayableListItemDto>(), 0, 2, 10)
            );
        });

        var supplierId = Guid.NewGuid();
        var dueFrom = new DateOnly(2026, 8, 1);
        var dueTo = new DateOnly(2026, 8, 31);

        var response = await controller.GetList(
            supplierId: supplierId,
            originType: "PurchaseInvoice",
            status: "pending",
            dueDateFrom: dueFrom,
            dueDateTo: dueTo,
            search: "001-001",
            page: 2,
            pageSize: 10,
            ct: CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest
            .Should()
            .Be(
                new GetAccountsPayablesListQuery(
                    supplierId,
                    "PurchaseInvoice",
                    "pending",
                    dueFrom,
                    dueTo,
                    "001-001",
                    2,
                    10
                )
            );
    }

    [Fact]
    public async Task GetList_filtra_por_OriginType_ExpenseDocument()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<AccountsPayablesListResponse>.Success(
                new AccountsPayablesListResponse(
                    new[] { SampleListItem(Guid.NewGuid()) with { OriginType = "ExpenseDocument" } },
                    1,
                    1,
                    25
                )
            );
        });

        var response = await controller.GetList(originType: "ExpenseDocument", ct: CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetAccountsPayablesListQuery(OriginType: "ExpenseDocument"));
    }
}
