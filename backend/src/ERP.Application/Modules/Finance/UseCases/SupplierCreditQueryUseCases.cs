using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>P0-02 Fase 11 — resultado paginado de <c>GetSupplierCreditListQuery</c>.</summary>
public sealed record SupplierCreditListResultDto(
    IReadOnlyList<SupplierCreditDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetSupplierCreditByIdQuery(Guid Id)
    : IRequest<Result<SupplierCreditDto>>,
        ICompanyScopedRequest;

public sealed record GetSupplierCreditListQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<SupplierCreditListResultDto>>,
        ICompanyScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetSupplierCreditByIdHandler
    : IRequestHandler<GetSupplierCreditByIdQuery, Result<SupplierCreditDto>>
{
    private readonly ISupplierCreditRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSupplierCreditByIdHandler(ISupplierCreditRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SupplierCreditDto>> Handle(
        GetSupplierCreditByIdQuery q,
        CancellationToken ct
    )
    {
        var credit = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return credit is null
            ? Result<SupplierCreditDto>.NotFound("Crédito de proveedor no encontrado.")
            : Result<SupplierCreditDto>.Success(Map.ToDto(credit));
    }
}

public sealed class GetSupplierCreditListHandler
    : IRequestHandler<GetSupplierCreditListQuery, Result<SupplierCreditListResultDto>>
{
    private readonly ISupplierCreditRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSupplierCreditListHandler(ISupplierCreditRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SupplierCreditListResultDto>> Handle(
        GetSupplierCreditListQuery q,
        CancellationToken ct
    )
    {
        var page = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var (items, total) = await _repo.GetPagedAsync(_t.TenantId, page, pageSize, ct);

        return Result<SupplierCreditListResultDto>.Success(
            new SupplierCreditListResultDto(items.Select(Map.ToDto).ToList(), total, page, pageSize)
        );
    }
}
