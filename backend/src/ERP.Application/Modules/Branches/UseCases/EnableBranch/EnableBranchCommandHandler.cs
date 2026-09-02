using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.EnableBranch;

public sealed class EnableBranchCommandHandler
    : IRequestHandler<EnableBranchCommand, Result<BranchListItemDto>>
{
    private readonly IBranchRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public EnableBranchCommandHandler(
        IBranchRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _activity = activity;
        _currentTenant = tenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<BranchListItemDto>> Handle(
        EnableBranchCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await _repo.GetByIdForCompanyAsync(
            _currentTenant.TenantId,
            _company.CompanyId,
            request.Id,
            cancellationToken
        );
        if (entity is null)
            return Result<BranchListItemDto>.Failure("Sucursal no encontrada.");

        entity.Enable(_user.UserId);
        await _activity.AddAsync(
            UserActivity.Create(
                _currentTenant.TenantId,
                _user.UserId,
                _user.Email,
                _user.FullName,
                module: "branches",
                action: "branch.enable",
                entityType: "Branch",
                entityId: entity.Id,
                description: entity.Name
            ),
            cancellationToken
        );
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<BranchListItemDto>.Success(
            new BranchListItemDto(
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
                entity.IsMainBranch
            )
        );
    }
}
