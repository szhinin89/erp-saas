using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;
using static ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements.GetStockMovementsQueryHandler;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexMovementDetail;

public sealed class GetKardexMovementDetailQueryHandler
    : IRequestHandler<GetKardexMovementDetailQuery, Result<KardexMovementDetailDto>>
{
    private readonly IStockRepository _stockRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly ISalesInvoiceRepository _salesRepo;
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IStockTransferRepository _transferRepo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _tenant;

    public GetKardexMovementDetailQueryHandler(
        IStockRepository stockRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        ISalesInvoiceRepository salesRepo,
        IStockAdjustmentRepository adjRepo,
        IStockTransferRepository transferRepo,
        IAccessRepository accessRepo,
        ICurrentTenant tenant
    )
    {
        _stockRepo = stockRepo;
        _purchaseRepo = purchaseRepo;
        _salesRepo = salesRepo;
        _adjRepo = adjRepo;
        _transferRepo = transferRepo;
        _accessRepo = accessRepo;
        _tenant = tenant;
    }

    public async Task<Result<KardexMovementDetailDto>> Handle(
        GetKardexMovementDetailQuery request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var movement = await _stockRepo.GetMovementByIdAsync(tid, request.MovementId, ct);
        if (movement is null)
            return Result<KardexMovementDetailDto>.NotFound("Movimiento de Kardex no encontrado.");

        var sourceDoc = await ResolveSourceDocumentAsync(
            tid,
            movement.CompanyId,
            movement.SourceDocType,
            movement.SourceDocId,
            movement.ProductId,
            movement.WarehouseId,
            ct
        );

        var user = await _accessRepo.GetUserByIdAsync(movement.CreatedBy, ct);
        var actor = new KardexActorDto(
            movement.CreatedBy,
            user is not null ? $"{user.FirstName} {user.LastName}".Trim() : "Usuario desconocido"
        );

        var documentChain = BuildDocumentChain(movement, sourceDoc);

        var previous = await _stockRepo.GetPreviousMovementAsync(
            tid,
            movement.CompanyId,
            movement.ProductId,
            movement.WarehouseId,
            movement.SequenceNumber,
            ct
        );
        var next = await _stockRepo.GetNextMovementAsync(
            tid,
            movement.CompanyId,
            movement.ProductId,
            movement.WarehouseId,
            movement.SequenceNumber,
            ct
        );

        var relations = new KardexMovementRelationsDto(
            Current: ToRelatedRef(movement),
            Previous: previous is null ? null : ToRelatedRef(previous),
            Next: next is null ? null : ToRelatedRef(next)
        );

        return Result<KardexMovementDetailDto>.Success(
            new KardexMovementDetailDto(ToDto(movement), sourceDoc, actor, documentChain, relations)
        );
    }

    private static KardexDocumentChainDto BuildDocumentChain(
        StockMovement movement,
        KardexSourceDocumentDto? sourceDoc
    )
    {
        if (movement.SourceDocId is null || string.IsNullOrWhiteSpace(movement.SourceDocType))
            return new KardexDocumentChainDto(Array.Empty<KardexDocumentChainLinkDto>());

        var link = new KardexDocumentChainLinkDto(
            RelationType: "SourceDocument",
            DocType: movement.SourceDocType,
            DocNumber: sourceDoc?.DocNumber,
            DocId: movement.SourceDocId,
            IsCurrent: true
        );

        return new KardexDocumentChainDto(new[] { link });
    }

    private static KardexRelatedMovementRef ToRelatedRef(StockMovement m) =>
        new(m.Id, m.SequenceNumber, m.MovementType.ToString(), m.EffectiveDate);

    private async Task<KardexSourceDocumentDto?> ResolveSourceDocumentAsync(
        Guid tenantId,
        Guid companyId,
        string? sourceDocType,
        Guid? sourceDocId,
        Guid productId,
        Guid warehouseId,
        CancellationToken ct
    )
    {
        if (sourceDocId is null || string.IsNullOrWhiteSpace(sourceDocType))
            return null;

        switch (sourceDocType)
        {
            case "PurchaseInvoice":
                {
                    var inv = await _purchaseRepo.GetByIdAsync(tenantId, sourceDocId.Value, ct);
                    if (inv is null)
                        return null;
                    var line =
                        inv.Lines.FirstOrDefault(l =>
                            l.ItemId == productId
                            && (l.WarehouseId is null || l.WarehouseId == warehouseId)
                        ) ?? inv.Lines.FirstOrDefault(l => l.ItemId == productId);
                    return new KardexSourceDocumentDto(
                        "PurchaseInvoice",
                        inv.InvoiceNumber,
                        inv.SupplierName,
                        line?.UnitPrice,
                        line?.DiscountPct,
                        line?.VatCode,
                        line?.VatRate,
                        null,
                        null
                    );
                }
            case "SalesInvoice":
                {
                    var inv = await _salesRepo.GetByIdAsync(tenantId, sourceDocId.Value, ct);
                    if (inv is null)
                        return null;
                    var line =
                        inv.Lines.FirstOrDefault(l =>
                            l.ItemId == productId && l.WarehouseId == warehouseId
                        ) ?? inv.Lines.FirstOrDefault(l => l.ItemId == productId);
                    return new KardexSourceDocumentDto(
                        "SalesInvoice",
                        inv.InvoiceNumber,
                        inv.Customer.Name,
                        line?.UnitPrice,
                        line?.DiscountPct,
                        line?.VatCode,
                        line?.VatRate,
                        null,
                        null
                    );
                }
            case "StockAdjustment":
                {
                    var adj = await _adjRepo.GetByIdAsync(tenantId, sourceDocId.Value, ct);
                    if (adj is null)
                        return null;
                    return new KardexSourceDocumentDto(
                        "StockAdjustment",
                        adj.AdjustmentNumber,
                        null,
                        null,
                        null,
                        null,
                        null,
                        adj.MovementType,
                        adj.Notes
                    );
                }
            case "StockTransfer":
                {
                    var tr = await _transferRepo.GetByIdAsync(
                        tenantId,
                        companyId,
                        sourceDocId.Value,
                        ct
                    );
                    if (tr is null)
                        return null;
                    return new KardexSourceDocumentDto(
                        "StockTransfer",
                        tr.TransferNumber,
                        null,
                        null,
                        null,
                        null,
                        null,
                        tr.Reason,
                        tr.Notes
                    );
                }
            default:
                return null;
        }
    }
}
