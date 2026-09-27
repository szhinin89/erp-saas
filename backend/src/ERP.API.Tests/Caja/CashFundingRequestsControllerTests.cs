using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-API-02E-D — contrato de <c>CashFundingRequestsController</c> con
/// <c>StubMediator</c>: policy declarada por endpoint (solicitante vs. cajero), mapeo de la query a
/// HTTP y re-lectura del detalle tras cada comando. El ownership se prueba en Application/PostgreSQL.
/// </summary>
public sealed class CashFundingRequestsControllerTests
{
    private static CashFundingRequestsController BuildController(Func<object, object> handler)
    {
        var controller = new CashFundingRequestsController(new StubMediator(handler));
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

    private static CashFundingRequestDto Detail(Guid id, string supplierName = "") =>
        new(id, "Pending", Guid.NewGuid(), "", Guid.NewGuid(), "", Guid.NewGuid(), Guid.NewGuid(), supplierName, 50m, 50m,
            Guid.NewGuid(), "", DateTime.UtcNow, null, null, null, null, null, null, null, [], [], false, false, false);

    [Theory]
    [InlineData(nameof(CashFundingRequestsController.Create), SupplierPaymentsPermissions.Create)]
    [InlineData(nameof(CashFundingRequestsController.GetMine), SupplierPaymentsPermissions.Create)]
    [InlineData(nameof(CashFundingRequestsController.Cancel), SupplierPaymentsPermissions.Create)]
    [InlineData(nameof(CashFundingRequestsController.GetList), CajaPermissions.FundingRequestsView)]
    [InlineData(nameof(CashFundingRequestsController.Fulfill), CajaPermissions.FundingRequestsFulfill)]
    [InlineData(nameof(CashFundingRequestsController.Reject), CajaPermissions.FundingRequestsFulfill)]
    public void Cada_endpoint_declara_su_permiso(string methodName, string expectedPermission)
    {
        var attr = typeof(CashFundingRequestsController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single();
        attr.Policy.Should().Be($"perm:{expectedPermission}");
    }

    [Fact]
    public void Detalle_solo_exige_autenticacion_y_resuelve_solicitante_o_view_en_Application()
    {
        typeof(CashFundingRequestsController)
            .GetMethod(nameof(CashFundingRequestsController.GetById))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Should()
            .BeEmpty();
        typeof(CashFundingRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy.Should()
            .BeNull();
    }

    [Fact]
    public async Task Create_mapea_el_pago_y_devuelve_201_con_el_detalle_releido()
    {
        var id = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        CreateCashFundingRequestCommand? sent = null;
        var reread = false;
        var controller = BuildController(req =>
        {
            switch (req)
            {
                case CreateCashFundingRequestCommand c:
                    sent = c;
                    return Result<CashFundingRequestDto>.Success(Detail(id), ApiResponseCodes.Common.Created);
                case GetCashFundingRequestByIdQuery q when q.Id == id:
                    reread = true;
                    return Result<CashFundingRequestDto>.Success(Detail(id, "Proveedor"));
                default:
                    throw new InvalidOperationException(req.GetType().Name);
            }
        });
        var body = new CreateCashFundingRequestRequest(Guid.NewGuid(), new DateOnly(2026, 9, 17), 50m, "R-1",
            [new SupplierPaymentMethodLineRequest(Guid.NewGuid(), null, Guid.NewGuid(), 50m)], null, null, clientRequestId);

        var response = await controller.Create(body, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        reread.Should().BeTrue();
        sent!.ClientRequestId.Should().Be(clientRequestId);
        (sent.Payment.SupplierId, sent.Payment.TotalAmount, sent.Payment.ReceiptNumber).Should().Be((body.SupplierId, 50m, "R-1"));
        sent.Payment.ApplicationLines.Should().BeEmpty();
    }

    [Fact]
    public async Task Comando_sin_acceso_al_detalle_conserva_la_respuesta_del_comando()
    {
        var id = Guid.NewGuid();
        var controller = BuildController(req => req switch
        {
            FulfillCashFundingRequestCommand => Result<CashFundingRequestDto>.Success(Detail(id)),
            GetCashFundingRequestByIdQuery => Result<CashFundingRequestDto>.NotFound("Solicitud de efectivo no encontrada."),
            _ => throw new InvalidOperationException(req.GetType().Name),
        });

        (await controller.Fulfill(id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Rechazo_de_dominio_se_mapea_a_422_sin_releer()
    {
        var controller = BuildController(req => req switch
        {
            RejectCashFundingRequestCommand => Result<CashFundingRequestDto>.ValidationFailure("La caja de la solicitud ya no está abierta."),
            _ => throw new InvalidOperationException(req.GetType().Name),
        });

        (await controller.Reject(Guid.NewGuid(), new CashFundingRequestReasonRequest("x"), CancellationToken.None))
            .Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Cancel_ajena_se_mapea_a_403()
    {
        var controller = BuildController(req => req switch
        {
            CancelCashFundingRequestCommand => Result<CashFundingRequestDto>.Forbidden("Solo quien solicitó el efectivo puede cancelar la solicitud."),
            _ => throw new InvalidOperationException(req.GetType().Name),
        });

        var response = await controller.Cancel(Guid.NewGuid(), new CashFundingRequestReasonRequest("x"), CancellationToken.None);
        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Detalle_inaccesible_es_404()
    {
        var controller = BuildController(_ => Result<CashFundingRequestDto>.NotFound("Solicitud de efectivo no encontrada."));
        (await controller.GetById(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Listados_envian_los_filtros_y_mine_no_acepta_solicitante_del_cliente()
    {
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<PagedResult<CashFundingRequestListItemDto>>.Success(new([], 2, 10, 0));
        });
        var register = Guid.NewGuid();
        var requester = Guid.NewGuid();

        (await controller.GetList("Pending", register, requester, 2, 10, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        sent.Should().Be(new GetCashFundingRequestListQuery("Pending", register, requester, 2, 10));

        (await controller.GetMine("Rejected", 3, 5, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        sent.Should().Be(new GetMyCashFundingRequestsQuery("Rejected", 3, 5));
        typeof(GetMyCashFundingRequestsQuery).GetProperties().Select(p => p.Name).Should().NotContain("RequestedByUserId");
    }
}
