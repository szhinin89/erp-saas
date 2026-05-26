using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetTrialBalance;

public sealed class GetTrialBalanceQueryHandler
    : IRequestHandler<GetTrialBalanceQuery, Result<IReadOnlyList<BalanceComprobacionLineDto>>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber        _subscriber;

    public GetTrialBalanceQueryHandler(IAccountingRepository repo, ICurrentSubscriber subscriber)
    {
        _repo   = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<BalanceComprobacionLineDto>>> Handle(
        GetTrialBalanceQuery query, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;

        var accounts = await _repo.GetAllBySubscriberAsync(subscriberId, ct);
        var totals   = await _repo.GetTrialBalanceAsync(subscriberId, query.Desde, query.Hasta, ct);

        var accountMap = accounts.ToDictionary(a => a.Id);

        // Only include accounts that have movements in the period, ordered by code
        var lines = totals
            .Where(t => accountMap.ContainsKey(t.AccountId))
            .Select(t =>
            {
                var acc = accountMap[t.AccountId];
                return new BalanceComprobacionLineDto(
                    AccountCode:  acc.Code.Value,
                    AccountName:  acc.Name,
                    AccountType:  acc.Type.ToString(),
                    TotalDebit:   t.TotalDebit,
                    TotalCredit:  t.TotalCredit,
                    NetBalance:   t.TotalDebit - t.TotalCredit);
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        return Result<IReadOnlyList<BalanceComprobacionLineDto>>.Success(lines);
    }
}
