using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.GetLineMatch;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Application.Tests.Inventory.ItemMatching;

/// <summary>
/// Cubre el endpoint reutilizado por /purchases para reconstruir el estado de Item Matching de una
/// línea ya guardada (PurchaseInvoiceDetail.PurchaseReceptionLineId) al reabrir una compra —
/// mismo repositorio y mapper que ya usa Recepción, sin lógica nueva de conciliación.
/// </summary>
public sealed class GetPurchaseReceptionLineMatchHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocumentWithLine(out PurchaseReceptionLine line, string supplierCode = "PROV-001")
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice,
            "1791352688001", "Proveedor S.A.", Guid.NewGuid(),
            "clave-de-acceso-000000000000000000000000000000000000000000000",
            "001-001-000000001", new DateOnly(2026, 7, 1), null, 10m, 1.5m, 11.5m, UserId);
        line = PurchaseReceptionLine.Create(
            document.Id, TenantId, "Línea de prueba", 1m, 1m,
            vatCode: "2", taxCode: "2", vatPercentage: 15m, taxValue: 0.15m,
            discountPct: 0m, discount: 0m, lineSubtotal: 1m, totalLine: 1.15m,
            supplierCode: supplierCode);
        document.AttachSriAuthorization(
            "AUTH-1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [line], UserId,
            docTypeCode: "01", sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
        return document;
    }

    private static GetPurchaseReceptionLineMatchHandler BuildHandler(
        PurchaseReceptionDocument? document, Guid lineId,
        out Mock<IItemMatchFinder> matchFinder)
    {
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, lineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        matchFinder = new Mock<IItemMatchFinder>();
        matchFinder.Setup(m => m.FindCandidatesAsync(
                TenantId, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        return new GetPurchaseReceptionLineMatchHandler(documentRepo.Object, matchFinder.Object, tenant.Object);
    }

    [Fact]
    public async Task Handle_returns_the_current_status_of_a_resolved_line_without_calling_the_match_finder()
    {
        var document = CreateDocumentWithLine(out var line);
        var itemId = Guid.NewGuid();
        line.ManualMatch(itemId, UserId, DateTime.UtcNow);
        var handler = BuildHandler(document, line.Id, out var matchFinder);

        var result = await handler.Handle(new GetPurchaseReceptionLineMatchQuery(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ItemId.Should().Be(itemId);
        result.Value.MatchStatus.Should().Be("MANUALLY_MATCHED");
        matchFinder.Verify(m => m.FindCandidatesAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_suggestions_for_a_still_pending_line()
    {
        var document = CreateDocumentWithLine(out var line);
        var handler = BuildHandler(document, line.Id, out var matchFinder);

        var result = await handler.Handle(new GetPurchaseReceptionLineMatchQuery(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ItemId.Should().BeNull();
        result.Value.MatchStatus.Should().Be("PENDING");
        matchFinder.Verify(m => m.FindCandidatesAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_the_line_does_not_exist()
    {
        var handler = BuildHandler(null, Guid.NewGuid(), out _);

        var result = await handler.Handle(new GetPurchaseReceptionLineMatchQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
