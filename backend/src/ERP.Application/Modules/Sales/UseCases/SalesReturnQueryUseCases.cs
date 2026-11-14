using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetSalesReturnByIdQuery(Guid Id)
    : IRequest<Result<SalesReturnDto>>,
        IBranchScopedRequest;

public sealed record GetSalesReturnListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<SalesReturnListResponse>>, IBranchScopedRequest;


/// <summary>
/// API-DOC-F-17: rango de paginación válido (mismo criterio que el listado de asientos). Antes un
/// <c>pageNumber &lt; 1</c> o <c>pageSize &lt; 0</c> llegaba a PostgreSQL como OFFSET/LIMIT negativo
/// y respondía error de servidor; el frontend solo envía 25 o 50.
/// </summary>
public sealed class GetSalesReturnListQueryValidator : AbstractValidator<GetSalesReturnListQuery>
{
    public GetSalesReturnListQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record SalesReturnListResponse(
    IReadOnlyList<SalesReturnListDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetSalesReturnByIdHandler
    : IRequestHandler<GetSalesReturnByIdQuery, Result<SalesReturnDto>>
{
    private readonly ISalesReturnRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSalesReturnByIdHandler(ISalesReturnRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        GetSalesReturnByIdQuery q,
        CancellationToken ct
    )
    {
        var salesReturn = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return salesReturn is null
            ? Result<SalesReturnDto>.NotFound("Devolución no encontrada.")
            : Result<SalesReturnDto>.Success(SalesReturnMapper.ToDto(salesReturn));
    }
}

public sealed class GetSalesReturnListHandler
    : IRequestHandler<GetSalesReturnListQuery, Result<SalesReturnListResponse>>
{
    private readonly ISalesReturnRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSalesReturnListHandler(ISalesReturnRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesReturnListResponse>> Handle(
        GetSalesReturnListQuery q,
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
        var dtos = items.Select(SalesReturnMapper.ToListDto).ToList();
        return Result<SalesReturnListResponse>.Success(
            new SalesReturnListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}
