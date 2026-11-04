using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

public sealed class UpdateCompanyHandler
    : IRequestHandler<UpdateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessRepository _access;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;

    public UpdateCompanyHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        ICurrentUser currentUser,
        IAccessRepository access,
        IEffectivePermissionKeysProvider permissionKeys
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _currentUser = currentUser;
        _access = access;
        _permissionKeys = permissionKeys;
    }

    public async Task<Result<CompanyDetailDto>> Handle(
        UpdateCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireMembershipAsync(
            command.Id,
            requireActiveCompany: false,
            cancellationToken
        );
        // Empresa pedida por id: inexistente, de otro tenant, sin membership o no operativa son la
        // MISMA respuesta (NOT_FOUND, mismo texto) — no se revela qué empresas existen. Se decide
        // por código: solo UNAUTHORIZED se propaga tal cual.
        if (!access.IsSuccess)
            return access.Code == ApiResponseCodes.Common.Unauthorized
                ? Result<CompanyDetailDto>.Failure(access.Error!, access.Code)
                : Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        // SEC-03: perm:erp.companies.update se evaluó en la empresa del X-Company-Id; la empresa
        // del id puede ser otra del usuario, así que el permiso se exige también en ella.
        if (!await CanUpdateAsync(access.Value!, cancellationToken))
            return Result<CompanyDetailDto>.Forbidden("No tiene permiso para actualizar esta empresa.");

        var entity = await _companies.GetTrackedByIdForTenantAsync(
            command.Id,
            access.Value!.TenantId,
            cancellationToken
        );
        if (entity is null)
            return Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        return await CompanyIdentityUpdate.ApplyAsync(
            entity,
            command,
            command.IsActive,
            _companies,
            _currentUser.UserId,
            cancellationToken
        );
    }

    /// <summary>Admin de la empresa destino (u operador global, que el guard resuelve como Admin) o perfil con el permiso en ella.</summary>
    private async Task<bool> CanUpdateAsync(CompanyAccessContext target, CancellationToken ct)
    {
        if (string.Equals(target.Role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        var membership = await _access.GetCompanyUserMembershipAsync(target.CompanyId, target.UserId, ct);
        if (membership is not { IsActive: true, ProfileId: Guid profileId })
            return false;

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            target.TenantId,
            target.CompanyId,
            target.UserId,
            profileId,
            ct
        );
        return allowed.Any(k =>
            string.Equals(k, SettingsPermissions.CompaniesUpdate, StringComparison.OrdinalIgnoreCase)
        );
    }
}
