using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Companies;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CompanyAccessGuard : ICompanyAccessGuard
{
    private readonly IAccessRepository _access;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ITenantRepository _tenants;
    private readonly ISecurityMetrics _metrics;
    private readonly IOperatorCompanyAccessPolicy _operatorAccessPolicy;

    public CompanyAccessGuard(
        IAccessRepository access,
        ICompanyRepository companies,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ITenantRepository tenants,
        ISecurityMetrics metrics,
        IOperatorCompanyAccessPolicy operatorAccessPolicy
    )
    {
        _access = access;
        _companies = companies;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _tenants = tenants;
        _metrics = metrics;
        _operatorAccessPolicy = operatorAccessPolicy;
    }

    public async Task<Result<Guid>> RequireActiveTenantAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_currentUser.IsAuthenticated)
            return Result<Guid>.Failure("No autenticado.", ApiResponseCodes.Common.Unauthorized);

        var tenantId = _currentTenant.TenantId;
        if (tenantId == Guid.Empty)
            return Result<Guid>.Failure(
                "Contexto de tenant no establecido.",
                ApiResponseCodes.Common.CompanyScopeForbidden
            );

        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<Guid>.Failure(
                "Tenant no válido o inactivo.",
                ApiResponseCodes.Common.CompanyScopeForbidden
            );

        return Result<Guid>.Success(tenantId);
    }

    public async Task<Result<CompanyAccessContext>> RequireMembershipAsync(
        Guid companyId,
        bool requireActiveCompany = true,
        CancellationToken cancellationToken = default
    )
    {
        var subResult = await RequireActiveTenantAsync(cancellationToken);
        if (!subResult.IsSuccess)
            return Result<CompanyAccessContext>.Failure(subResult.Error!, subResult.Code);

        var tenantId = subResult.Value!;

        var company = await _companies.GetByIdAsync(companyId, cancellationToken);
        if (company is null || company.TenantId != tenantId)
        {
            _metrics.RecordCrossCompanyDenied();
            // Inexistente y ajena al tenant son indistinguibles a propósito (sin existence
            // leakage): ambas son NOT_FOUND, nunca un "prohibido" que confirme que existe.
            return Result<CompanyAccessContext>.NotFound(
                "Empresa no encontrada o no pertenece al tenant activo."
            );
        }

        if (
            requireActiveCompany
            && (
                !company.IsActive
                || company.OperationalStatus != CompanyOperationalStatus.Operational
            )
        )
            return Result<CompanyAccessContext>.Forbidden("Empresa no disponible para operar.");

        var membership = await _access.GetCompanyUserMembershipAsync(
            companyId,
            _currentUser.UserId,
            cancellationToken
        );
        if (membership is not null && membership.IsActive)
            return await BuildSuccessAsync(
                tenantId,
                companyId,
                membership.Role,
                company,
                cancellationToken
            );

        // ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando esta empresa
        // (operator_mode + GlobalUserRole activa) no tiene CompanyUserMembership aquí — la
        // política central es la única fuente que puede sustituir ese requisito. Usuarios
        // normales sin membership siguen bloqueados igual que antes.
        if (await _operatorAccessPolicy.IsAuthorizedOperatorAsync(cancellationToken))
            return await BuildSuccessAsync(
                tenantId,
                companyId,
                SecurityRoles.Admin,
                company,
                cancellationToken
            );

        _metrics.RecordMembershipValidationFailed();
        return Result<CompanyAccessContext>.Forbidden("No tiene acceso a esta empresa.");
    }

    private async Task<Result<CompanyAccessContext>> BuildSuccessAsync(
        Guid tenantId,
        Guid companyId,
        string role,
        Company company,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await _tenants.GetByIdAsync(tenantId, cancellationToken);

        return Result<CompanyAccessContext>.Success(
            new CompanyAccessContext(
                _currentUser.UserId,
                tenantId,
                companyId,
                role,
                tenantEntity?.IsActive ?? false,
                company.IsActive
            )
        );
    }

    public async Task<Result<CompanyAccessContext>> RequireCurrentCompanyAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_currentCompany.HasCompanyContext)
        {
            _metrics.RecordInvalidCompanyContext();
            return Result<CompanyAccessContext>.Failure(
                "No hay empresa operativa seleccionada.",
                ApiResponseCodes.Common.CompanyScopeForbidden
            );
        }

        var access = await RequireMembershipAsync(
            _currentCompany.CompanyId,
            requireActiveCompany: true,
            cancellationToken
        );

        // La empresa operativa es CONTEXTO, no un recurso pedido: su rechazo (ajena, inexistente,
        // no operativa, sin membership) es COMPANY_SCOPE_FORBIDDEN, igual que en
        // CompanyScopeBehavior. Se decide por código; el mensaje se conserva tal cual.
        return
            access.IsSuccess
            || access.Code
                is not (ApiResponseCodes.Common.NotFound or ApiResponseCodes.Common.Forbidden)
            ? access
            : Result<CompanyAccessContext>.Failure(
                access.Error!,
                ApiResponseCodes.Common.CompanyScopeForbidden
            );
    }
}
