using ERP.Application.Common;
using ERP.Application.MasterData;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed class SearchBusinessPartnersHandler
    : IRequestHandler<SearchBusinessPartnersQuery, Result<PagedResult<BusinessPartnerDto>>>
{
    private readonly IBusinessPartnerRepository _repo;
    private readonly IBusinessPartnerOperationalLinkEnricher _linkEnricher;

    public SearchBusinessPartnersHandler(
        IBusinessPartnerRepository repo,
        IBusinessPartnerOperationalLinkEnricher linkEnricher)
    {
        _repo = repo;
        _linkEnricher = linkEnricher;
    }

    public async Task<Result<PagedResult<BusinessPartnerDto>>> Handle(
        SearchBusinessPartnersQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Take, 1, 200);
        var pageNumber = take > 0 ? (request.Skip / take) + 1 : 1;

        var resultsTask = _repo.SearchAsync(request.Query, request.IsActive, request.IsCustomer, request.IsSupplier, request.Skip, take, ct);
        var totalTask = _repo.CountAsync(request.Query, request.IsActive, request.IsCustomer, request.IsSupplier, ct);
        await Task.WhenAll(resultsTask, totalTask);
        var results = resultsTask.Result;
        var total = totalTask.Result;

        var enriched = await _linkEnricher.EnrichAsync(results, ct);
        return Result<PagedResult<BusinessPartnerDto>>.Success(
            new PagedResult<BusinessPartnerDto>(enriched, pageNumber, take, total));
    }
}
