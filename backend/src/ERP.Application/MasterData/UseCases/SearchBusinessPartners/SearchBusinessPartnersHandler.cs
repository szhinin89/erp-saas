using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

public sealed class SearchBusinessPartnersHandler
    : IRequestHandler<SearchBusinessPartnersQuery, Result<PagedResult<BusinessPartnerSummaryDto>>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;

    public SearchBusinessPartnersHandler(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo
    ) => (_bpRepo, _roleRepo) = (bpRepo, roleRepo);

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

        // Una sola query extra para toda la página (no N+1) — ver
        // ZH-MASTERDATA-PARTNER-SEARCH-ROLE-FLAGS-API-07.
        var roleFlags = await _roleRepo.GetActiveRoleFlagsByBpIdsAsync(
            items.Select(x => x.Id),
            cancellationToken
        );

        var dtos = items
            .Select(bp =>
            {
                var flags = roleFlags.TryGetValue(bp.Id, out var f) ? f : (false, false);
                return BusinessPartnerSummaryDto.From(bp, flags.Item1, flags.Item2);
            })
            .ToList();
        return Result<PagedResult<BusinessPartnerSummaryDto>>.Success(
            new PagedResult<BusinessPartnerSummaryDto>(dtos, pageNumber, take, total)
        );
    }
}
