using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — expone <c>PurchasePayable</c> para que el usuario
/// pueda consultar/seleccionar qué cuenta por pagar liquidar. Mismo shape que
/// <c>SalesReceivableDto</c> (Sales), adaptado a los campos propios de CxP (incluye
/// <c>TotalRetained</c>, sin equivalente en CxC).
/// </summary>
public sealed record PurchasePayableDto(
    Guid Id,
    Guid PurchaseId,
    Guid SupplierId,
    string SupplierName,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal TotalRetained,
    decimal BalanceDue,
    string Status,
    int InstallmentCount,
    IReadOnlyList<PurchasePayableInstallmentDto> Installments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record PurchasePayableInstallmentDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    string Status
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetPayableByIdQuery(Guid Id)
    : IRequest<Result<PurchasePayableDto>>,
        IBranchScopedRequest;

public sealed record GetPayablesListQuery(
    string? Status = null,
    Guid? SupplierId = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<PayablesListResponse>>, IBranchScopedRequest;

public sealed record PayablesListResponse(
    IReadOnlyList<PurchasePayableDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPayableByIdHandler
    : IRequestHandler<GetPayableByIdQuery, Result<PurchasePayableDto>>
{
    private readonly IPurchasePayableRepository _repo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;

    public GetPayableByIdHandler(
        IPurchasePayableRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _t = t;
    }

    public async Task<Result<PurchasePayableDto>> Handle(
        GetPayableByIdQuery q,
        CancellationToken ct
    )
    {
        var p = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (p is null)
            return Result<PurchasePayableDto>.NotFound("Cuenta por pagar no encontrada.");

        var names = await _invoiceRepo.GetSupplierNamesByIdsAsync(_t.TenantId, [p.PurchaseId], ct);
        return Result<PurchasePayableDto>.Success(
            MapDto(p, names.GetValueOrDefault(p.PurchaseId, string.Empty))
        );
    }

    internal static PurchasePayableDto MapDto(
        Domain.Modules.Purchases.Entities.PurchasePayable p,
        string supplierName
    ) =>
        new(
            p.Id,
            p.PurchaseId,
            p.SupplierId,
            supplierName,
            p.TotalAmount,
            p.PaidAmount,
            p.TotalRetained,
            p.BalanceDue,
            p.Status,
            p.Installments.Count,
            p.Installments.Select(i => new PurchasePayableInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DueDate,
                    i.Amount,
                    i.PaidAmount,
                    i.Status
                ))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt
        );
}

public sealed class GetPayablesListHandler
    : IRequestHandler<GetPayablesListQuery, Result<PayablesListResponse>>
{
    private readonly IPurchasePayableRepository _repo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;

    public GetPayablesListHandler(
        IPurchasePayableRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _t = t;
    }

    public async Task<Result<PayablesListResponse>> Handle(
        GetPayablesListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Status,
            q.SupplierId,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var names = await _invoiceRepo.GetSupplierNamesByIdsAsync(
            _t.TenantId,
            items.Select(x => x.PurchaseId).Distinct().ToList(),
            ct
        );
        var dtos = items
            .Select(p => GetPayableByIdHandler.MapDto(p, names.GetValueOrDefault(p.PurchaseId, string.Empty)))
            .ToList();
        return Result<PayablesListResponse>.Success(
            new PayablesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
