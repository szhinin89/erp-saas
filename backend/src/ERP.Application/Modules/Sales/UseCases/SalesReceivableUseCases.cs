using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record SalesReceivableDto(
    Guid Id,
    Guid InvoiceId,
    Guid CustomerId,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    string Status,
    int InstallmentCount,
    IReadOnlyList<SalesReceivableInstallmentDto> Installments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record SalesReceivableInstallmentDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    string Status
);

// ── Queries ─────────────────────────────────────────────────────────────

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto (defensa en profundidad) — SalesReceivable
/// deriva de SalesInvoice, que tampoco tiene BranchId de cabecera, así que esto no filtra
/// resultados por sucursal, solo exige sucursal activa autorizada.
/// </summary>
public sealed record GetReceivableByInvoiceQuery(Guid InvoiceId)
    : IRequest<Result<SalesReceivableDto>>,
        IBranchScopedRequest;

public sealed record GetReceivablesListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<ReceivablesListResponse>>, IBranchScopedRequest;

public sealed record ReceivablesListResponse(
    IReadOnlyList<SalesReceivableDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetReceivableByInvoiceHandler
    : IRequestHandler<GetReceivableByInvoiceQuery, Result<SalesReceivableDto>>
{
    private readonly ISalesReceivableRepository _repo;
    private readonly ICurrentTenant _t;

    public GetReceivableByInvoiceHandler(ISalesReceivableRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesReceivableDto>> Handle(
        GetReceivableByInvoiceQuery q,
        CancellationToken ct
    )
    {
        var r = await _repo.GetByInvoiceIdAsync(_t.TenantId, q.InvoiceId, ct);
        return r is null
            ? Result<SalesReceivableDto>.NotFound("Cuenta por cobrar no encontrada.")
            : Result<SalesReceivableDto>.Success(MapDto(r));
    }

    internal static SalesReceivableDto MapDto(Domain.Modules.Sales.Entities.SalesReceivable r) =>
        new(
            r.Id,
            r.InvoiceId,
            r.CustomerId,
            r.OriginalAmount,
            r.PaidAmount,
            r.BalanceDue,
            r.Status,
            r.Installments.Count,
            r.Installments.Select(i => new SalesReceivableInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.DueDate,
                    i.Amount,
                    i.PaidAmount,
                    i.Status
                ))
                .ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );
}

public sealed class GetReceivablesListHandler
    : IRequestHandler<GetReceivablesListQuery, Result<ReceivablesListResponse>>
{
    private readonly ISalesReceivableRepository _repo;
    private readonly ICurrentTenant _t;

    public GetReceivablesListHandler(ISalesReceivableRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<ReceivablesListResponse>> Handle(
        GetReceivablesListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var dtos = items.Select(GetReceivableByInvoiceHandler.MapDto).ToList();
        return Result<ReceivablesListResponse>.Success(
            new ReceivablesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
