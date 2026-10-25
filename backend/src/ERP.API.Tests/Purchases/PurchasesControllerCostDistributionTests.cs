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
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — PUT/DELETE cost-distributions: CostType llega como string
/// y se convierte con TryParse case-sensitive (más IsDefined, que rechaza valores numéricos fuera del
/// enum); un valor inválido devuelve BadRequest sin invocar el mediator. Identidad y revisión viajan
/// intactas al comando.
/// </summary>
public sealed class PurchasesControllerCostDistributionTests
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
    public async Task ReplaceCostDistribution_envia_identidad_revision_y_enum_al_comando(
        string costTypeInPayload,
        PurchaseCostType expected
    )
    {
        ReplacePurchaseCostDistributionCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (ReplacePurchaseCostDistributionCommand)req;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });
        var (invoiceId, distributionId, lineId, sourceId) =
            (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await controller.ReplaceCostDistribution(
            invoiceId,
            distributionId,
            new ReplaceCostDistributionRequest(costTypeInPayload, 3, null, [lineId], sourceId),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().Be(new ReplacePurchaseCostDistributionCommand(invoiceId, distributionId, expected, 3,
            null, captured!.TargetLineIds, sourceId));
        captured.TargetLineIds.Should().Equal(lineId);
    }

    [Theory]
    [InlineData("freight")] // minúsculas — case-sensitive a propósito, no coincide con el enum
    [InlineData("Flete")]
    [InlineData("")]
    [InlineData("Other")]
    [InlineData("99")]
    public async Task ReplaceCostDistribution_con_CostType_invalido_devuelve_BadRequest_sin_llamar_al_mediator(
        string invalidCostType
    )
    {
        var mediatorCalled = false;
        var controller = BuildController(req =>
        {
            mediatorCalled = true;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });

        var response = await controller.ReplaceCostDistribution(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ReplaceCostDistributionRequest(invalidCostType, 0, 10m, [Guid.NewGuid()]),
            CancellationToken.None
        );

        response.Should().BeOfType<BadRequestObjectResult>();
        mediatorCalled.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveCostDistribution_envia_la_revision_esperada()
    {
        RemovePurchaseCostDistributionCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (RemovePurchaseCostDistributionCommand)req;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });
        var (invoiceId, distributionId) = (Guid.NewGuid(), Guid.NewGuid());

        var response = await controller.RemoveCostDistribution(invoiceId, distributionId, 4, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().Be(new RemovePurchaseCostDistributionCommand(invoiceId, distributionId, 4));
    }
}
