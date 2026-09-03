using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// PAYABLES-READ-API-11 — fila de listado de la API genérica de CxP (cualquier origen). Distinto
/// de <c>PurchasePayableDto</c> (Purchases, filtrado a <see cref="AccountsPayableOriginType.PurchaseInvoice"/>
/// y con nombres de campo heredados de <c>PurchasePayable</c> por estabilidad de contrato) —
/// este DTO es el consumido por la nueva pantalla transversal de CxP. Todos los montos y
/// <see cref="Status"/> se derivan de <see cref="AccountsPayable.Installments"/> — nunca de
/// <c>PurchaseInvoice</c>/<c>ExpenseDocument</c>, que son solo el documento de origen.
/// </summary>
public sealed record AccountsPayableListItemDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string OriginType,
    Guid OriginId,
    string DocumentType,
    string DocumentNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string Status
);

/// <summary>Cuota individual dentro del detalle de una CxP — saldo propio, nunca derivado de la cabecera.</summary>
public sealed record AccountsPayableInstallmentDetailDto(
    Guid InstallmentId,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string Status
);

public sealed record AccountsPayableDetailDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string OriginType,
    Guid OriginId,
    string DocumentType,
    string DocumentNumber,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RetainedAmount,
    decimal ReturnCreditAmount,
    decimal SupplierCreditAmount,
    decimal CreditNoteAmount,
    decimal OutstandingAmount,
    string Status,
    IReadOnlyList<AccountsPayableInstallmentDetailDto> Installments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// PAYABLES-BRANCH-SCOPE-DECISION-01 — CxP es company-level, no branch-level (decisión de negocio):
/// una cuota o pago a proveedor pertenece a la empresa, no a una sucursal operativa concreta. Marcado
/// <see cref="ICompanyScopedRequest"/> (no <c>IBranchScopedRequest</c>) para no exigir sucursal activa.
/// </summary>
public sealed record GetAccountsPayableByIdQuery(Guid Id)
    : IRequest<Result<AccountsPayableDetailDto>>,
        ICompanyScopedRequest;

/// <summary>PAYABLES-BRANCH-SCOPE-DECISION-01 — ver <see cref="GetAccountsPayableByIdQuery"/>.</summary>
public sealed record GetAccountsPayablesListQuery(
    Guid? SupplierId = null,
    string? OriginType = null,
    string? Status = null,
    DateOnly? DueDateFrom = null,
    DateOnly? DueDateTo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<AccountsPayablesListResponse>>, ICompanyScopedRequest;

public sealed record AccountsPayablesListResponse(
    IReadOnlyList<AccountsPayableListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Mapeo compartido ──────────────────────────────────────────────────────

internal static class AccountsPayableDtoMapper
{
    public static AccountsPayableListItemDto ToListItem(AccountsPayable p, string supplierName) =>
        new(
            p.Id,
            p.SupplierId,
            supplierName,
            p.OriginType.ToString(),
            p.OriginId,
            p.DocumentType,
            p.DocumentNumber,
            p.IssueDate,
            p.Installments.Count == 0 ? null : p.Installments.Min(i => i.DueDate),
            p.TotalAmount,
            p.PaidAmount,
            p.OutstandingAmount,
            p.Status.ToString().ToLowerInvariant()
        );

    public static AccountsPayableDetailDto ToDetail(AccountsPayable p, string supplierName) =>
        new(
            p.Id,
            p.SupplierId,
            supplierName,
            p.OriginType.ToString(),
            p.OriginId,
            p.DocumentType,
            p.DocumentNumber,
            p.IssueDate,
            p.AccountingDate,
            p.TotalAmount,
            p.PaidAmount,
            p.RetainedAmount,
            p.ReturnCreditAmount,
            p.SupplierCreditAmount,
            p.CreditNoteAmount,
            p.OutstandingAmount,
            p.Status.ToString().ToLowerInvariant(),
            p.Installments
                .OrderBy(i => i.InstallmentNumber)
                .Select(i => new AccountsPayableInstallmentDetailDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DueDate,
                    i.Amount,
                    i.PaidAmount,
                    i.OutstandingAmount,
                    i.Status.ToString().ToLowerInvariant()
                ))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt
        );
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetAccountsPayableByIdHandler
    : IRequestHandler<GetAccountsPayableByIdQuery, Result<AccountsPayableDetailDto>>
{
    private readonly IAccountsPayableRepository _repo;
    private readonly IBusinessPartnerRepository _partners;
    private readonly ICurrentTenant _t;

    public GetAccountsPayableByIdHandler(
        IAccountsPayableRepository repo,
        IBusinessPartnerRepository partners,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _partners = partners;
        _t = t;
    }

    public async Task<Result<AccountsPayableDetailDto>> Handle(
        GetAccountsPayableByIdQuery q,
        CancellationToken ct
    )
    {
        var p = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (p is null)
            return Result<AccountsPayableDetailDto>.NotFound("Cuenta por pagar no encontrada.");

        var names = await _partners.GetNamesByIdsAsync([p.SupplierId], ct);
        return Result<AccountsPayableDetailDto>.Success(
            AccountsPayableDtoMapper.ToDetail(p, names.GetValueOrDefault(p.SupplierId, string.Empty))
        );
    }
}

public sealed class GetAccountsPayablesListHandler
    : IRequestHandler<GetAccountsPayablesListQuery, Result<AccountsPayablesListResponse>>
{
    private readonly IAccountsPayableRepository _repo;
    private readonly IBusinessPartnerRepository _partners;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAccountsPayablesListHandler(
        IAccountsPayableRepository repo,
        IBusinessPartnerRepository partners,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _partners = partners;
        _t = t;
        _c = c;
    }

    public async Task<Result<AccountsPayablesListResponse>> Handle(
        GetAccountsPayablesListQuery q,
        CancellationToken ct
    )
    {
        AccountsPayableOriginType? originType = null;
        if (
            !string.IsNullOrWhiteSpace(q.OriginType)
            && Enum.TryParse<AccountsPayableOriginType>(q.OriginType.Trim(), ignoreCase: true, out var parsedOrigin)
        )
            originType = parsedOrigin;

        AccountsPayableStatus? status = null;
        if (
            !string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<AccountsPayableStatus>(q.Status.Trim(), ignoreCase: true, out var parsedStatus)
        )
            status = parsedStatus;

        var (items, total) = await _repo.SearchAsync(
            _t.TenantId,
            _c.CompanyId,
            originType,
            status,
            q.SupplierId,
            q.DueDateFrom,
            q.DueDateTo,
            q.Search,
            q.Page,
            q.PageSize,
            ct
        );

        var names = await _partners.GetNamesByIdsAsync(
            items.Select(x => x.SupplierId).Distinct(),
            ct
        );
        var dtos = items
            .Select(p => AccountsPayableDtoMapper.ToListItem(p, names.GetValueOrDefault(p.SupplierId, string.Empty)))
            .ToList();

        return Result<AccountsPayablesListResponse>.Success(
            new AccountsPayablesListResponse(dtos, total, q.Page, q.PageSize)
        );
    }
}
