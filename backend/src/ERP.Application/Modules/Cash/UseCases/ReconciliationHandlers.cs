using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.Services;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record SuggestReconciliationQuery(Guid StatementId)
    : IRequest<Result<IReadOnlyList<ReconciliationSuggestionDto>>>, ICompanyScopedRequest;

public sealed class SuggestReconciliationQueryHandler
    : IRequestHandler<SuggestReconciliationQuery, Result<IReadOnlyList<ReconciliationSuggestionDto>>>
{
    private readonly IReconciliationService _svc;

    public SuggestReconciliationQueryHandler(IReconciliationService svc) => _svc = svc;

    public Task<Result<IReadOnlyList<ReconciliationSuggestionDto>>> Handle(
        SuggestReconciliationQuery request,
        CancellationToken ct)
        => _svc.SugerirConciliacionAsync(request.StatementId, ct);
}

public sealed record ReconcileBankTransactionCommand(Guid MovimientoId, Guid JournalEntryId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class ReconcileBankTransactionCommandHandler
    : IRequestHandler<ReconcileBankTransactionCommand, Result<bool>>
{
    private readonly IReconciliationService _svc;

    public ReconcileBankTransactionCommandHandler(IReconciliationService svc) => _svc = svc;

    public Task<Result<bool>> Handle(ReconcileBankTransactionCommand request, CancellationToken ct)
        => _svc.ConciliarMovimientoAsync(request.MovimientoId, request.JournalEntryId, ct);
}
