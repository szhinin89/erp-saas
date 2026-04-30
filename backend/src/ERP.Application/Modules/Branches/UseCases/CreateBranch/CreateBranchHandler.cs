using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public sealed class CreateBranchHandler
{
    private readonly IBranchRepository _repo;
    private readonly IGeographyReadRepository _geo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateBranchHandler(
        IBranchRepository repo,
        IGeographyReadRepository geo,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _geo = geo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<BranchDto>> HandleAsync(CreateBranchCommand command, CancellationToken ct = default)
    {
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

        var tenantId = _tenant.TenantId;
        var userId = _user.UserId;

        if (command.IsMainBranch)
            await _repo.ClearMainBranchExceptAsync(tenantId, null, userId, ct);

        var entity = Branch.Create(
            tenantId,
            command.Name,
            command.Address,
            command.Reference,
            command.Phones,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            command.Latitude,
            command.Longitude,
            command.RechargeOption,
            command.IsMainBranch,
            userId);

        if (!command.IsActive)
            entity.Disable(userId);

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BranchDto>.Success(new BranchDto(
            entity.Id,
            entity.Name,
            entity.Address,
            entity.Reference,
            entity.Phones,
            entity.CountryId,
            entity.ProvinceId,
            entity.CantonId,
            entity.ParishId,
            entity.Latitude,
            entity.Longitude,
            entity.RechargeOption,
            entity.IsActive,
            entity.IsMainBranch));
    }
}
