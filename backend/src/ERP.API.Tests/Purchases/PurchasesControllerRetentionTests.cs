using System.Reflection;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// Wiring HTTP de la retención en Compras. ZH-PURCHASE-RETENTION-CONFIRM-01: la retención se emite
/// solo dentro de <see cref="PurchasesController.ConfirmPurchase"/> (<see cref="ConfirmPurchaseRequest.Retention"/>
/// → <see cref="ConfirmPurchaseCommand.Retention"/>); ya no existe un endpoint de emisión posterior.
/// Se verifica el mapeo ruta+body → comando y la policy de cada endpoint (todas de Compras, ninguna
/// de Gastos). Las reglas de negocio (elegibilidad, CxP, atomicidad, concurrencia) se cubren en
/// <c>ConfirmPurchaseHandlerTests</c> y en <c>PurchaseRetentionConfirmIntegrationTests</c> (PostgreSQL).
/// </summary>
public sealed class PurchasesControllerRetentionTests
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

    private static IssueRetentionLineInput VatLine() =>
        new(RetentionTaxType.Vat, "725", 100m, 30m, 30m);

    private static string? PolicyOf(string methodName) =>
        typeof(PurchasesController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>()
            ?.Policy;

    [Fact]
    public async Task ConfirmPurchase_mapea_la_intencion_de_retencion_al_comando_con_el_id_de_la_ruta()
    {
        ConfirmPurchaseCommand? captured = null;
        var controller = BuildController(req =>
        {
            captured = (ConfirmPurchaseCommand)req;
            return Result<PurchaseInvoiceDto>.Success(null!);
        });
        var purchaseInvoiceId = Guid.NewGuid();
        var intent = new RetentionIntent(
            true,
            Guid.NewGuid(),
            new DateOnly(2026, 9, 30),
            new[] { VatLine() }
        );

        var response = await controller.ConfirmPurchase(
            purchaseInvoiceId,
            new ConfirmPurchaseRequest(null, intent),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!
            .InvoiceId.Should()
            .Be(purchaseInvoiceId, "la compra siempre viene de la ruta, nunca del body");
        captured.Retention.Should().BeSameAs(intent);
    }

    [Fact]
    public async Task ConfirmPurchase_sin_body_o_sin_retencion_confirma_sin_intencion()
    {
        var captured = new List<ConfirmPurchaseCommand>();
        var controller = BuildController(req =>
        {
            captured.Add((ConfirmPurchaseCommand)req);
            return Result<PurchaseInvoiceDto>.Success(null!);
        });

        await controller.ConfirmPurchase(Guid.NewGuid(), null, CancellationToken.None);
        await controller.ConfirmPurchase(
            Guid.NewGuid(),
            new ConfirmPurchaseRequest(),
            CancellationToken.None
        );

        captured.Should().HaveCount(2).And.OnlyContain(c => c.Retention == null);
    }

    [Fact]
    public void ConfirmPurchaseRequest_nunca_expone_Tenant_Company_Branch_ni_numero_de_retencion()
    {
        var requestProperties = typeof(ConfirmPurchaseRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();
        var intentProperties = typeof(RetentionIntent)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        requestProperties.Should().NotContain(new[] { "TenantId", "CompanyId", "BranchId" });
        intentProperties
            .Should()
            .NotContain(
                new[] { "TenantId", "CompanyId", "BranchId", "RetentionNumber", "SourceDocumentId" }
            );
    }

    /// <summary>
    /// Matriz de permisos de la retención en Compras: confirmar (y por tanto emitir la retención) exige
    /// el permiso de Compras de siempre; ningún endpoint de Compras exige un permiso de Gastos.
    /// </summary>
    [Theory]
    [InlineData(nameof(PurchasesController.ConfirmPurchase), PurchasePermissions.Update)]
    [InlineData(nameof(PurchasesController.GetRetentionPreview), PurchasePermissions.View)]
    [InlineData(nameof(PurchasesController.GetRetention), PurchasePermissions.View)]
    public void Endpoints_de_retencion_de_Compras_usan_permisos_de_Compras(
        string method,
        string permission
    )
    {
        PolicyOf(method).Should().Be($"perm:{permission}");
        PolicyOf(method).Should().NotContain("expenses");
    }

    /// <summary>
    /// ZH-RETENTION-CANCELLATION-LIFECYCLE-01 — la retención solo se anula como consecuencia de
    /// anular la compra (CancelPurchase → RetentionCanceller); no existe anulación aislada que deje la
    /// compra confirmada sin la retención que se pidió al confirmarla.
    /// </summary>
    [Fact]
    public void No_existe_anulacion_aislada_de_la_retencion_de_una_compra()
    {
        typeof(PurchasesController)
            .GetMethods()
            .Select(m => m.Name)
            .Should()
            .NotContain("CancelRetention");
        typeof(PurchasesController)
            .Assembly.GetType("ERP.API.Controllers.CancelPurchaseRetentionRequest")
            .Should()
            .BeNull();
        typeof(ConfirmPurchaseCommand)
            .Assembly.GetType("ERP.Application.Modules.Retentions.UseCases.CancelRetentionCommand")
            .Should()
            .BeNull();
    }

    [Fact]
    public void La_emision_posterior_de_retencion_sobre_una_compra_confirmada_fue_retirada()
    {
        typeof(PurchasesController)
            .GetMethods()
            .Select(m => m.Name)
            .Should()
            .NotContain("IssueRetention");
        typeof(PurchasesController)
            .Assembly.GetType("ERP.API.Controllers.IssuePurchaseRetentionRequest")
            .Should()
            .BeNull();
        typeof(ConfirmPurchaseCommand)
            .Assembly.GetType("ERP.Application.Modules.Retentions.UseCases.IssueRetentionCommand")
            .Should()
            .BeNull();
    }

    [Fact]
    public void GetRetention_requiere_el_permiso_de_Compras_PurchasePermissions_View()
    {
        var method = typeof(PurchasesController).GetMethod(
            nameof(PurchasesController.GetRetention)
        )!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be($"perm:{PurchasePermissions.View}");
    }

    [Fact]
    public async Task GetRetention_construye_GetRetentionBySourceQuery_con_PurchaseInvoice_y_el_id_de_la_ruta()
    {
        GetRetentionBySourceQuery? captured = null;
        var controller = BuildController(req =>
        {
            captured = (GetRetentionBySourceQuery)req;
            return Result<RetentionDocumentDto?>.Success(null);
        });
        var purchaseInvoiceId = Guid.NewGuid();

        var response = await controller.GetRetention(purchaseInvoiceId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.SourceDocumentType.Should().Be(RetentionSourceDocumentType.PurchaseInvoice);
        captured.SourceDocumentId.Should().Be(purchaseInvoiceId);
    }

    [Fact]
    public void Endpoints_legacy_de_withholding_fueron_retirados()
    {
        // PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E — el flujo legacy IssuedWithholding fue
        // eliminado por completo: no debe quedar ningún endpoint /withholding en el controller.
        var methodNames = typeof(PurchasesController).GetMethods().Select(m => m.Name).ToArray();
        methodNames.Should().NotContain("GetWithholdingByPurchase");
        methodNames.Should().NotContain("IssueWithholding");
        methodNames.Should().NotContain("GetWithholdingById");
        methodNames.Should().NotContain("CancelWithholding");
    }
}
