using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
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
using System.Reflection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// PURCHASES-RETENTIONS-CANCEL-05D — cubre el wiring HTTP de <see cref="PurchasesController.CancelRetention"/>:
/// valida que la retención pertenece realmente a la compra de la ruta ANTES de delegar en
/// <see cref="CancelRetentionCommand"/> (transversal, sin cambios), y que la policy de permiso es
/// la misma que ya protege el resto de acciones de Compras (sin permiso nuevo de Retenciones). Las
/// reglas de negocio de la cancelación en sí (CxP, posting, estados) están cubiertas en
/// <c>CancelRetentionHandlerTests</c>/<c>RetentionCancellerTests</c> — este controller es un
/// pass-through delgado con una única validación propia (pertenencia).
/// </summary>
public sealed class PurchasesControllerCancelRetentionTests
{
    private static PurchasesController BuildController(Func<object, object> handler)
    {
        var controller = new PurchasesController(new StubMediator(handler));
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

    private static RetentionDocumentDto BuildRetentionDto(Guid id, Guid purchaseInvoiceId) =>
        new(
            id, Guid.NewGuid(), Guid.NewGuid(),
            RetentionSourceDocumentType.PurchaseInvoice, purchaseInvoiceId, Guid.NewGuid(),
            Guid.NewGuid(), "001-001-000000005", new DateOnly(2026, 9, 3),
            RetentionStatus.Issued, 30m, 0m, 30m, null, null, null,
            new List<RetentionDocumentLineDto>(), "09/2026", "01", "001-001-000000123",
            new DateOnly(2026, 8, 27), null, null, 100m, 115m
        );

    private static PurchasesController BuildControllerWithFlow(
        Guid purchaseId,
        RetentionDocumentDto? existingRetention,
        Func<CancelRetentionCommand, object>? onCancel = null
    ) =>
        BuildController(req =>
        {
            if (req is GetRetentionBySourceQuery q)
            {
                q.SourceDocumentType.Should().Be(RetentionSourceDocumentType.PurchaseInvoice);
                q.SourceDocumentId.Should().Be(purchaseId);
                return Result<RetentionDocumentDto?>.Success(existingRetention);
            }
            if (req is CancelRetentionCommand cmd)
            {
                return onCancel?.Invoke(cmd)
                    ?? Result<RetentionDocumentDto>.Success(existingRetention!);
            }
            throw new InvalidOperationException($"Unexpected request: {req.GetType().Name}");
        });

    [Fact]
    public async Task CancelRetention_delega_en_CancelRetentionCommand_cuando_la_retencion_pertenece_a_la_compra()
    {
        var purchaseId = Guid.NewGuid();
        var retentionId = Guid.NewGuid();
        var existing = BuildRetentionDto(retentionId, purchaseId);
        CancelRetentionCommand? captured = null;
        var controller = BuildControllerWithFlow(
            purchaseId,
            existing,
            cmd =>
            {
                captured = cmd;
                return Result<RetentionDocumentDto>.Success(existing with { Status = RetentionStatus.Cancelled });
            }
        );

        var response = await controller.CancelRetention(
            purchaseId,
            retentionId,
            new CancelPurchaseRetentionRequest("Error en el cálculo"),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.RetentionDocumentId.Should().Be(retentionId);
        captured.Reason.Should().Be("Error en el cálculo");
    }

    [Fact]
    public async Task CancelRetention_rechaza_con_NotFound_si_la_retencion_no_pertenece_a_esa_compra()
    {
        var purchaseId = Guid.NewGuid();
        var retentionId = Guid.NewGuid();
        var otherPurchaseRetention = BuildRetentionDto(Guid.NewGuid(), Guid.NewGuid());
        var cancelCalled = false;
        var controller = BuildControllerWithFlow(
            purchaseId,
            otherPurchaseRetention, // Id distinto de retentionId
            _ =>
            {
                cancelCalled = true;
                return Result<RetentionDocumentDto>.Success(otherPurchaseRetention);
            }
        );

        var response = await controller.CancelRetention(
            purchaseId,
            retentionId,
            new CancelPurchaseRetentionRequest("Motivo"),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
        cancelCalled.Should().BeFalse("nunca debe delegar la cancelación si la retención no es la de esta compra");
    }

    [Fact]
    public async Task CancelRetention_rechaza_con_NotFound_si_la_compra_no_tiene_ninguna_retencion()
    {
        var purchaseId = Guid.NewGuid();
        var cancelCalled = false;
        var controller = BuildControllerWithFlow(
            purchaseId,
            existingRetention: null,
            onCancel: _ =>
            {
                cancelCalled = true;
                return Result<RetentionDocumentDto>.Success(null!);
            }
        );

        var response = await controller.CancelRetention(
            purchaseId,
            Guid.NewGuid(),
            new CancelPurchaseRetentionRequest("Motivo"),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
        cancelCalled.Should().BeFalse();
    }

    [Fact]
    public void CancelRetention_requiere_el_permiso_de_Compras_PurchasePermissions_Update()
    {
        var method = typeof(PurchasesController).GetMethod(nameof(PurchasesController.CancelRetention))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be($"perm:{PurchasePermissions.Update}");
    }

    [Fact]
    public void Endpoint_legacy_CancelWithholding_fue_retirado()
    {
        // PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E
        typeof(PurchasesController)
            .GetMethods()
            .Select(m => m.Name)
            .Should()
            .NotContain("CancelWithholding");
    }
}
