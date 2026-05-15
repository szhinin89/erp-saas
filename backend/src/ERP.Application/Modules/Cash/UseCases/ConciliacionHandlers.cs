using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.Services;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record SugerirConciliacionQuery(Guid ExtractoId)
    : IRequest<Result<IReadOnlyList<SugerenciaConciliacionDto>>>;

public sealed class SugerirConciliacionQueryHandler
    : IRequestHandler<SugerirConciliacionQuery, Result<IReadOnlyList<SugerenciaConciliacionDto>>>
{
    private readonly IConciliacionService _svc;

    public SugerirConciliacionQueryHandler(IConciliacionService svc) => _svc = svc;

    public Task<Result<IReadOnlyList<SugerenciaConciliacionDto>>> Handle(
        SugerirConciliacionQuery request,
        CancellationToken ct)
        => _svc.SugerirConciliacionAsync(request.ExtractoId, ct);
}

public sealed record ConciliarMovimientoBancarioCommand(Guid MovimientoId, Guid AsientoContableId)
    : IRequest<Result<bool>>;

public sealed class ConciliarMovimientoBancarioCommandHandler
    : IRequestHandler<ConciliarMovimientoBancarioCommand, Result<bool>>
{
    private readonly IConciliacionService _svc;

    public ConciliarMovimientoBancarioCommandHandler(IConciliacionService svc) => _svc = svc;

    public Task<Result<bool>> Handle(ConciliarMovimientoBancarioCommand request, CancellationToken ct)
        => _svc.ConciliarMovimientoAsync(request.MovimientoId, request.AsientoContableId, ct);
}
