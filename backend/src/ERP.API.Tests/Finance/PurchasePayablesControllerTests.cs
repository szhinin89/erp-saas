using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Finance;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — contrato de PurchasePayablesController con
/// StubMediator, mismo alcance que FinancePaymentsControllerTests. Antes de este controller no
/// existía forma de consultar CxP pendientes desde la aplicación.
/// </summary>
public sealed class PurchasePayablesControllerTests
{
    private static PurchasePayablesController BuildController(Func<object, object> handler)
    {
        var controller = new PurchasePayablesController(new StubMediator(handler));
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

    private static PurchasePayableDto SampleDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proveedor de Prueba S.A.",
            100m,
            0m,
            0m,
            100m,
            "pending",
            0,
            Array.Empty<PurchasePayableInstallmentDto>(),
            DateTime.UtcNow,
            null
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Fact]
    public void GetById_exige_perm_purchases_view()
    {
        var method = typeof(PurchasePayablesController).GetMethod(
            nameof(PurchasePayablesController.GetById)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{PurchasePermissions.View}");
    }

    [Fact]
    public void GetList_exige_perm_purchases_view()
    {
        var method = typeof(PurchasePayablesController).GetMethod(
            nameof(PurchasePayablesController.GetList)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{PurchasePermissions.View}");
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
            return Result<PurchasePayableDto>.Success(SampleDto(id));
        });

        var response = await controller.GetById(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetPayableByIdQuery(id));
    }

    [Fact]
    public async Task GetById_de_CxP_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<PurchasePayableDto>.NotFound("Cuenta por pagar no encontrada.")
        );

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GET / (listado) ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_retorna_200_y_envia_los_parametros_de_paginado_correctos()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PayablesListResponse>.Success(
                new PayablesListResponse(Array.Empty<PurchasePayableDto>(), 0, 2, 10)
            );
        });

        var response = await controller.GetList(
            status: "pending",
            pageNumber: 2,
            pageSize: 10,
            ct: CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetPayablesListQuery("pending", null, 2, 10));
    }

    [Fact]
    public async Task GetList_usa_valores_por_defecto_cuando_no_se_especifican()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PayablesListResponse>.Success(
                new PayablesListResponse(Array.Empty<PurchasePayableDto>(), 0, 1, 25)
            );
        });

        await controller.GetList(ct: CancellationToken.None);

        sentRequest.Should().Be(new GetPayablesListQuery(null, null, 1, 25));
    }
}
