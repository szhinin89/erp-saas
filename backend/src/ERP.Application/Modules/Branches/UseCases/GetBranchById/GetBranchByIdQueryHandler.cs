using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Modules.Branches.UseCases.GetBranchById;

public sealed class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, Result<BranchDetailDto>>
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetBranchByIdQueryHandler(IBranchRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<BranchDetailDto>> Handle(GetBranchByIdQuery request, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(_tenant.TenantId, request.Id, ct);
        if (x is null)
            return Result<BranchDetailDto>.Failure("Sucursal no encontrada.");

        return Result<BranchDetailDto>.Success(new BranchDetailDto(
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
            x.IsMainBranch,
            x.CreatedAt,
            x.UpdatedAt,
            x.CreatedBy,
            x.UpdatedBy));
    }
}
