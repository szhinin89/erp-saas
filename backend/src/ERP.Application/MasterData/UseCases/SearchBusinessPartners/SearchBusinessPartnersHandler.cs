using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed class SearchBusinessPartnersHandler
    : IRequestHandler<SearchBusinessPartnersQuery, Result<PagedResult<BusinessPartnerSummaryDto>>>
{
    private readonly IBusinessPartnerRepository _bpRepo;

    public SearchBusinessPartnersHandler(IBusinessPartnerRepository bpRepo) => _bpRepo = bpRepo;

    public async Task<Result<PagedResult<BusinessPartnerSummaryDto>>> Handle(
        SearchBusinessPartnersQuery q,
        CancellationToken cancellationToken
    )
    {
        var take = Math.Clamp(q.Take, 1, 200);
        var pageNumber = take > 0 ? (q.Skip / take) + 1 : 1;

        var items = await _bpRepo.SearchAsync(
            q.Query,
            q.IsActive,
            q.Roles,
            q.Skip,
            take,
            cancellationToken
        );
        var total = await _bpRepo.CountAsync(q.Query, q.IsActive, q.Roles, cancellationToken);

        var dtos = items.Select(BusinessPartnerSummaryDto.From).ToList();
        return Result<PagedResult<BusinessPartnerSummaryDto>>.Success(
            new PagedResult<BusinessPartnerSummaryDto>(dtos, pageNumber, take, total)
        );
    }
}
