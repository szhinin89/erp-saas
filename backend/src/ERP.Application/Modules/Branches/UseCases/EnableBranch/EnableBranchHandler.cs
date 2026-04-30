using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.EnableBranch;

public sealed class EnableBranchHandler
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public EnableBranchHandler(IBranchRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<BranchDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, id, ct);
        if (entity is null)
            return Result<BranchDto>.Failure("Sucursal no encontrada.");

        entity.Enable(_user.UserId);
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
