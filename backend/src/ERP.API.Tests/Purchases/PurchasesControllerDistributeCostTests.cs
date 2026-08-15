using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// PURCHASE-COSTTYPE-ENUM-CONTRACT-CLEANUP-01 — PurchasesController.DistributeCost sigue recibiendo
/// CostType como string en el body (contrato HTTP sin cambios), pero ahora lo convierte a
/// PurchaseCostType con Enum.TryParse antes de construir el comando. Estos tests cubren: (1) los dos
/// valores válidos actuales del frontend llegan al mediator ya como enum, (2) un valor inválido
/// devuelve un BadRequest amigable sin invocar el mediator (nunca el error genérico de deserialización
/// JSON que se tendría si CostType fuera enum directo en el DTO del request).
/// </summary>
public sealed class PurchasesControllerDistributeCostTests
{
    private static PurchasesController BuildController(Func<object, object> handler)
    {
        var controller = new PurchasesController(new StubMediator(handler));
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

    [Theory]
    [InlineData("Freight", PurchaseCostType.Freight)]
    [InlineData("OtherCost", PurchaseCostType.OtherCost)]
    public async Task DistributeCost_convierte_el_string_del_payload_al_enum_esperado(
        string costTypeInPayload,
        PurchaseCostType expected
    )
    {
        DistributePurchaseCostCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (DistributePurchaseCostCommand)req;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var response = await controller.DistributeCost(
            invoiceId,
            new DistributeCostRequest(costTypeInPayload, 10m, new List<Guid> { lineId }),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.CostType.Should().Be(expected);
        captured.InvoiceId.Should().Be(invoiceId);
        captured.IncludedLineIds.Should().Equal(lineId);
    }

    [Theory]
    [InlineData("freight")] // minúsculas — case-sensitive a propósito, no coincide con el enum
    [InlineData("Flete")]
    [InlineData("")]
    [InlineData("Other")]
    public async Task DistributeCost_con_CostType_invalido_devuelve_BadRequest_amigable_sin_llamar_al_mediator(
        string invalidCostType
    )
    {
        var mediatorCalled = false;
        var controller = BuildController(req =>
        {
            mediatorCalled = true;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });

        var response = await controller.DistributeCost(
            Guid.NewGuid(),
            new DistributeCostRequest(invalidCostType, 10m, new List<Guid> { Guid.NewGuid() }),
            CancellationToken.None
        );

        response.Should().BeOfType<BadRequestObjectResult>();
        mediatorCalled.Should().BeFalse();
    }
}
