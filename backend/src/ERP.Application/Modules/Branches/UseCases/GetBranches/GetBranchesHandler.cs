using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetBranches;

public sealed class GetBranchesHandler
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetBranchesHandler(IBranchRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<BranchDto>>> HandleAsync(
        bool? activeFilter,
        string? search,
        CancellationToken ct = default)
    {
        var items = await _repo.GetAsync(_tenant.TenantId, activeFilter, search, ct);
        var dtos = items.Select(x => new BranchDto(
            x.Id,
            x.Name,
            x.Address,
            x.Reference,
            x.Phones,
            x.CountryId,
            x.ProvinceId,
            x.CantonId,
            x.ParishId,
            x.Latitude,
            x.Longitude,
            x.RechargeOption,
            x.IsActive,
            x.IsMainBranch)).ToList();

        return Result<IReadOnlyList<BranchDto>>.Success(dtos);
    }
}
