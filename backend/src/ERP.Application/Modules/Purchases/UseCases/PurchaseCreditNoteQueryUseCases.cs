using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// FLOW-READY-02C.2 — devuelve cabecera, líneas, factura afectada (número/proveedor/saldo
/// pendiente actual), datos del documento de recepción vinculado (si existe) y snapshot financiero
/// (si ya fue autorizada).
/// </summary>
public sealed record GetPurchaseCreditNoteByIdQuery(Guid Id)
    : IRequest<Result<PurchaseCreditNoteDto>>,
        IBranchScopedRequest;

public sealed record GetPurchaseCreditNoteListQuery(
    string? Status = null,
    Guid? SupplierId = null,
    Guid? PurchaseInvoiceId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PurchaseCreditNoteListResultDto>>, IBranchScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPurchaseCreditNoteByIdHandler
    : IRequestHandler<GetPurchaseCreditNoteByIdQuery, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _repo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPurchaseCreditNoteByIdHandler(
        IPurchaseCreditNoteRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IPurchaseReturnRepository returnRepo,
        IItemRepository itemRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _receptionRepo = receptionRepo;
        _returnRepo = returnRepo;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _t = t;
        _c = c;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        GetPurchaseCreditNoteByIdQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var creditNote = await _repo.GetByIdAsync(tid, q.Id, ct);
        if (creditNote is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");

        var invoice = await _invoiceRepo.GetByIdAsync(tid, creditNote.PurchaseInvoiceId, ct);
        var payable =
            invoice is null
                ? null
                : await _payableRepo.GetByOriginAsync(
                    tid,
                    _c.CompanyId,
                    AccountsPayableOriginType.PurchaseInvoice,
                    invoice.Id,
                    ct
                );
        var receptionDoc =
            creditNote.ReceptionDocumentId is { } receptionDocumentId
                ? await _receptionRepo.GetByIdAsync(tid, receptionDocumentId, ct)
                : null;

        var dto = CreditNoteMap.ToDto(
            creditNote,
            invoice?.InvoiceNumber,
            invoice?.SupplierName,
            payable?.OutstandingAmount,
            receptionDoc?.AccessKey
        );

        // PURCHASE-CREDIT-NOTE-SINGLE-REVIEW-SCREEN-01 — solo lectura: el usuario no debe necesitar
        // abrir /purchases/returns/{id} para entender el caso de una NC tipo Devolución. Nunca toca
        // PurchaseReturn/inventario/CxP/contabilidad, solo lee datos ya calculados/autorizados por
        // esos agregados.
        dto = await EnrichWithLinkedReturnAsync(dto, invoice, tid, ct);

        return Result<PurchaseCreditNoteDto>.Success(dto);
    }

    private async Task<PurchaseCreditNoteDto> EnrichWithLinkedReturnAsync(
        PurchaseCreditNoteDto dto,
        Domain.Modules.Purchases.Entities.PurchaseInvoice? invoice,
        Guid tenantId,
        CancellationToken ct
    )
    {
        if (dto.LinkedPurchaseReturnId is not { } returnId)
            return dto;

        var purchaseReturn = await _returnRepo.GetByIdAsync(tenantId, returnId, ct);
        if (purchaseReturn is null)
            return dto;

        dto = dto with
        {
            LinkedPurchaseReturnNumber = purchaseReturn.ReturnNumber,
            LinkedPurchaseReturnStatus = purchaseReturn.Status.ToString(),
            LinkedPurchaseReturnAuthorizedGrandTotal = purchaseReturn.AuthorizedGrandTotal,
        };

        if (invoice is null)
            return dto;

        // Las líneas de la NC de Devolución solo guardan Description/PurchaseInvoiceDetailId — el
        // producto/bodega real vive en la PurchaseInvoiceDetail referenciada (misma resolución que
        // GetPurchaseReturnByIdHandler aplica sobre PurchaseReturnDetail).
        var invoiceLinesById = invoice.Lines.ToDictionary(l => l.Id);

        var itemIds = dto
            .Lines.Where(l => l.PurchaseInvoiceDetailId is { } id && invoiceLinesById.ContainsKey(id))
            .Select(l => invoiceLinesById[l.PurchaseInvoiceDetailId!.Value].ItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var items = itemIds.Count == 0
            ? []
            : await _itemRepo.GetByIdsLightAsync(itemIds, tenantId, ct);
        var itemsById = items.ToDictionary(
            i => i.Id,
            (Domain.Modules.Items.Entities.Item i) => (Sku: i.Code.SKU, Name: i.Code.ShortName)
        );

        var warehouseIds = dto
            .Lines.Where(l => l.PurchaseInvoiceDetailId is { } id && invoiceLinesById.ContainsKey(id))
            .Select(l => invoiceLinesById[l.PurchaseInvoiceDetailId!.Value].WarehouseId ?? invoice.GlobalWarehouseId)
            .Where(w => w is not null)
            .Select(w => w!.Value)
            .Distinct()
            .ToList();
        var warehouseNamesById = new Dictionary<Guid, string>();
        foreach (var warehouseId in warehouseIds)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(tenantId, warehouseId, ct);
            if (warehouse is not null)
                warehouseNamesById[warehouseId] = warehouse.Name;
        }

        var enrichedLines = dto
            .Lines.Select(l =>
            {
                if (l.PurchaseInvoiceDetailId is not { } detailId
                    || !invoiceLinesById.TryGetValue(detailId, out var invoiceLine))
                    return l;

                var warehouseId = invoiceLine.WarehouseId ?? invoice.GlobalWarehouseId;
                return l with
                {
                    ItemSku = invoiceLine.ItemId is { } itemId && itemsById.TryGetValue(itemId, out var item)
                        ? item.Sku
                        : null,
                    ItemName = invoiceLine.ItemId is { } itemId2 && itemsById.TryGetValue(itemId2, out var item2)
                        ? item2.Name
                        : null,
                    WarehouseName = warehouseId is { } whId ? warehouseNamesById.GetValueOrDefault(whId) : null,
                };
            })
            .ToList();

        return dto with { Lines = enrichedLines };
    }
}

public sealed class GetPurchaseCreditNoteListHandler
    : IRequestHandler<GetPurchaseCreditNoteListQuery, Result<PurchaseCreditNoteListResultDto>>
{
    private readonly IPurchaseCreditNoteRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchaseCreditNoteListHandler(IPurchaseCreditNoteRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseCreditNoteListResultDto>> Handle(
        GetPurchaseCreditNoteListQuery q,
        CancellationToken ct
    )
    {
        var page = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Status,
            q.SupplierId,
            q.PurchaseInvoiceId,
            q.DateFrom,
            q.DateTo,
            page,
            pageSize,
            ct
        );

        return Result<PurchaseCreditNoteListResultDto>.Success(
            new PurchaseCreditNoteListResultDto(
                items.Select(CreditNoteMap.ToListItemDto).ToList(),
                total,
                page,
                pageSize
            )
        );
    }
}
