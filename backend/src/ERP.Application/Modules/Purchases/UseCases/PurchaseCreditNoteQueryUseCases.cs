using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
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
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ICurrentTenant _t;

    public GetPurchaseCreditNoteByIdHandler(
        IPurchaseCreditNoteRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _receptionRepo = receptionRepo;
        _t = t;
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
                : await _invoiceRepo.GetPayableByPurchaseIdAsync(tid, invoice.Id, ct);
        var receptionDoc =
            creditNote.ReceptionDocumentId is { } receptionDocumentId
                ? await _receptionRepo.GetByIdAsync(tid, receptionDocumentId, ct)
                : null;

        return Result<PurchaseCreditNoteDto>.Success(
            CreditNoteMap.ToDto(
                creditNote,
                invoice?.InvoiceNumber,
                invoice?.SupplierName,
                payable?.BalanceDue,
                receptionDoc?.AccessKey
            )
        );
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
