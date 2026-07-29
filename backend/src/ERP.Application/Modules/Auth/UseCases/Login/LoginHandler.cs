using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    /// <summary>
    /// Sentinel usado cuando LoginCommand no trae TerminalId (ningún cliente real lo envía
    /// todavía). UserSession.TerminalId exige un valor no vacío, pero su contenido no afecta
    /// ninguna regla de negocio hoy (el cierre de sesión anterior es uniforme sin distinguir
    /// terminal) — ver UserSession.CloseByNewLogin.
    /// </summary>
    internal const string UnresolvedTerminalId = "terminal-unresolved";

    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IBranchRepository _branchRepository;
    private readonly IMediator _mediator;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IOptions<PasswordResetOptions> _passwordResetOptions;

    public LoginHandler(
        ITenantRepository TenantRepository,
        ICompanyRepository companyRepository,
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        ICompanyProvisioningService companyProvisioning,
        IBranchRepository branchRepository,
        IMediator mediator,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IOptions<PasswordResetOptions> passwordResetOptions
    )
    {
        _tenantRepository = TenantRepository;
        _companyRepository = companyRepository;
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _companyProvisioning = companyProvisioning;
        _branchRepository = branchRepository;
        _mediator = mediator;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordResetOptions = passwordResetOptions;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var username = command.Username.Trim();

        var identityUser = await _accessRepository.GetUserByUsernameAsync(
            username,
            cancellationToken
        );
        if (identityUser is null)
            return Result<AuthResponseDto>.Failure(
                "No estás registrado a una empresa. Comunícate con el administrador."
            );

        if (!identityUser.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (!_passwordHasher.VerifyPassword(command.Password, identityUser.PasswordHash))
            return Result<AuthResponseDto>.Failure(
                "Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador."
            );

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(
            identityUser.Id,
            cancellationToken
        );
        if (memberships.Count == 0)
            return Result<AuthResponseDto>.Failure(
                "No estás registrado a una empresa. Comunícate con el administrador."
            );

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, cancellationToken);
        var membershipByCompany = memberships.ToDictionary(m => m.CompanyId);

        var tenantGroups = companies.GroupBy(c => c.TenantId).ToList();

        if (tenantGroups.Count > 1)
            return Result<AuthResponseDto>.Failure(
                "Tu usuario está asociado a múltiples tenants. Usa el flujo de selección de tenant."
            );

        if (tenantGroups.Count == 0)
            return Result<AuthResponseDto>.Failure(
                "No estás registrado a una empresa. Comunícate con el administrador."
            );

        var tenantId = tenantGroups[0].Key;

        // Fase H: se revisa recién aquí — después de verificar la contraseña y de confirmar que
        // el usuario pertenece a exactamente un tenant — nunca antes de VerifyPassword, para no
        // revelar este estado a quien no demostró conocer la contraseña actual/temporal. En vez
        // de bloquear el login (deadlock: ChangeMyPassword exige sesión, y sin este bloque nunca
        // se llega a tener una), se emite un PasswordResetToken de un solo uso y NO se entrega
        // JWT — el cliente debe llamar POST /auth/complete-password-reset antes de obtener sesión.
        if (identityUser.RequirePasswordReset)
        {
            var (rawToken, expiresAtUtc) = await PasswordResetTokenIssuer.IssueAsync(
                _passwordResetTokenRepository,
                _passwordResetOptions,
                identityUser.Id,
                PasswordResetToken.KindIdentity,
                tenantId,
                overrideLifetimeMinutes: _passwordResetOptions.Value.FirstLoginTokenLifetimeMinutes,
                cancellationToken
            );

            return Result<AuthResponseDto>.Success(
                new AuthResponseDto(
                    identityUser.Id,
                    identityUser.FullName,
                    identityUser.Username,
                    identityUser.Email?.Value,
                    Role: string.Empty,
                    tenantId,
                    Token: string.Empty
                )
                {
                    RequiresPasswordReset = true,
                    PasswordResetToken = rawToken,
                    PasswordResetTokenExpiresIn = (int)
                        Math.Round((expiresAtUtc - DateTime.UtcNow).TotalSeconds),
                }
            );
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Tenant inactivo o no encontrado.");

        var companiesInTenant = tenantGroups[0].ToList();
        if (companiesInTenant.Count > 1)
        {
            const string pendingCompanyRole = SecurityRoles.User;
            var tokenWithoutCompany = _accessTokenService.GenerateSessionToken(
                identityUser,
                tenantId,
                pendingCompanyRole
            );
            var (refreshMulti, refreshExpiryMulti) = await _refreshTokenService.CreateAsync(
                identityUser.Id,
                tenantId,
                null,
                RefreshUserType.Identity,
                cancellationToken
            );

            return Result<AuthResponseDto>.Success(
                new AuthResponseDto(
                    identityUser.Id,
                    identityUser.FullName,
                    identityUser.Username,
                    identityUser.Email?.Value,
                    pendingCompanyRole,
                    tenantId,
                    tokenWithoutCompany
                )
                {
                    CompanyId = null,
                    RequiresCompanySelection = true,
                    RefreshToken = refreshMulti,
                    RefreshTokenExpiry = refreshExpiryMulti,
                }
            );
        }

        var company = companiesInTenant[0];
        if (!membershipByCompany.TryGetValue(company.Id, out var membership))
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, cancellationToken);

        var identityToken = _accessTokenService.GenerateSessionToken(
            identityUser,
            tenantId,
            membership.Role
        );

        string identityRefresh;
        DateTime identityRefreshExpiry;

        var (branchId, preferencesFailure) = await CompanyUserPreferencesLoginResolver.ResolveAsync(
            _mediator,
            membership.Id,
            () => ResolveMainBranchIdAsync(tenantId, company.Id, cancellationToken),
            cancellationToken
        );
        if (preferencesFailure is not null)
            return preferencesFailure;

        if (branchId is Guid resolvedBranchId)
        {
            var sessionResult = await _mediator.Send(
                new CreateAuthenticatedSessionCommand(
                    tenantId,
                    company.Id,
                    identityUser.Id,
                    resolvedBranchId,
                    command.TerminalId ?? UnresolvedTerminalId
                ),
                cancellationToken
            );

            if (!sessionResult.IsSuccess)
                return sessionResult.Code == ApiResponseCodes.Common.Conflict
                    ? Result<AuthResponseDto>.Conflict(
                        sessionResult.Error ?? "No se pudo iniciar la sesión. Intenta nuevamente."
                    )
                    : Result<AuthResponseDto>.Failure(
                        sessionResult.Error ?? "No se pudo iniciar la sesión."
                    );

            identityRefresh = sessionResult.Value!.RefreshToken;
            identityRefreshExpiry = sessionResult.Value.RefreshTokenExpiry;
        }
        else
        {
            // Sin BranchId resuelto — LoginMode.AskBranch explícito, o histórico sin
            // preferencias con 0/>1 branches IsMainBranch: no se crea UserSession en el login.
            // El frontend completa la selección después, vía GetSessionContext + BranchSelectorModal
            // (useBranchGate) y POST /session/switch-branch, que sí crea la sucursal activa.
            (identityRefresh, identityRefreshExpiry) = await _refreshTokenService.CreateAsync(
                identityUser.Id,
                tenantId,
                company.Id,
                RefreshUserType.Identity,
                cancellationToken
            );
        }

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                identityUser.Id,
                identityUser.FullName,
                identityUser.Username,
                identityUser.Email?.Value,
                membership.Role,
                tenantId,
                identityToken
            )
            {
                CompanyId = company.Id,
                RequiresCompanySelection = false,
                OnboardingCompleted = company.OnboardingCompleted,
                OperationalStatus = company.OperationalStatus,
                RefreshToken = identityRefresh,
                RefreshTokenExpiry = identityRefreshExpiry,
            }
        );
    }

    /// <summary>
    /// Resuelve la sucursal principal (IsMainBranch) activa de la empresa como sustituto
    /// interino de una selección real de sucursal (que no existe todavía en este roadmap —
    /// requiere un endpoint/UI dedicados, fuera de alcance de esta fase). Devuelve null si no
    /// hay exactamente una — nunca adivina: 0 sucursales o más de una marcada como principal
    /// dejan el login sin UserSession en vez de elegir una al azar.
    /// </summary>
    private async Task<Guid?> ResolveMainBranchIdAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var branches = await _branchRepository.GetAsync(
            tenantId,
            activeFilter: true,
            search: null,
            cancellationToken
        );
        var mainBranches = branches.Where(b => b.CompanyId == companyId && b.IsMainBranch).ToList();
        return mainBranches.Count == 1 ? mainBranches[0].Id : null;
    }
}
