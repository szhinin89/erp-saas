using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Auth.UseCases;
using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Media;
using ERP.Application.Modules.Session.DTOs;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Media.Enums;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.GetSessionContext;

public sealed class GetSessionContextHandler
    : IRequestHandler<GetSessionContextQuery, Result<SessionContextDto>>
{
    private const string LogoRole = "logo";

    private static readonly string[] AdminPermissions = ["*"];

    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentBranch _currentBranch;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;
    private readonly IMediaService _media;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly IMediator _mediator;
    private readonly IOperatorCompanyAccessPolicy _operatorAccessPolicy;

    public GetSessionContextHandler(
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentBranch currentBranch,
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        ICompanyRepository companyRepository,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider permissionKeys,
        IMediaService media,
        IUserSessionRepository userSessionRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        IMediator mediator,
        IOperatorCompanyAccessPolicy operatorAccessPolicy
    )
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentBranch = currentBranch;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _companyRepository = companyRepository;
        _companyContext = companyContext;
        _permissionKeys = permissionKeys;
        _media = media;
        _userSessionRepository = userSessionRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _mediator = mediator;
        _operatorAccessPolicy = operatorAccessPolicy;
    }

    public async Task<Result<SessionContextDto>> Handle(
        GetSessionContextQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated || _currentTenant.TenantId == Guid.Empty)
            return Result<SessionContextDto>.Failure("No autenticado.");

        var identityUser = await _accessRepository.GetUserByIdAsync(
            _currentUser.UserId,
            cancellationToken
        );
        var tenant = await _tenantRepository.GetByIdAsync(
            _currentTenant.TenantId,
            cancellationToken
        );

        var operationalContext = await _companyContext.ResolveOperationalForCurrentUserAsync(
            cancellationToken
        );

        var company =
            operationalContext is not null && operationalContext.CompanyId != Guid.Empty
                ? await _companyRepository.GetByIdAsync(
                    operationalContext.CompanyId,
                    cancellationToken
                )
                : null;

        var identity = new SessionIdentityDto(
            _currentUser.UserId,
            identityUser?.FullName ?? string.Empty,
            identityUser?.Username ?? string.Empty,
            identityUser?.Email?.Value
        );

        CompanyLogoDto? logo = null;
        if (company is not null)
        {
            var logoMedia = await _media.GetActivePrimaryAsync(
                _currentTenant.TenantId,
                company.Id,
                MediaOwnerType.Company,
                company.Id,
                LogoRole,
                cancellationToken
            );

            logo = logoMedia is null ? null : CompanyLogoDto.FromMediaFile(logoMedia);
        }

        var tenantDto = new SessionTenantDto(
            _currentTenant.TenantId,
            company?.TradeName ?? company?.LegalName ?? tenant?.Name ?? string.Empty,
            logo
        );

        var authorization = new SessionAuthorizationDto(
            ResolveRoles(_currentUser.Role),
            await ResolvePermissionsAsync(operationalContext, cancellationToken)
        );

        var preferences = new SessionPreferencesDto(tenant?.PreferredLanguage ?? "es");

        var branch = company is not null
            ? await ResolveActiveBranchAsync(company.Id, cancellationToken)
            : null;

        return Result<SessionContextDto>.Success(
            new SessionContextDto(identity, tenantDto, authorization, preferences, branch)
        );
    }

    /// <summary>
    /// Fase I-2.5 — orden de precedencia explícito para resolver la sucursal activa. Elimina la
    /// competencia entre ICurrentBranch y UserSession.BranchId detectada en Fase I-2 (un
    /// switch-branch avanzaba el header/activeBranchStore del cliente pero el bootstrap seguía
    /// devolviendo la sucursal de apertura de sesión al recargar la página):
    ///
    ///   1. <see cref="ICurrentBranch"/> (header X-Branch-Id) — fuente de verdad de "la sucursal
    ///      activa ahora mismo" para este request, exactamente igual que ICurrentCompany para la
    ///      empresa. Si el cliente ya trae un contexto de sucursal válido (porque un bootstrap
    ///      anterior lo fijó o porque hizo switch-branch), ese valor manda siempre — pero solo si
    ///      además pasa la MISMA autorización que <see cref="IBranchAccessGuard"/> exige en cada
    ///      request branch-scoped (sucursal activa + CompanyUserBranch vigente para la membership).
    ///      ZH-AUTH-BRANCH-CONTEXT-EXPENSES-AUDIT-12: antes esto solo validaba integridad mínima
    ///      (existe y pertenece a la empresa), por lo que un X-Branch-Id persistido en el cliente
    ///      cuya autorización fue revocada después (CompanyUserBranch desactivado) seguía siendo
    ///      "confirmado" aquí y el cliente lo reenviaba a endpoints branch-scoped reales, que sí
    ///      la rechazan (BRANCH_SCOPE_FORBIDDEN) — session/context nunca se autocorregía. Si el
    ///      header no pasa esta validación, se trata como si no existiera y se continúa con el
    ///      siguiente nivel, nunca se devuelve directamente null ni se lanza excepción (este
    ///      endpoint no es branch-scoped: solo informa cuál es la sucursal activa real).
    ///   2. UserSession Active para (usuario, tenant, empresa) — solo si no hay header. Hecho
    ///      histórico inmutable ("con qué sucursal se abrió esta sesión de sistema"); switch-branch
    ///      nunca lo muta (ver SwitchBranchHandler). Es el fallback de arranque en frío: el primer
    ///      bootstrap inmediatamente después del login, antes de que el cliente tenga nada
    ///      persistido en activeBranchStore/sessionStorage.
    ///      ZH-AUTH-BRANCH-FORBIDDEN-F5-NO-LOGOUT-13: igual que el header en el nivel 1, este valor
    ///      también se revalida con <see cref="IsAuthorizedForBranchAsync"/> antes de devolverse. Sin
    ///      esto, una sucursal revocada (CompanyUserBranch desactivado) después de abrir la sesión
    ///      podía seguir siendo "la sucursal activa" reportada aquí — el cliente la reenviaba a
    ///      endpoints branch-scoped reales, que la rechazan (BRANCH_SCOPE_FORBIDDEN), y session/context
    ///      volvía a servir la misma sucursal revocada en cada F5, cascadeando el 403 en vez de
    ///      autocorregirse. Si no pasa la validación, se descarta y se continúa al nivel 3.
    ///   3. CompanyUserPreferencesLoginResolver — única fuente de verdad para resolver la
    ///      sucursal inicial cuando tampoco existe una UserSession reutilizable (mismo mecanismo
    ///      que LoginHandler/SwitchCompanyHandler, sin reimplementarlo).
    /// </summary>
    private async Task<SessionBranchDto?> ResolveActiveBranchAsync(
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            companyId,
            _currentUser.UserId,
            cancellationToken
        );

        if (_currentBranch.HasBranchContext)
        {
            var headerBranch = await _branchRepository.GetByIdForCompanyAsync(
                _currentTenant.TenantId,
                companyId,
                _currentBranch.BranchId,
                cancellationToken
            );

            if (
                headerBranch is not null
                && headerBranch.IsActive
                && await IsAuthorizedForBranchAsync(membership, headerBranch.Id, cancellationToken)
            )
                return new SessionBranchDto(
                    headerBranch.Id,
                    headerBranch.Name,
                    headerBranch.IsMainBranch
                );
        }

        var activeSessions = await _userSessionRepository.GetActiveSessionsAsync(
            _currentUser.UserId,
            _currentTenant.TenantId,
            cancellationToken
        );
        var existingSession = activeSessions.FirstOrDefault(s => s.CompanyId == companyId);

        Guid? branchId = existingSession?.BranchId;

        if (
            branchId is Guid sessionBranchId
            && !await IsAuthorizedForBranchAsync(membership, sessionBranchId, cancellationToken)
        )
            branchId = null;

        if (branchId is null && membership is not null && membership.IsActive)
        {
            var (resolvedBranchId, _) = await CompanyUserPreferencesLoginResolver.ResolveAsync(
                _mediator,
                membership.Id,
                () => ResolveMainBranchIdAsync(companyId, cancellationToken),
                cancellationToken
            );

            branchId = resolvedBranchId;
        }

        if (branchId is not Guid resolved || resolved == Guid.Empty)
            return null;

        var branch = await _branchRepository.GetByIdForCompanyAsync(
            _currentTenant.TenantId,
            companyId,
            resolved,
            cancellationToken
        );
        return branch is null
            ? null
            : new SessionBranchDto(branch.Id, branch.Name, branch.IsMainBranch);
    }

    /// <summary>
    /// Misma autorización que <see cref="IBranchAccessGuard"/>: membership activa en la empresa
    /// y fila <c>CompanyUserBranch</c> vigente para esa membership+sucursal. Sin excepción para
    /// Admin de empresa — BranchAccessGuard tampoco la aplica (recibe sus propias filas
    /// CompanyUserBranch igual que cualquier otro rol). ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01:
    /// la única excepción real es un admin global operando esta empresa (sin membership aquí),
    /// resuelta por <see cref="IOperatorCompanyAccessPolicy"/> — misma política que
    /// <c>BranchAccessGuard</c>, nunca duplicada.
    /// </summary>
    private async Task<bool> IsAuthorizedForBranchAsync(
        CompanyUserMembership? membership,
        Guid branchId,
        CancellationToken cancellationToken
    )
    {
        if (
            membership is not null
            && membership.IsActive
            && await _companyUserBranchRepository.ExistsAsync(
                membership.Id,
                branchId,
                cancellationToken
            )
        )
            return true;

        return await _operatorAccessPolicy.IsAuthorizedOperatorAsync(cancellationToken);
    }

    /// <summary>
    /// Mismo heurístico interino que LoginHandler.ResolveMainBranchIdAsync/
    /// SwitchCompanyHandler.ResolveMainBranchIdAsync (fallback cuando no hay preferencia
    /// DirectToDefault resoluble): nunca adivina, solo resuelve si hay exactamente una
    /// sucursal activa marcada IsMainBranch en la empresa.
    /// </summary>
    private async Task<Guid?> ResolveMainBranchIdAsync(
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var branches = await _branchRepository.GetByCompanyAsync(
            _currentTenant.TenantId,
            companyId,
            activeFilter: true,
            search: null,
            cancellationToken
        );
        var mainBranches = branches.Where(b => b.IsMainBranch).ToList();
        return mainBranches.Count == 1 ? mainBranches[0].Id : null;
    }

    private static IReadOnlyList<string> ResolveRoles(string? role) =>
        string.IsNullOrEmpty(role)
        || string.Equals(role, "Bootstrap", StringComparison.OrdinalIgnoreCase)
            ? []
            : [role];

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        OperationalCompanyContext? operationalContext,
        CancellationToken cancellationToken
    )
    {
        if (
            string.Equals(
                _currentUser.Role,
                SecurityRoles.Admin,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return AdminPermissions;

        if (
            operationalContext is null
            || operationalContext.CompanyId == Guid.Empty
            || !operationalContext.IsActiveMembership
            || operationalContext.ProfileId is null
        )
            return [];

        return await _permissionKeys.GetAllowedKeysAsync(
            _currentTenant.TenantId,
            operationalContext.CompanyId,
            _currentUser.UserId,
            operationalContext.ProfileId.Value,
            cancellationToken
        );
    }
}
