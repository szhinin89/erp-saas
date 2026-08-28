using ERP.Application.Common;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — expone la CxP de Compras para que el usuario pueda
/// consultar/seleccionar qué cuenta por pagar liquidar. PAYABLES-PURCHASE-MIGRATION-10: el nombre
/// del tipo y sus campos se conservan tal cual (contrato de API estable, consumido por
/// frontend/finance) aunque el origen real ahora es <see cref="AccountsPayable"/> genérico filtrado
/// por <see cref="AccountsPayableOriginType.PurchaseInvoice"/> — nunca <c>PurchasePayable</c>
/// (eliminado).
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
    private readonly IAccountsPayableRepository _repo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPayableByIdHandler(
        IAccountsPayableRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _t = t;
        _c = c;
    }

    public async Task<Result<PurchasePayableDto>> Handle(
        GetPayableByIdQuery q,
        CancellationToken ct
    )
    {
        var p = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (p is null || p.OriginType != AccountsPayableOriginType.PurchaseInvoice)
            return Result<PurchasePayableDto>.NotFound("Cuenta por pagar no encontrada.");

        var names = await _invoiceRepo.GetSupplierNamesByIdsAsync(_t.TenantId, [p.OriginId], ct);
        return Result<PurchasePayableDto>.Success(
            MapDto(p, names.GetValueOrDefault(p.OriginId, string.Empty))
        );
    }

    internal static PurchasePayableDto MapDto(AccountsPayable p, string supplierName) =>
        new(
            p.Id,
            p.OriginId,
            p.SupplierId,
            supplierName,
            p.TotalAmount,
            p.PaidAmount,
            p.RetainedAmount,
            p.OutstandingAmount,
            p.Status.ToString().ToLowerInvariant(),
            p.Installments.Count,
            p.Installments.Select(i => new PurchasePayableInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DueDate,
                    i.Amount,
                    i.PaidAmount,
                    i.Status.ToString().ToLowerInvariant()
                ))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt
        );
}

public sealed class GetPayablesListHandler
    : IRequestHandler<GetPayablesListQuery, Result<PayablesListResponse>>
{
    private readonly IAccountsPayableRepository _repo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPayablesListHandler(
        IAccountsPayableRepository repo,
        IPurchaseInvoiceRepository invoiceRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _invoiceRepo = invoiceRepo;
        _t = t;
        _c = c;
    }

    public async Task<Result<PayablesListResponse>> Handle(
        GetPayablesListQuery q,
        CancellationToken ct
    )
    {
        AccountsPayableStatus? status = null;
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<AccountsPayableStatus>(q.Status.Trim(), ignoreCase: true, out var parsed))
            status = parsed;

        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            _c.CompanyId,
            AccountsPayableOriginType.PurchaseInvoice,
            status,
            q.SupplierId,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var names = await _invoiceRepo.GetSupplierNamesByIdsAsync(
            _t.TenantId,
            items.Select(x => x.OriginId).Distinct().ToList(),
            ct
        );
        var dtos = items
            .Select(p => GetPayableByIdHandler.MapDto(p, names.GetValueOrDefault(p.OriginId, string.Empty)))
            .ToList();
        return Result<PayablesListResponse>.Success(
            new PayablesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
