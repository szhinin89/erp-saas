using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.UpdateBranch;

public sealed class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Result<BranchListItemDto>>
{
    private readonly IBranchRepository _repo;
    private readonly IGeographyReadRepository _geo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateBranchCommandHandler(
        IBranchRepository repo,
        IGeographyReadRepository geo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _geo = geo;
        _activity = activity;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<BranchListItemDto>> Handle(UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _user.UserId;

        var entity = await _repo.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result<BranchListItemDto>.Failure("Sucursal no encontrada.");

        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Address))
            return Result<BranchListItemDto>.Failure("Nombre y dirección son obligatorios.");

        var locErr = await BranchLocationValidation.ValidateAsync(
            _geo,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            cancellationToken);
        if (locErr is not null)
            return Result<BranchListItemDto>.Failure(locErr);

        if (command.IsMainBranch)
            await _repo.ClearMainBranchExceptAsync(tenantId, command.Id, userId, cancellationToken);

        entity.Update(
            command.Name,
            command.Address,
            command.Description,
            command.Reference,
            command.PostalCode,
            command.Phone,
            command.SecondaryPhone,
            command.Email,
            command.Website,
            command.ManagerName,
            command.ManagerPosition,
            command.ManagerEmail,
            command.ManagerPhone,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            command.Latitude,
            command.Longitude,
            command.OpeningDate,
            command.InternalNotes,
            command.IsMainBranch,
            userId);

        if (command.IsActive && !entity.IsActive)
            entity.Enable(userId);
        else if (!command.IsActive && entity.IsActive)
            entity.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _user.Email,
            _user.FullName,
            module: "branches",
            action: "branch.update",
            entityType: "Branch",
            entityId: entity.Id,
            description: entity.Name), cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<BranchListItemDto>.Success(new BranchListItemDto(
            entity.Id,
            entity.Name,
            entity.Code,
            entity.Address,
            entity.CountryId,
            entity.ProvinceId,
            entity.CantonId,
            entity.ParishId,
            entity.Phone,
            entity.Email,
            entity.ManagerName,
            entity.IsActive,
            entity.IsMainBranch));
    }
}
