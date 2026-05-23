using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed class SearchBusinessPartnersHandler
    : IRequestHandler<SearchBusinessPartnersQuery, Result<IReadOnlyList<BusinessPartnerDto>>>
{
    private readonly IBusinessPartnerRepository _repo;

    public SearchBusinessPartnersHandler(IBusinessPartnerRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<BusinessPartnerDto>>> Handle(
        SearchBusinessPartnersQuery request, CancellationToken ct)
    {
        var results = await _repo.SearchAsync(request.Query, request.IsActive, request.Skip, request.Take, ct);
        return Result<IReadOnlyList<BusinessPartnerDto>>.Success(
            results.Select(BusinessPartnerDto.From).ToList());
    }
}
