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
    private readonly ICurrentTenant _t;

    public GetPayableByIdHandler(IPurchasePayableRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchasePayableDto>> Handle(
        GetPayableByIdQuery q,
        CancellationToken ct
    )
    {
        var p = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return p is null
            ? Result<PurchasePayableDto>.NotFound("Cuenta por pagar no encontrada.")
            : Result<PurchasePayableDto>.Success(MapDto(p));
    }

    internal static PurchasePayableDto MapDto(
        Domain.Modules.Purchases.Entities.PurchasePayable p
    ) =>
        new(
            p.Id,
            p.PurchaseId,
            p.SupplierId,
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
    private readonly ICurrentTenant _t;

    public GetPayablesListHandler(IPurchasePayableRepository repo, ICurrentTenant t)
    {
        _repo = repo;
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
        var dtos = items.Select(GetPayableByIdHandler.MapDto).ToList();
        return Result<PayablesListResponse>.Success(
            new PayablesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
