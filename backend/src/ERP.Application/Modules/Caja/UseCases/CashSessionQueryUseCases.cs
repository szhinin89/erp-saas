using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Queries ────────────────────────────────────────────────────────────

public sealed record GetCashSessionByIdQuery(Guid Id)
    : IRequest<Result<CashSessionDto>>,
        IBranchScopedRequest;

public sealed record GetCashSessionListQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<CashSessionListResponse>>, IBranchScopedRequest;

public sealed record GetMyCashSessionQuery()
    : IRequest<Result<CashSessionDto?>>,
        IBranchScopedRequest;

// ── Handlers ───────────────────────────────────────────────────────────

public sealed class GetCashSessionByIdHandler
    : IRequestHandler<GetCashSessionByIdQuery, Result<CashSessionDto>>
{
    private readonly ICashSessionRepository _repo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICashRegisterRepository _crRepo;
    private readonly ICurrentTenant _t;

    public GetCashSessionByIdHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _t = t;
    }

    public async Task<Result<CashSessionDto>> Handle(
        GetCashSessionByIdQuery q,
        CancellationToken ct
    )
    {
        var session = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (session is null)
            return Result<CashSessionDto>.NotFound("Sesión de caja no encontrada.");

        var ep = await _epRepo.GetByIdAsync(session.EmissionPointId, _t.TenantId, ct);
        var register = await _crRepo.GetByIdAsync(_t.TenantId, session.CashRegisterId, ct);
        return Result<CashSessionDto>.Success(
            CajaMapper.ToDto(
                session,
                ep?.EmissionType.ToString(),
                (register?.DefaultWarehouseId, register?.DefaultWarehouse?.Name),
                (register?.DefaultCustomerId, register?.DefaultCustomer?.Name.LegalName)
            )
        );
    }
}

public sealed class GetCashSessionListHandler
    : IRequestHandler<GetCashSessionListQuery, Result<CashSessionListResponse>>
{
    private readonly ICashSessionRepository _repo;
    private readonly ICurrentTenant _t;

    public GetCashSessionListHandler(ICashSessionRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<CashSessionListResponse>> Handle(
        GetCashSessionListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );

        var dtos = items.Select(CajaMapper.ToListDto).ToList();
        return Result<CashSessionListResponse>.Success(
            new CashSessionListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

public sealed class GetMyCashSessionHandler
    : IRequestHandler<GetMyCashSessionQuery, Result<CashSessionDto?>>
{
    private readonly ICashSessionRepository _repo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICashRegisterRepository _crRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public GetMyCashSessionHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<CashSessionDto?>> Handle(GetMyCashSessionQuery q, CancellationToken ct)
    {
        var session = await _repo.GetOpenByUserAsync(_t.TenantId, _u.UserId, ct);
        if (session is null)
            return Result<CashSessionDto?>.Success(null);

        var full = await _repo.GetByIdAsync(_t.TenantId, session.Id, ct);
        if (full is null)
            return Result<CashSessionDto?>.Success(null);

        var ep = await _epRepo.GetByIdAsync(full.EmissionPointId, _t.TenantId, ct);
        var register = await _crRepo.GetByIdAsync(_t.TenantId, full.CashRegisterId, ct);
        return Result<CashSessionDto?>.Success(
            CajaMapper.ToDto(
                full,
                ep?.EmissionType.ToString(),
                (register?.DefaultWarehouseId, register?.DefaultWarehouse?.Name),
                (register?.DefaultCustomerId, register?.DefaultCustomer?.Name.LegalName)
            )
        );
    }
}
