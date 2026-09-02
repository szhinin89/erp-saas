using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetBranches;

public sealed class GetBranchesQueryHandler
    : IRequestHandler<GetBranchesQuery, Result<IReadOnlyList<BranchListItemDto>>>
{
    private readonly IBranchRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetBranchesQueryHandler(
        IBranchRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany currentCompany
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<IReadOnlyList<BranchListItemDto>>> Handle(
        GetBranchesQuery request,
        CancellationToken cancellationToken
    )
    {
        var items = await _repo.GetByCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            request.ActiveFilter,
            request.Search,
            cancellationToken
        );
        var dtos = items
            .Select(x => new BranchListItemDto(
                x.Id,
                x.Name,
                x.Code,
                x.Address,
                x.CountryId,
                x.ProvinceId,
                x.CantonId,
                x.ParishId,
                x.Phone,
                x.Email,
                x.ManagerName,
                x.IsActive,
                x.IsMainBranch
            ))
            .ToList();

        return Result<IReadOnlyList<BranchListItemDto>>.Success(dtos);
    }
}
