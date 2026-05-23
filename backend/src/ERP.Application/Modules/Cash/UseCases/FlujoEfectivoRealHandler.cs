using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record GetFlujoEfectivoRealQuery(DateTime DateFrom, DateTime DateTo)
    : IRequest<Result<IReadOnlyList<DailyCashFlowDto>>>, ICompanyScopedRequest;

public sealed class GetFlujoEfectivoRealQueryHandler
    : IRequestHandler<GetFlujoEfectivoRealQuery, Result<IReadOnlyList<DailyCashFlowDto>>>
{
    private readonly ICashRepository _caja;
    private readonly IAccountingRepository _accounting;
    private readonly ICurrentSubscriber _tenant;

    public GetFlujoEfectivoRealQueryHandler(
        ICashRepository caja,
        IAccountingRepository accounting,
        ICurrentSubscriber tenant)
    {
        _caja        = caja;
        _accounting = accounting;
        _tenant     = tenant;
    }

    public async Task<Result<IReadOnlyList<DailyCashFlowDto>>> Handle(
        GetFlujoEfectivoRealQuery request,
        CancellationToken ct)
    {
        var ids = new HashSet<Guid>();

        foreach (var c in await _caja.ListBankAccountsAsync(ct))
        {
            if (c.LedgerAccountId is { } bid)
                ids.Add(bid);
        }

        foreach (var c in await _caja.ListPettyCashesAsync(ct))
        {
            if (c.LedgerAccountId is { } cid)
                ids.Add(cid);
        }

        if (ids.Count == 0)
        {
            return Result<IReadOnlyList<DailyCashFlowDto>>.Success(
                Array.Empty<DailyCashFlowDto>());
        }

        var lines = await _accounting.GetPostedLineAmountsByAccountsAsync(
            _tenant.SubscriberId,
            ids.ToList(),
            request.DateFrom,
            request.DateTo,
            ct);

        var porDia = lines
            .GroupBy(x => DateOnly.FromDateTime(x.EntryDate))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var entradas = g.Sum(x => x.Credit);
                var salidas  = g.Sum(x => x.Debit);
                var neto     = entradas - salidas;
                return new DailyCashFlowDto(g.Key, entradas, salidas, neto);
            })
            .ToList();

        return Result<IReadOnlyList<DailyCashFlowDto>>.Success(porDia);
    }
}
