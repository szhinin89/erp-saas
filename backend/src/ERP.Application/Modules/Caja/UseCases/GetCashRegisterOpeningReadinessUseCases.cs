using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

/// <summary>
/// READINESS-2B-3 — diagnóstico previo a la apertura de la caja seleccionada, con la misma regla
/// que aplica <see cref="OpenCashSessionHandler"/> (<see cref="ICashRegisterOpeningReadinessChecker"/>).
/// Solo lectura y orientativo: la apertura lo vuelve a evaluar. Una caja de otra sucursal se trata
/// como no encontrada.
/// </summary>
public sealed record GetCashRegisterOpeningReadinessQuery(Guid CashRegisterId)
    : IRequest<Result<CashRegisterOpeningReadinessDto>>,
        IBranchScopedRequest;

public sealed class GetCashRegisterOpeningReadinessHandler
    : IRequestHandler<GetCashRegisterOpeningReadinessQuery, Result<CashRegisterOpeningReadinessDto>>
{
    private readonly ICashRegisterRepository _registers;
    private readonly IEmissionPointRepository _emissionPoints;
    private readonly ICashRegisterOpeningReadinessChecker _checker;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetCashRegisterOpeningReadinessHandler(
        ICashRegisterRepository registers,
        IEmissionPointRepository emissionPoints,
        ICashRegisterOpeningReadinessChecker checker,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _registers = registers;
        _emissionPoints = emissionPoints;
        _checker = checker;
        _t = t;
        _b = b;
    }

    public async Task<Result<CashRegisterOpeningReadinessDto>> Handle(
        GetCashRegisterOpeningReadinessQuery q,
        CancellationToken ct
    )
    {
        var register = await _registers.GetByIdAsync(_t.TenantId, q.CashRegisterId, ct);
        if (register is null || register.BranchId != _b.BranchId)
            return Result<CashRegisterOpeningReadinessDto>.NotFound("Caja no encontrada.");

        var emissionPoint = register.EmissionPointId is { } emissionPointId
            ? await _emissionPoints.GetByIdForCompanyAsync(
                _t.TenantId,
                register.CompanyId,
                emissionPointId,
                ct
            )
            : null;

        var readiness = await _checker.CheckAsync(register, emissionPoint, ct);
        var point = readiness.ActiveEmissionPoint;
        return Result<CashRegisterOpeningReadinessDto>.Success(
            new(
                register.Id,
                register.BranchId,
                point?.Id,
                point?.EmissionType.ToString(),
                readiness.CanOpen,
                readiness.FiscalStatus.ToString(),
                readiness
                    .Requirements.Select(r => new CashRegisterOpeningRequirementDto(
                        r.Code,
                        r.Severity.ToString(),
                        r.ActionTarget?.ToString()
                    ))
                    .ToList()
            )
        );
    }
}
