using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.UpdateBranch;

public sealed class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Result<BranchDto>>
{
    private readonly IBranchRepository _repo;
    private readonly IGeographyReadRepository _geo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public UpdateBranchCommandHandler(
        IBranchRepository repo,
        IGeographyReadRepository geo,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user)
    {
        _repo = repo;
        _geo = geo;
        _activity = activity;
        _subscriber = subscriber;
        _user = user;
    }

    public async Task<Result<BranchDto>> Handle(UpdateBranchCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId = _user.UserId;

        var entity = await _repo.GetByIdAsync(subscriberId, command.Id, ct);
        if (entity is null)
            return Result<BranchDto>.Failure("Sucursal no encontrada.");

        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Address))
            return Result<BranchDto>.Failure("Nombre y dirección son obligatorios.");

        var locErr = await BranchLocationValidation.ValidateAsync(
            _geo,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            ct);
        if (locErr is not null)
            return Result<BranchDto>.Failure(locErr);

        if (command.IsMainBranch)
            await _repo.ClearMainBranchExceptAsync(subscriberId, command.Id, userId, ct);

        entity.Update(
            command.Name,
            command.Address,
            command.BranchType,
            command.Reference,
            command.Phones,
            command.Email,
            command.ManagerName,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            command.Latitude,
            command.Longitude,
            command.StorageCapacity,
            command.DailySalesGoal,
            command.RechargeOption,
            command.IsMainBranch,
            userId);

        if (command.IsActive && !entity.IsActive)
            entity.Enable(userId);
        else if (!command.IsActive && entity.IsActive)
            entity.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _user.Email,
            _user.FullName,
            module: "branches",
            action: "branch.update",
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
