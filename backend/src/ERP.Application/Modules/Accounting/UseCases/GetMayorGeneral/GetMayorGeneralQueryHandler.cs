using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetMayorGeneral;

public sealed class GetMayorGeneralQueryHandler
    : IRequestHandler<GetMayorGeneralQuery, Result<IReadOnlyList<MayorGeneralLineDto>>>
{
    private readonly IAccountingRepository _repo;
    private readonly ICurrentSubscriber        _subscriber;

    public GetMayorGeneralQueryHandler(IAccountingRepository repo, ICurrentSubscriber subscriber)
    {
        _repo   = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<MayorGeneralLineDto>>> Handle(
        GetMayorGeneralQuery query, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;

        var lines = await _repo.GetMayorGeneralLinesAsync(
            subscriberId, query.AccountId, query.Desde, query.Hasta, ct);

        decimal balance = 0m;
        var result = lines.Select(l =>
        {
            balance += l.Debit - l.Credit;
            return new MayorGeneralLineDto(l.Date, l.Reference, l.Description, l.Debit, l.Credit, balance);
        }).ToList();

        return Result<IReadOnlyList<MayorGeneralLineDto>>.Success(result);
    }
}
