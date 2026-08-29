using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Security;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// SECURITY-PERMISSION-SCOPE-01: rechaza permisos inexistentes (no declarados en
/// <see cref="KernelRegistry.Permissions"/>) y permisos no asignables (declarados pero fuera de
/// <see cref="KernelRegistry.AssignablePermissionKeys"/>) en pasos separados, y aplica una regla
/// anti-escalamiento — un asignador sin rol <see cref="SecurityRoles.Admin"/> nunca puede otorgar
/// (<c>IsAllowed = true</c>) un permiso que él mismo no tiene efectivo en su contexto operativo
/// actual (<see cref="ICompanyContextProvider"/> + <see cref="IEffectivePermissionKeysProvider"/>).
/// Revocar (<c>IsAllowed = false</c>) no escala privilegios y no pasa por este chequeo.
/// No implementa restricción por plan/entitlement SaaS externo: eso es deuda del futuro
/// <c>IExternalEntitlementService</c> (SaaS es una plataforma externa conectada por API, fuera del
/// alcance de este handler) — <see cref="RejectedPermission"/>/<c>rejected</c> se declara pero
/// sigue sin poblarse aquí. Ver el mismo gap de plan en
/// <see cref="GetProfilePermissionAuditHandler"/> (todo permiso se reporta como <c>Effective</c>).
/// </summary>
public class UpsertProfilePermissionsHandler
    : IRequestHandler<UpsertProfilePermissionsCommand, Result<PermissionUpsertResultDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _effectivePermissionKeys;

    public UpsertProfilePermissionsHandler(
        IAccessRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider effectivePermissionKeys
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
        _companyContext = companyContext;
        _effectivePermissionKeys = effectivePermissionKeys;
    }

    public Task<Result<PermissionUpsertResultDto>> HandleAsync(
        UpsertProfilePermissionsCommand command,
        CancellationToken cancellationToken = default
    ) => Handle(command, cancellationToken);

    public async Task<Result<PermissionUpsertResultDto>> Handle(
        UpsertProfilePermissionsCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!(_currentTenant.TenantId != Guid.Empty) || !_currentUser.IsAuthenticated)
            return Result<PermissionUpsertResultDto>.Failure("No autenticado.");

        if (command.Items is null || command.Items.Count == 0)
            return Result<PermissionUpsertResultDto>.Failure("Debe enviar al menos 1 permiso.");

        // ADMIN-PERMISSIONS-SSOT-KERNEL-02 / SECURITY-PERMISSION-SCOPE-01: rechazo atómico — si
        // algún permiso no existe o no es asignable, no se guarda nada (nunca un guardado parcial
        // de los válidos). ValidationError mapea a 422 vía ApiResultExtensions.MapFailure.
        var requestedKeys = command
            .Items.Select(i => i.PermissionKey?.Trim() ?? string.Empty)
            .Where(k => k.Length > 0)
            .Distinct()
            .ToList();

        var nonExistentKeys = requestedKeys
            .Where(k => !KernelRegistry.Permissions.Contains(k, StringComparer.Ordinal))
            .ToList();
        if (nonExistentKeys.Count > 0)
            return Result<PermissionUpsertResultDto>.Failure(
                $"Permiso(s) inexistente(s): {string.Join(", ", nonExistentKeys)}.",
                ApiResponseCodes.Common.ValidationError
            );

        var nonAssignableKeys = requestedKeys
            .Where(k => !KernelRegistry.AssignablePermissionKeys.Contains(k))
            .ToList();
        if (nonAssignableKeys.Count > 0)
            return Result<PermissionUpsertResultDto>.Failure(
                $"Permiso(s) no asignable(s): {string.Join(", ", nonAssignableKeys)}.",
                ApiResponseCodes.Common.ValidationError
            );

        // SECURITY-PERMISSION-SCOPE-01: anti-escalamiento — un asignador sin rol Admin (bypass
        // total dentro del tenant, igual que en RuntimePermissionAuthorizer) no puede otorgar un
        // permiso que él mismo no tiene efectivo en su contexto operativo actual. Revocar
        // (IsAllowed = false) nunca escala privilegios, así que no pasa por este chequeo.
        if (!string.Equals(_currentUser.Role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var grantingKeys = command
                .Items.Where(i => i.IsAllowed && !string.IsNullOrWhiteSpace(i.PermissionKey))
                .Select(i => i.PermissionKey.Trim())
                .Distinct()
                .ToList();

            if (grantingKeys.Count > 0)
            {
                var assignerContext = await _companyContext.ResolveOperationalForCurrentUserAsync(
                    cancellationToken
                );
                if (
                    assignerContext is null
                    || !assignerContext.IsActiveMembership
                    || assignerContext.ProfileId is null
                )
                    return Result<PermissionUpsertResultDto>.Failure(
                        "No se pudo resolver el contexto operativo del usuario que asigna.",
                        ApiResponseCodes.Common.Forbidden
                    );

                var assignerAllowedKeys = await _effectivePermissionKeys.GetAllowedKeysAsync(
                    _currentTenant.TenantId,
                    assignerContext.CompanyId,
                    _currentUser.UserId,
                    assignerContext.ProfileId.Value,
                    cancellationToken
                );
                var assignerAllowedSet = new HashSet<string>(
                    assignerAllowedKeys,
                    StringComparer.OrdinalIgnoreCase
                );

                var outOfScopeKeys = grantingKeys
                    .Where(k => !assignerAllowedSet.Contains(k))
                    .ToList();
                if (outOfScopeKeys.Count > 0)
                    return Result<PermissionUpsertResultDto>.Failure(
                        $"No puede asignar permiso(s) fuera de su propio alcance: {string.Join(", ", outOfScopeKeys)}.",
                        ApiResponseCodes.Common.Forbidden
                    );
            }
        }

        var profile = await _repo.GetProfileByIdAsync(
            _currentTenant.TenantId,
            command.ProfileId,
            cancellationToken
        );
        if (profile is null)
            return Result<PermissionUpsertResultDto>.Failure("Perfil no existe.");

        var tenantId = _currentTenant.TenantId;
        var actorId = _currentUser.UserId;
        var saved = new List<string>();
        var rejected = new List<RejectedPermission>();

        foreach (var item in command.Items.Where(i => !string.IsNullOrWhiteSpace(i.PermissionKey)))
        {
            var key = item.PermissionKey.Trim();
            var existing = await _repo.GetProfilePermissionAsync(
                tenantId,
                command.ProfileId,
                key,
                cancellationToken
            );

            if (existing is null)
            {
                var created = AccessProfilePermission.Create(
                    tenantId: tenantId,
                    profileId: command.ProfileId,
                    permissionKey: key,
                    isAllowed: item.IsAllowed,
                    createdBy: actorId
                );
                await _repo.AddProfilePermissionAsync(created, cancellationToken);
            }
            else
            {
                existing.SetAllowed(item.IsAllowed, actorId);
            }

            saved.Add(key);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        var memberships = await _repo.GetCompanyUserMembershipsByTenantAsync(
            tenantId,
            onlyActive: true,
            cancellationToken
        );

        var profileMemberships = memberships.Where(m => m.ProfileId == command.ProfileId).ToList();

        foreach (var companyId in profileMemberships.Select(m => m.CompanyId).Distinct())
        {
            await _permissionsCache.BumpCompanyVersionAsync(companyId, cancellationToken);
        }

        foreach (var membership in profileMemberships)
        {
            _navigationBuilder.InvalidateCache(
                tenantId,
                membership.CompanyId,
                membership.IdentityUserId
            );
        }

        return Result<PermissionUpsertResultDto>.Success(
            new PermissionUpsertResultDto(saved, rejected)
        );
    }
}
