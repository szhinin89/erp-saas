using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.DisableBranch;

public sealed class DisableBranchCommandHandler : IRequestHandler<DisableBranchCommand, Result<BranchDto>>
{
    private readonly IBranchRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public DisableBranchCommandHandler(
        IBranchRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _activity = activity;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<BranchDto>> Handle(DisableBranchCommand request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, request.Id, ct);
        if (entity is null)
            return Result<BranchDto>.Failure("Sucursal no encontrada.");

        entity.Disable(_user.UserId);
        await _activity.AddAsync(UserActivity.Create(
            _tenant.TenantId,
            _user.UserId,
            _user.Email,
            _user.FullName,
            module: "branches",
            action: "branch.disable",
            entityType: "Branch",
            entityId: entity.Id,
            description: entity.Name), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BranchDto>.Success(new BranchDto(
            entity.Id,
            entity.Name,
            entity.Address,
            entity.Code,
            entity.BranchType,
            entity.Reference,
            entity.Phones,
            entity.Email,
            entity.ManagerName,
            entity.CountryId,
            entity.ProvinceId,
            entity.CantonId,
            entity.ParishId,
            entity.Latitude,
            entity.Longitude,
            entity.StorageCapacity,
            entity.DailySalesGoal,
            entity.RechargeOption,
            entity.IsActive,
            entity.IsMainBranch));
    }
}
