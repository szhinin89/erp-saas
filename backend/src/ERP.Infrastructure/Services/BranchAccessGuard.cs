using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Companies;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class BranchAccessGuard : IBranchAccessGuard
{
    private readonly ICompanyAccessGuard _companyAccessGuard;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly IAccessRepository _accessRepository;

    public BranchAccessGuard(
        ICompanyAccessGuard companyAccessGuard,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        IAccessRepository accessRepository
    )
    {
        _companyAccessGuard = companyAccessGuard;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _accessRepository = accessRepository;
    }

    public async Task<Result<BranchAccessContext>> RequireBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default
    )
    {
        var companyAccess = await _companyAccessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!companyAccess.IsSuccess)
            return Result<BranchAccessContext>.Failure(companyAccess.Error!);

        var company = companyAccess.Value!;

        var branch = await _branchRepository.GetByIdForCompanyAsync(
            company.TenantId,
            company.CompanyId,
            branchId,
            cancellationToken
        );
        if (branch is null)
            return Result<BranchAccessContext>.Failure("Sucursal no encontrada.");

        if (!branch.IsActive)
            return Result<BranchAccessContext>.Failure("La sucursal está deshabilitada.");

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            company.CompanyId,
            company.UserId,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<BranchAccessContext>.Failure("No tiene acceso a esta empresa.");

        var authorized = await _companyUserBranchRepository.ExistsAsync(
            membership.Id,
            branchId,
            cancellationToken
        );
        if (!authorized)
            return Result<BranchAccessContext>.Failure(
                "No tiene autorización para operar en esta sucursal."
            );

        return Result<BranchAccessContext>.Success(
            new BranchAccessContext(
                company.UserId,
                company.TenantId,
                company.CompanyId,
                branch.Id,
                branch.Name,
                branch.IsMainBranch
            )
        );
    }
}
