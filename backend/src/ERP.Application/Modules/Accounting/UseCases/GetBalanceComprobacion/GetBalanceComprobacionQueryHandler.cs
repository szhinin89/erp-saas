using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetBalanceComprobacion;

public sealed class GetBalanceComprobacionQueryHandler
    : IRequestHandler<GetBalanceComprobacionQuery, Result<IReadOnlyList<BalanceComprobacionLineDto>>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber        _tenant;

    public GetBalanceComprobacionQueryHandler(IAccountingRepository repo, ICurrentSubscriber tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<BalanceComprobacionLineDto>>> Handle(
        GetBalanceComprobacionQuery query, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;

        var accounts = await _repo.GetAllByTenantAsync(subscriberId, ct);
        var totals   = await _repo.GetBalanceComprobacionAsync(subscriberId, query.Desde, query.Hasta, ct);

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
