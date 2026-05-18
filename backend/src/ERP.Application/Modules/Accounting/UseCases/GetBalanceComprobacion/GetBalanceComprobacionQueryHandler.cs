using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetBalanceComprobacion;

public sealed class GetBalanceComprobacionQueryHandler
    : IRequestHandler<GetBalanceComprobacionQuery, Result<IReadOnlyList<BalanceComprobacionLineDto>>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentTenant        _tenant;

    public GetBalanceComprobacionQueryHandler(IAccountingRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<BalanceComprobacionLineDto>>> Handle(
        GetBalanceComprobacionQuery query, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;

        var accounts = await _repo.GetAllByTenantAsync(tenantId, ct);
        var totals   = await _repo.GetBalanceComprobacionAsync(tenantId, query.Desde, query.Hasta, ct);

        var accountMap = accounts.ToDictionary(a => a.Id);
        var totalsMap  = totals.ToDictionary(t => t.AccountId);

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
