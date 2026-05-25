using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetBranchById;

public sealed class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, Result<BranchDetailDto>>
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentSubscriber _subscriber;

    public GetBranchByIdQueryHandler(IBranchRepository repo, ICurrentSubscriber subscriber)
    {
        _repo = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<BranchDetailDto>> Handle(GetBranchByIdQuery request, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(_subscriber.SubscriberId, request.Id, ct);
        if (x is null)
            return Result<BranchDetailDto>.Failure("Sucursal no encontrada.");

        return Result<BranchDetailDto>.Success(new BranchDetailDto(
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
            x.IsMainBranch,
            x.CreatedAt,
            x.UpdatedAt,
            x.CreatedBy,
            x.UpdatedBy));
    }
}
