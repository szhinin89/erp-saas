using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetPurchaseReturnByIdQuery(Guid Id)
    : IRequest<Result<PurchaseReturnDto>>,
        IBranchScopedRequest;

public sealed record GetPurchaseReturnListQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PurchaseReturnListResultDto>>,
        IBranchScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPurchaseReturnByIdHandler
    : IRequestHandler<GetPurchaseReturnByIdQuery, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _repo;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ICurrentTenant _t;

    public GetPurchaseReturnByIdHandler(
        IPurchaseReturnRepository repo,
        IItemRepository itemRepo,
        IWarehouseRepository warehouseRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _receptionRepo = receptionRepo;
        _t = t;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        GetPurchaseReturnByIdQuery q,
        CancellationToken ct
    )
    {
        var purchaseReturn = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (purchaseReturn is null)
            return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");

        var dto = await EnrichAsync(Map.ToDto(purchaseReturn), ct);
        return Result<PurchaseReturnDto>.Success(dto);
    }

    /// <summary>
    /// PURCHASE-RETURN-DETAIL-DISPLAY-NAMES-01 — solo lectura, nunca falla la respuesta si algo no
    /// se puede resolver (ítem/bodega eliminados, documento de NC no encontrado): esos campos
    /// simplemente quedan en null y el frontend cae de vuelta al Id crudo. No toca ningún campo de
    /// negocio de <c>purchaseReturn</c>.
    /// </summary>
    private async Task<PurchaseReturnDto> EnrichAsync(PurchaseReturnDto dto, CancellationToken ct)
    {
        var tenantId = _t.TenantId;

        var itemIds = dto.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = itemIds.Count == 0
            ? []
            : await _itemRepo.GetByIdsLightAsync(itemIds, tenantId, ct);
        var itemsById = items.ToDictionary(
            i => i.Id,
            (Item i) => (Sku: i.Code.SKU, Name: i.Code.ShortName)
        );

        var warehouseIds = dto.Lines.Select(l => l.WarehouseId).Distinct().ToList();
        var warehouseNamesById = new Dictionary<Guid, string>();
        foreach (var warehouseId in warehouseIds)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(tenantId, warehouseId, ct);
            if (warehouse is not null)
                warehouseNamesById[warehouseId] = warehouse.Name;
        }

        var enrichedLines = dto
            .Lines.Select(l =>
                l with
                {
                    ItemSku = itemsById.TryGetValue(l.ItemId, out var item) ? item.Sku : null,
                    ItemName = itemsById.TryGetValue(l.ItemId, out var item2) ? item2.Name : null,
                    WarehouseName = warehouseNamesById.GetValueOrDefault(l.WarehouseId),
                }
            )
            .ToList();

        string? creditNoteInvoiceNumber = null;
        string? creditNoteAccessKey = null;
        if (dto.SupplierCreditNoteDocumentId is { } documentId)
        {
            var document = await _receptionRepo.GetByIdAsync(tenantId, documentId, ct);
            creditNoteInvoiceNumber = document?.InvoiceNumber;
            creditNoteAccessKey = document?.AccessKey;
        }

        return dto with
        {
            Lines = enrichedLines,
            SupplierCreditNoteInvoiceNumber = creditNoteInvoiceNumber,
            SupplierCreditNoteAccessKey = creditNoteAccessKey,
        };
    }
}

public sealed class GetPurchaseReturnListHandler
    : IRequestHandler<GetPurchaseReturnListQuery, Result<PurchaseReturnListResultDto>>
{
    private readonly IPurchaseReturnRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchaseReturnListHandler(IPurchaseReturnRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseReturnListResultDto>> Handle(
        GetPurchaseReturnListQuery q,
        CancellationToken ct
    )
    {
        var page = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var (items, total) = await _repo.GetPagedAsync(_t.TenantId, q.Status, page, pageSize, ct);

        return Result<PurchaseReturnListResultDto>.Success(
            new PurchaseReturnListResultDto(items.Select(Map.ToDto).ToList(), total, page, pageSize)
        );
    }
}
