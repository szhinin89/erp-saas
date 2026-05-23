using ERP.Application.Common;
using ERP.Application.MasterData;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed class SearchBusinessPartnersHandler
    : IRequestHandler<SearchBusinessPartnersQuery, Result<IReadOnlyList<BusinessPartnerDto>>>
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

    public async Task<Result<IReadOnlyList<BusinessPartnerDto>>> Handle(
        SearchBusinessPartnersQuery request, CancellationToken ct)
    {
        var results = await _repo.SearchAsync(request.Query, request.IsActive, request.IsCustomer, request.IsSupplier, request.Skip, request.Take, ct);
        var enriched = await _linkEnricher.EnrichAsync(results, ct);
        return Result<IReadOnlyList<BusinessPartnerDto>>.Success(enriched);
    }
}
