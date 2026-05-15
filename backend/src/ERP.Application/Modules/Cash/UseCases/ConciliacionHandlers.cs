using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.Services;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record SugerirReconciliationQuery(Guid ExtractoId)
    : IRequest<Result<IReadOnlyList<SugerenciaReconciliationDto>>>;

public sealed class SugerirReconciliationQueryHandler
    : IRequestHandler<SugerirReconciliationQuery, Result<IReadOnlyList<SugerenciaReconciliationDto>>>
{
    private readonly IReconciliationService _svc;

    public SugerirReconciliationQueryHandler(IReconciliationService svc) => _svc = svc;

    public Task<Result<IReadOnlyList<SugerenciaReconciliationDto>>> Handle(
        SugerirReconciliationQuery request,
        CancellationToken ct)
        => _svc.SugerirConciliacionAsync(request.ExtractoId, ct);
}

public sealed record ConciliarBankTransactionCommand(Guid MovimientoId, Guid JournalEntryId)
    : IRequest<Result<bool>>;

public sealed class ConciliarBankTransactionCommandHandler
    : IRequestHandler<ConciliarBankTransactionCommand, Result<bool>>
{
    private readonly IReconciliationService _svc;

    public ConciliarBankTransactionCommandHandler(IReconciliationService svc) => _svc = svc;

    public Task<Result<bool>> Handle(ConciliarBankTransactionCommand request, CancellationToken ct)
        => _svc.ConciliarMovimientoAsync(request.MovimientoId, request.JournalEntryId, ct);
}
