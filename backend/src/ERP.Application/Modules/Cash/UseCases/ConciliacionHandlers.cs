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

public sealed record ConciliarBankTransactionCommand(Guid MovimientoId, Guid JournalEntryId)
    : IRequest<Result<bool>>;

public sealed class ConciliarBankTransactionCommandHandler
    : IRequestHandler<ConciliarBankTransactionCommand, Result<bool>>
{
    private readonly IConciliacionService _svc;

    public ConciliarBankTransactionCommandHandler(IConciliacionService svc) => _svc = svc;

    public Task<Result<bool>> Handle(ConciliarBankTransactionCommand request, CancellationToken ct)
        => _svc.ConciliarMovimientoAsync(request.MovimientoId, request.JournalEntryId, ct);
}
