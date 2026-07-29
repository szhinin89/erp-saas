using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetBranchById;

public sealed class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, Result<BranchDetailDto>>
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetBranchByIdQueryHandler(IBranchRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _currentTenant = tenant;
    }

    public async Task<Result<BranchDetailDto>> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var x = await _repo.GetByIdAsync(_currentTenant.TenantId, request.Id, cancellationToken);
        if (x is null)
            return Result<BranchDetailDto>.Failure("Sucursal no encontrada.");

        return Result<BranchDetailDto>.Success(new BranchDetailDto(
            x.Id,
            x.Name,
            x.Code,
            x.Description,
            x.IsMainBranch,
            x.Address,
            x.CountryId,
            x.ProvinceId,
            x.CantonId,
            x.ParishId,
            x.Reference,
            x.PostalCode,
            x.Latitude,
            x.Longitude,
            x.Phone,
            x.SecondaryPhone,
            x.Email,
            x.Website,
            x.ManagerName,
            x.ManagerPosition,
            x.ManagerEmail,
            x.ManagerPhone,
            x.OpeningDate,
            x.InternalNotes,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt,
            x.CreatedBy,
            x.UpdatedBy));
    }
}
