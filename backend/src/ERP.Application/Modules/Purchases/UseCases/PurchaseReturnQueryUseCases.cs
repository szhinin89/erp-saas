using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Interfaces;
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
    private readonly ICurrentTenant _t;

    public GetPurchaseReturnByIdHandler(IPurchaseReturnRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        GetPurchaseReturnByIdQuery q,
        CancellationToken ct
    )
    {
        var purchaseReturn = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return purchaseReturn is null
            ? Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.")
            : Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn));
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
