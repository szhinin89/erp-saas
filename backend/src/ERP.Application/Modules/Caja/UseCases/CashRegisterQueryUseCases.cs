using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Queries ────────────────────────────────────────────────────────────

/// <summary>
/// Listado de administración de Cajas — empresa completa, todas las sucursales. Distinto de
/// <see cref="GetCashRegistersByCurrentBranchQuery"/> (usado por Apertura de Caja, branch-scoped,
/// no se toca).
/// </summary>
public sealed record GetAllCashRegistersQuery(bool? ActiveFilter = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<CashRegisterDto>>>, ICompanyScopedRequest;

/// <summary>
/// Puntos de emisión activos de una Sucursal — para poblar el selector cascada
/// Sucursal → Punto de Emisión en el formulario de administración de Cajas.
/// </summary>
public sealed record GetEmissionPointLookupsByBranchQuery(Guid BranchId)
    : IRequest<Result<IReadOnlyList<EmissionPointLookupForBranchDto>>>, ICompanyScopedRequest;

/// <summary>
/// Lista las cajas (CashRegister) de la sucursal activa. El cliente nunca envía BranchId —
/// se resuelve exclusivamente desde ICurrentBranch (IBranchScopedRequest), igual que
/// OpenCashSessionCommand resuelve BranchId/EmissionPointId server-side.
/// </summary>
public sealed record GetCashRegistersByCurrentBranchQuery(bool? ActiveOnly = null)
    : IRequest<Result<IReadOnlyList<CashRegisterDto>>>, IBranchScopedRequest;

/// <summary>
/// Solo devuelve la caja si pertenece a la sucursal activa — una caja de otra sucursal se trata
/// como no encontrada, nunca se expone su existencia fuera de su sucursal.
/// </summary>
public sealed record GetCashRegisterByIdQuery(Guid Id)
    : IRequest<Result<CashRegisterDto>>, IBranchScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetCashRegistersByCurrentBranchHandler
    : IRequestHandler<GetCashRegistersByCurrentBranchQuery, Result<IReadOnlyList<CashRegisterDto>>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly ICashRegisterUsageGuard _usageGuard;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashRegistersByCurrentBranchHandler(
        ICashRegisterRepository repo, ICashRegisterUsageGuard usageGuard, ICurrentTenant t, ICurrentBranch b)
    {
        _repo = repo; _usageGuard = usageGuard; _t = t; _b = b;
    }

    public async Task<Result<IReadOnlyList<CashRegisterDto>>> Handle(
        GetCashRegistersByCurrentBranchQuery q, CancellationToken ct)
    {
        var registers = await _repo.GetByBranchAsync(_t.TenantId, _b.BranchId, q.ActiveOnly, ct);
        var usedIds = await _usageGuard.GetUsedIdsAsync(_t.TenantId, registers.Select(r => r.Id).ToList(), ct);
        return Result<IReadOnlyList<CashRegisterDto>>.Success(
            registers.Select(r => CajaMapper.ToDto(r, usedIds.Contains(r.Id))).ToList());
    }
}

public sealed class GetCashRegisterByIdHandler
    : IRequestHandler<GetCashRegisterByIdQuery, Result<CashRegisterDto>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly ICashRegisterUsageGuard _usageGuard;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashRegisterByIdHandler(
        ICashRegisterRepository repo, ICashRegisterUsageGuard usageGuard, ICurrentTenant t, ICurrentBranch b)
    {
        _repo = repo; _usageGuard = usageGuard; _t = t; _b = b;
    }

    public async Task<Result<CashRegisterDto>> Handle(GetCashRegisterByIdQuery q, CancellationToken ct)
    {
        var register = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (register is null || register.BranchId != _b.BranchId)
            return Result<CashRegisterDto>.NotFound("Caja no encontrada.");

        var hasHistory = await _usageGuard.HasHistoryAsync(_t.TenantId, register.Id, ct);
        return Result<CashRegisterDto>.Success(CajaMapper.ToDto(register, hasHistory));
    }
}

public sealed class GetAllCashRegistersHandler
    : IRequestHandler<GetAllCashRegistersQuery, Result<IReadOnlyList<CashRegisterDto>>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly ICashRegisterUsageGuard _usageGuard;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetAllCashRegistersHandler(
        ICashRegisterRepository repo, ICashRegisterUsageGuard usageGuard, ICurrentTenant t, ICurrentCompany c)
    {
        _repo = repo; _usageGuard = usageGuard; _t = t; _c = c;
    }

    public async Task<Result<IReadOnlyList<CashRegisterDto>>> Handle(
        GetAllCashRegistersQuery q, CancellationToken ct)
    {
        var registers = await _repo.GetAllByCompanyAsync(_t.TenantId, _c.CompanyId, q.ActiveFilter, q.Search, ct);
        var usedIds = await _usageGuard.GetUsedIdsAsync(_t.TenantId, registers.Select(r => r.Id).ToList(), ct);
        return Result<IReadOnlyList<CashRegisterDto>>.Success(
            registers.Select(r => CajaMapper.ToDto(r, usedIds.Contains(r.Id))).ToList());
    }
}

public sealed class GetEmissionPointLookupsByBranchHandler
    : IRequestHandler<GetEmissionPointLookupsByBranchQuery, Result<IReadOnlyList<EmissionPointLookupForBranchDto>>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant _t;

    public GetEmissionPointLookupsByBranchHandler(IEmissionPointRepository repo, ICurrentTenant t)
    {
        _repo = repo; _t = t;
    }

    public async Task<Result<IReadOnlyList<EmissionPointLookupForBranchDto>>> Handle(
        GetEmissionPointLookupsByBranchQuery q, CancellationToken ct)
    {
        var points = await _repo.GetActiveByBranchAsync(_t.TenantId, q.BranchId, ct);
        var dtos = points
            .Select(ep => new EmissionPointLookupForBranchDto(ep.Id, ep.Code, ep.Name, ep.Establishment.Code))
            .ToList();
        return Result<IReadOnlyList<EmissionPointLookupForBranchDto>>.Success(dtos);
    }
}
