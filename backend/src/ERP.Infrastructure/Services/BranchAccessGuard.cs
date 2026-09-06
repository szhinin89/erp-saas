using ERP.Application.Common;
using ERP.Application.Common.Security;
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
    private readonly IOperatorCompanyAccessPolicy _operatorAccessPolicy;

    public BranchAccessGuard(
        ICompanyAccessGuard companyAccessGuard,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        IAccessRepository accessRepository,
        IOperatorCompanyAccessPolicy operatorAccessPolicy
    )
    {
        _companyAccessGuard = companyAccessGuard;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _accessRepository = accessRepository;
        _operatorAccessPolicy = operatorAccessPolicy;
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
        var hasActiveMembership = membership is not null && membership.IsActive;

        if (hasActiveMembership)
        {
            var authorized = await _companyUserBranchRepository.ExistsAsync(
                membership!.Id,
                branchId,
                cancellationToken
            );
            if (authorized)
                return BuildSuccess(company, branch);
        }

        // ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando esta empresa (ya
        // autorizado por ICompanyAccessGuard vía la misma política) puede operar cualquier
        // sucursal activa de la empresa sin CompanyUserBranch — la pertenencia/actividad de
        // `branch` ya se validó arriba. Esto también cubre el caso real donde el mismo usuario
        // TIENE una CompanyUserMembership (p. ej. porque además es Admin de esta empresa) pero
        // su fila CompanyUserBranch para esta sucursal fue revocada (is_active=false): la
        // restricción de esa membership específica no debe bloquear su capacidad de soporte
        // global — por eso este chequeo corre siempre que la membership no autorizó la
        // sucursal, nunca solo cuando la membership no existe.
        if (await _operatorAccessPolicy.IsAuthorizedOperatorAsync(cancellationToken))
            return BuildSuccess(company, branch);

        return Result<BranchAccessContext>.Failure(
            hasActiveMembership
                ? "No tiene autorización para operar en esta sucursal."
                : "No tiene acceso a esta empresa."
        );
    }

    private static Result<BranchAccessContext> BuildSuccess(
        CompanyAccessContext company,
        ERP.Domain.Branches.Entities.Branch branch
    ) =>
        Result<BranchAccessContext>.Success(
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
