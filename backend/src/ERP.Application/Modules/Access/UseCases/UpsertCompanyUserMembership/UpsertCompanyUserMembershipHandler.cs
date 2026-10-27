using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

/// <summary>
/// SEC-05 — anti-escalamiento (mismo bypass que <see cref="ERP.Application.Access.Authorization.RuntimePermissionAuthorizer"/>,
/// <see cref="OperationalCompanyContext.HasAdminBypass"/>, y mismo criterio de alcance que
/// <c>UpsertProfilePermissionsHandler</c>): solo un Admin de la empresa u operador global asigna o
/// retira el rol Admin; un no Admin delegado no modifica su propia membership ni asigna un perfil
/// con permisos que él no tiene; el perfil asignado debe existir, estar activo y pertenecer a la
/// misma empresa (tenant + empresa explícitos, nunca solo el id del cliente).
/// </summary>
public class UpsertCompanyUserMembershipHandler
    : IRequestHandler<UpsertCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly IMediator _mediator;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _effectivePermissionKeys;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertCompanyUserMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        ITenantRepository TenantRepository,
        ICompanyRepository companyRepository,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        IMediator mediator,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider effectivePermissionKeys,
        IUnitOfWork unitOfWork
    )
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _tenantRepository = TenantRepository;
        _companyRepository = companyRepository;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _mediator = mediator;
        _companyContext = companyContext;
        _effectivePermissionKeys = effectivePermissionKeys;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<object>> HandleAsync(
        UpsertCompanyUserMembershipCommand command,
        CancellationToken cancellationToken = default
    ) => Handle(command, cancellationToken);

    public async Task<Result<object>> Handle(
        UpsertCompanyUserMembershipCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null)
            return Result<object>.Failure("Tenant no encontrado.");

        var company = await _companyRepository.GetByIdForTenantAsync(
            command.CompanyId,
            command.TenantId,
            cancellationToken
        );
        if (company is null)
            return Result<object>.NotFound("Empresa no encontrada para el tenant.");

        var username = command.Username.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByUsernameAsync(username, cancellationToken);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var existing = await _accessRepository.GetCompanyUserMembershipAsync(
            company.Id,
            user.Id,
            cancellationToken
        );

        var assignmentViolation = await ValidateAssignmentAsync(
            command,
            company.Id,
            user.Id,
            existing,
            cancellationToken
        );
        if (assignmentViolation is not null)
            return assignmentViolation;

        if (existing is not null)
        {
            // CompanyAdministratorInvariant: la empresa debe conservar al menos un Administrador
            // activo tras el cambio de rol — regla pura, el handler obtiene las memberships
            // activas y se las entrega, nunca al revés.
            var activeMemberships = await _accessRepository.GetCompanyUserMembershipsByCompanyAsync(
                company.Id,
                onlyActive: true,
                cancellationToken
            );
            var invariantViolation = CompanyAdministratorInvariant.Validate<object>(
                existing,
                new CompanyAdministratorInvariant.FutureState(IsActive: true, Role: command.Role),
                activeMemberships
            );
            if (invariantViolation is not null)
                return invariantViolation;
        }

        // SEC-06 (R5): toda validación ocurre antes de persistir — nunca se guarda un cambio de
        // rol/perfil para abandonar después la operación sin invalidar la caché.
        var branchViolation = await ValidateBranchesAsync(
            command,
            tenant.Id,
            company.Id,
            cancellationToken
        );
        if (branchViolation is not null)
            return branchViolation;

        return await _permissionsCache.RunAsync(
            PermissionsCacheScope.User(company.Id, user.Id),
            ct => PersistAsync(command, tenant.Id, company.Id, user.Id, existing, ct),
            cancellationToken
        );
    }

    /// <summary>
    /// SEC-06 (R5): membership, sucursales y preferencias como una sola unidad atómica. Usa la
    /// transacción ambiente si existe (p. ej. <c>CreateSystemUserHandler</c>, que confirma o revierte
    /// todo); si no, abre la suya. Un fallo de preferencias revierte el cambio de membership.
    /// </summary>
    private async Task<Result<object>> PersistAsync(
        UpsertCompanyUserMembershipCommand command,
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CompanyUserMembership? existing,
        CancellationToken cancellationToken
    )
    {
        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            CompanyUserMembership membership;
            if (existing is null)
            {
                membership = CompanyUserMembership.Create(
                    companyId,
                    userId,
                    command.Role,
                    command.ProfileId,
                    createdBy: _currentUser.UserId
                );
                await _accessRepository.AddCompanyUserMembershipAsync(membership, cancellationToken);
            }
            else
            {
                existing.Activate(command.Role, command.ProfileId, updatedBy: _currentUser.UserId);
                membership = existing;
            }

            await _accessRepository.SaveChangesAsync(cancellationToken);

            await AuthorizeBranchesAsync(
                command,
                tenantId,
                companyId,
                membership.Id,
                cancellationToken
            );

            // ── Preferencias operativas ──────────────────────────────────────────────────────
            // CompanyUserPreferences sigue siendo la única fuente de verdad de DefaultBranch/LoginMode.
            // Nunca se reimplementa su validación: siempre se reutilizan los UseCases de Fase C vía
            // MediatR (mismo patrón que LoginHandler → CreateAuthenticatedSessionCommand).
            var preferencesResult = await UpsertPreferencesAsync(
                membership.Id,
                command,
                cancellationToken
            );
            if (!preferencesResult.IsSuccess)
            {
                if (ownsTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                return Result<object>.Failure(preferencesResult.Error!, preferencesResult.Code);
            }

            if (ownsTransaction)
                await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        _navigationBuilder.InvalidateCache(command.TenantId, companyId, userId);
        return Result<object>.Success(new { });
    }

    /// <summary>Existencia real de cada sucursal pedida, antes de persistir nada (todo o nada).</summary>
    private async Task<Result<object>?> ValidateBranchesAsync(
        UpsertCompanyUserMembershipCommand command,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        foreach (var branchId in (command.AuthorizedBranchIds ?? []).Distinct())
        {
            var branch = await _branchRepository.GetByIdForCompanyAsync(
                tenantId,
                companyId,
                branchId,
                cancellationToken
            );
            if (branch is null)
                return Result<object>.NotFound($"La sucursal {branchId} no existe.");
        }

        return null;
    }

    // ── Autorización de sucursales ──────────────────────────────────────────────────
    // CompanyUserBranch sigue siendo la única fuente de verdad de sucursales permitidas.
    // Este es hoy el único flujo de producción que escribe en esa tabla — reutiliza la
    // entidad/repositorio ya construidos en Fase B tal cual, sin tocar CompanyUserBranch.Create
    // ni su Domain. Se valida existencia real de cada Branch antes de autorizar cualquiera
    // (todo o nada) y se ignoran las ya autorizadas (idempotente, sin duplicar el índice único).
    private async Task AuthorizeBranchesAsync(
        UpsertCompanyUserMembershipCommand command,
        Guid tenantId,
        Guid companyId,
        Guid membershipId,
        CancellationToken cancellationToken
    )
    {
        if (command.AuthorizedBranchIds is not { Count: > 0 } branchIds)
            return;

        foreach (var branchId in branchIds.Distinct())
        {
            var alreadyAuthorized = await _companyUserBranchRepository.ExistsAsync(
                membershipId,
                branchId,
                cancellationToken
            );
            if (alreadyAuthorized)
                continue;

            var authorization = CompanyUserBranch.Create(
                tenantId,
                companyId,
                membershipId,
                branchId,
                createdBy: _currentUser.UserId
            );
            await _companyUserBranchRepository.AddAsync(authorization, cancellationToken);
        }

        await _companyUserBranchRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<object>?> ValidateAssignmentAsync(
        UpsertCompanyUserMembershipCommand command,
        Guid companyId,
        Guid targetUserId,
        CompanyUserMembership? existing,
        CancellationToken cancellationToken
    )
    {
        // Autoridad del que asigna en ESTA empresa (membership operativa u operador global).
        var actor = await _companyContext.ResolveOperationalForCurrentUserAsync(
            cancellationToken
        );
        if (actor is null || actor.CompanyId != companyId)
            return Result<object>.Forbidden(
                "No se pudo resolver la autoridad del usuario que asigna en la empresa."
            );

        if (!actor.HasAdminBypass)
        {
            if (targetUserId == actor.UserId)
                return Result<object>.Forbidden("No puede modificar su propia membresía.");

            if (IsAdminRole(command.Role) || (existing is not null && IsAdminRole(existing.Role)))
                return Result<object>.Forbidden(
                    "Solo un administrador de la empresa puede asignar, modificar o retirar el rol Admin."
                );
        }

        if (command.ProfileId is not Guid profileId)
            return null;

        // Perfil vigente sin cambios: no es una asignación nueva.
        if (existing is { IsActive: true } && existing.ProfileId == profileId)
            return null;

        var profile = await _accessRepository.GetProfileByIdAsync(
            command.TenantId,
            companyId,
            profileId,
            cancellationToken
        );
        if (profile is null)
            return Result<object>.NotFound("El perfil no existe en la empresa.");
        if (!profile.IsActive)
            return Result<object>.ValidationFailure("El perfil está inactivo.");

        if (actor.HasAdminBypass)
            return null;

        if (!actor.IsActiveMembership || actor.ProfileId is null)
            return Result<object>.Forbidden(
                "No se pudo resolver el alcance del usuario que asigna."
            );

        var actorAllowed = new HashSet<string>(
            await _effectivePermissionKeys.GetAllowedKeysAsync(
                command.TenantId,
                companyId,
                actor.UserId,
                actor.ProfileId.Value,
                cancellationToken
            ),
            StringComparer.OrdinalIgnoreCase
        );
        var outOfScopeKeys = (
            await _accessRepository.GetProfilePermissionsAsync(
                command.TenantId,
                profileId,
                cancellationToken
            )
        )
            .Where(p => p.IsAllowed && !actorAllowed.Contains(p.PermissionKey))
            .Select(p => p.PermissionKey)
            .ToList();

        return outOfScopeKeys.Count > 0
            ? Result<object>.Forbidden(
                $"No puede asignar un perfil con permiso(s) fuera de su propio alcance: {string.Join(", ", outOfScopeKeys)}."
            )
            : null;
    }

    // Mismo criterio que OperationalCompanyContext.HasAdminBypass (sin distinguir mayúsculas).
    private static bool IsAdminRole(string? role) =>
        string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase);

    private async Task<Result<CompanyUserPreferencesDto?>> UpsertPreferencesAsync(
        Guid membershipId,
        UpsertCompanyUserMembershipCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingPreferences = await _mediator.Send(
            new GetCompanyUserPreferencesQuery(membershipId),
            cancellationToken
        );
        if (!existingPreferences.IsSuccess)
            return existingPreferences;

        if (existingPreferences.Value is null)
        {
            // Membresía nueva (o reactivada sin preferencias previas): si el administrador no
            // informó nada, se crea con el default documentado — AskBranch, sin sucursal. Nunca
            // se deja una membresía sin fila de preferencias.
            var createResult = await _mediator.Send(
                new CreateCompanyUserPreferencesCommand(
                    membershipId,
                    command.LoginMode ?? nameof(CompanyUserLoginMode.AskBranch),
                    command.DefaultBranchId
                ),
                cancellationToken
            );

            return createResult.IsSuccess
                ? Result<CompanyUserPreferencesDto?>.Success(createResult.Value)
                : Result<CompanyUserPreferencesDto?>.Failure(
                    createResult.Error!,
                    createResult.Code
                );
        }

        // Ya existían preferencias: solo se tocan si el administrador informó explícitamente
        // alguno de los dos campos — editar rol/perfil no debe resetear silenciosamente una
        // configuración de login ya definida.
        if (command.DefaultBranchId is null && command.LoginMode is null)
            return existingPreferences;

        var current = existingPreferences.Value;
        var updateResult = await _mediator.Send(
            new UpdateCompanyUserPreferencesCommand(
                membershipId,
                command.LoginMode ?? current.LoginMode,
                command.DefaultBranchId ?? current.DefaultBranchId
            ),
            cancellationToken
        );

        return updateResult.IsSuccess
            ? Result<CompanyUserPreferencesDto?>.Success(updateResult.Value)
            : Result<CompanyUserPreferencesDto?>.Failure(updateResult.Error!, updateResult.Code);
    }
}
