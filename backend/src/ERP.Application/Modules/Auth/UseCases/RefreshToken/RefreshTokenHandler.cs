using System.Security.Claims;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public sealed class RefreshTokenHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly ICompanyRepository _companyRepository;

    public RefreshTokenHandler(
        IRefreshTokenService refreshTokenService,
        IAccessRepository accessRepository,
        ITenantRepository TenantRepository,
        IAccessTokenService accessTokenService,
        ICompanyRepository companyRepository
    )
    {
        _refreshTokenService = refreshTokenService;
        _accessRepository = accessRepository;
        _tenantRepository = TenantRepository;
        _accessTokenService = accessTokenService;
        _companyRepository = companyRepository;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        // SEC-SUSP-02b: el estado de usuario/tenant/empresa/membresía se resuelve ANTES de consumir
        // el token. Si el refresh va a rechazarse, el token no se rota: no queda un sucesor activo
        // que el cliente nunca recibió, y el reintento no se confunde con reutilización maliciosa.
        // Un token que no pasa la lectura previa (inexistente, revocado, vencido) sí va al camino
        // de rotación, que conserva la detección de reutilización, la auditoría y los límites.
        var peek = await _refreshTokenService.ValidateWithoutRotatingAsync(
            command.RawRefreshToken,
            cancellationToken
        );

        Result<AuthResponseDto>? resolved = null;
        if (peek.IsValid)
        {
            resolved = await ResolveSessionAsync(peek, cancellationToken);
            if (!resolved.IsSuccess)
                return resolved;
        }

        var v = await _refreshTokenService.ValidateAndRotateAsync(
            command.RawRefreshToken,
            cancellationToken
        );
        if (!v.IsValid)
        {
            if (v.IsRateLimited)
                return Result<AuthResponseDto>.Failure(
                    v.Error ?? "Demasiados intentos de renovación.",
                    ApiResponseCodes.Common.RateLimited
                );
            return Result<AuthResponseDto>.Failure(v.Error ?? "Refresh token inválido.");
        }

        // Defensa en profundidad: si la lectura previa no pudo validar (p. ej. un doble de prueba
        // o un cambio entre ambas lecturas), se resuelve con el resultado de la rotación.
        resolved ??= await ResolveSessionAsync(v, cancellationToken);
        if (!resolved.IsSuccess)
            return resolved;

        return Result<AuthResponseDto>.Success(
            resolved.Value! with
            {
                RefreshToken = v.NewToken,
                RefreshTokenExpiry = v.NewExpiry,
            }
        );
    }

    /// <summary>
    /// Resuelve la sesión (usuario, tenant, empresa, membresía y rol) y arma la respuesta SIN
    /// refresh token. No escribe nada: es seguro llamarlo antes de rotar.
    /// </summary>
    private async Task<Result<AuthResponseDto>> ResolveSessionAsync(
        RefreshTokenValidationResult v,
        CancellationToken cancellationToken
    )
    {
        if (v.UserType != RefreshUserType.Identity)
            return Result<AuthResponseDto>.Failure(
                "Tipo de sesión no soportado. Inicie sesión nuevamente."
            );

        var user = await _accessRepository.GetUserByIdAsync(v.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido.");

        if (v.TenantId == Guid.Empty)
        {
            var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
                user.Id,
                SecurityRoles.Admin,
                cancellationToken
            );

            if (globalRole is null)
                return Result<AuthResponseDto>.Failure("Rol global no activo.");

            var globalAccessToken = _accessTokenService.GenerateSessionToken(
                user,
                Guid.Empty,
                SecurityRoles.Admin
            );

            return Result<AuthResponseDto>.Success(
                new AuthResponseDto(
                    user.Id,
                    user.FullName,
                    user.Username,
                    user.Email?.Value,
                    SecurityRoles.Admin,
                    Guid.Empty,
                    globalAccessToken
                )
                {
                    CompanyId = null,
                    RequiresCompanySelection = false,
                    OnboardingCompleted = true,
                }
            );
        }

        var tenant = await _tenantRepository.GetByIdAsync(v.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure("Tenant no encontrado o inactivo.");

        Guid? companyId = v.CompanyId;
        CompanyUserMembership? membership = null;
        ERP.Domain.Modules.Company.Entities.Company? resolvedCompany = null;

        if (companyId is Guid cid && cid != Guid.Empty)
        {
            resolvedCompany = await _companyRepository.GetByIdForTenantAsync(
                cid,
                v.TenantId,
                cancellationToken
            );
            if (resolvedCompany is null)
                return Result<AuthResponseDto>.Failure("Empresa no válida para el tenant.");

            // ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: si este refresh token se originó en
            // operate-company (RefreshToken.IsOperatorSession), revalidar GlobalUserRole en vivo
            // — nunca confiar en el flag persistido solo — y, si sigue activo, reemitir
            // operator_mode/global_admin_user_id en el nuevo access token. Sin esto, cada
            // refresh/F5 perdía esos claims (solo viven en el access token, que nunca se
            // persiste) y la sesión se degradaba silenciosamente a admin de empresa normal,
            // regida por su propia CompanyUserMembership (que puede no tener CompanyUserBranch
            // vigente). Si el rol global ya no está activo, no se falla aquí — simplemente se
            // continúa con la resolución normal por membership de abajo.
            if (v.IsOperatorSession && v.GlobalAdminUserId is Guid globalAdminUserId)
            {
                var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
                    globalAdminUserId,
                    SecurityRoles.Admin,
                    cancellationToken
                );
                if (globalRole is not null)
                {
                    var operatorAccessToken = _accessTokenService.GenerateSessionToken(
                        user,
                        v.TenantId,
                        SecurityRoles.Admin,
                        new Claim[]
                        {
                            new("operator_mode", "true"),
                            new("global_admin_user_id", globalAdminUserId.ToString()),
                        }
                    );

                    return Result<AuthResponseDto>.Success(
                        new AuthResponseDto(
                            user.Id,
                            user.FullName,
                            user.Username,
                            user.Email?.Value,
                            SecurityRoles.Admin,
                            v.TenantId,
                            operatorAccessToken
                        )
                        {
                            CompanyId = resolvedCompany.Id,
                            RequiresCompanySelection = false,
                            OnboardingCompleted = resolvedCompany.OnboardingCompleted,
                            OperationalStatus = resolvedCompany.OperationalStatus,
                            OperatorMode = true,
                            GlobalAdminUserId = globalAdminUserId,
                        }
                    );
                }
            }

            membership = await _accessRepository.GetCompanyUserMembershipAsync(
                resolvedCompany.Id,
                user.Id,
                cancellationToken
            );
            if (membership is null || !membership.IsActive)
                return Result<AuthResponseDto>.Failure("Membresía no activa para la empresa.");
        }
        else
        {
            var memberships =
                await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(
                    user.Id,
                    cancellationToken
                );
            var companies = await _companyRepository.GetByIdsAsync(
                memberships.Select(m => m.CompanyId).ToList(),
                cancellationToken
            );
            var inTenant = companies.Where(c => c.TenantId == v.TenantId).ToList();

            if (inTenant.Count == 1)
            {
                resolvedCompany = inTenant[0];
                companyId = resolvedCompany.Id;
                membership = memberships.First(m => m.CompanyId == companyId);
            }
            else if (inTenant.Count == 0)
            {
                return Result<AuthResponseDto>.Failure("Sin acceso a empresas en este tenant.");
            }
            else
            {
                // N companies: emit pending-company token — SwitchCompany will issue the final one.
                const string pendingCompanyRole = SecurityRoles.User;
                var accessTokenPartial = _accessTokenService.GenerateSessionToken(
                    user,
                    v.TenantId,
                    pendingCompanyRole
                );

                return Result<AuthResponseDto>.Success(
                    new AuthResponseDto(
                        user.Id,
                        user.FullName,
                        user.Username,
                        user.Email?.Value,
                        pendingCompanyRole,
                        v.TenantId,
                        accessTokenPartial
                    )
                    {
                        CompanyId = null,
                        RequiresCompanySelection = true,
                    }
                );
            }
        }

        membership ??= await _accessRepository.GetCompanyUserMembershipAsync(
            companyId!.Value,
            user.Id,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure("Membresía no encontrada.");

        var accessToken = _accessTokenService.GenerateSessionToken(
            user,
            v.TenantId,
            membership.Role
        );

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(
                user.Id,
                user.FullName,
                user.Username,
                user.Email?.Value,
                membership.Role,
                v.TenantId,
                accessToken
            )
            {
                CompanyId = companyId,
                OnboardingCompleted = resolvedCompany?.OnboardingCompleted ?? false,
                OperationalStatus = resolvedCompany?.OperationalStatus,
            }
        );
    }
}
