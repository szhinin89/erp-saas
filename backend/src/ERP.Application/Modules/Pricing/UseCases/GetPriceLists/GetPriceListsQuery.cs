using MediatR;
using ERP.Application.Common;
using ERP.Application.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Application.Pricing.UseCases.GetPriceLists;

public sealed record GetPriceListsQuery
    : IRequest<Result<IReadOnlyList<PriceListDto>>>, ICompanyScopedRequest;

public sealed class GetPriceListsQueryHandler
    : IRequestHandler<GetPriceListsQuery, Result<IReadOnlyList<PriceListDto>>>
{
    private readonly IPriceListRepository _repository;

    public GetPriceListsQueryHandler(IPriceListRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<PriceListDto>>> Handle(
        GetPriceListsQuery query, CancellationToken ct)
    {
        var lists = await _repository.GetAllByCompanyAsync(ct);

        var dtos = lists.Select(pl => new PriceListDto(
            pl.Id, pl.Code, pl.Name, pl.CurrencyCode,
            pl.PriceIncludesVat, pl.ValidFrom, pl.ValidTo,
            pl.IsDefault, pl.Target.ToString(), pl.IsActive, pl.CreatedAt)).ToList();

        return Result<IReadOnlyList<PriceListDto>>.Success(dtos);
    }
}
