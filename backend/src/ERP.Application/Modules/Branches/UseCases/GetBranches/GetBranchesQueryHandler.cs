using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetBranches;

public sealed class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, Result<IReadOnlyList<BranchDto>>>
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentSubscriber _subscriber;

    public GetBranchesQueryHandler(IBranchRepository repo, ICurrentSubscriber subscriber)
    {
        _repo = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAsync(_subscriber.SubscriberId, request.ActiveFilter, request.Search, ct);
        var dtos = items.Select(x => new BranchDto(
            x.Id,
            x.Name,
            x.Address,
            x.Code,
            x.BranchType,
            x.Reference,
            x.Phones,
            x.Email,
            x.ManagerName,
            x.CountryId,
            x.ProvinceId,
            x.CantonId,
            x.ParishId,
            x.Latitude,
            x.Longitude,
            x.StorageCapacity,
            x.DailySalesGoal,
            x.RechargeOption,
            x.IsActive,
            x.IsMainBranch)).ToList();

        return Result<IReadOnlyList<BranchDto>>.Success(dtos);
    }
}
