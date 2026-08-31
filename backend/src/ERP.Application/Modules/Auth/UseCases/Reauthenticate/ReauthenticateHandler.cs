using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.Reauthenticate;

/// <summary>
/// Fase 4: reautenticación del MISMO usuario tras bloqueo por inactividad (SessionLockOverlay),
/// sin desmontar la pantalla. A diferencia de <c>RefreshTokenHandler</c>, la identidad se resuelve
/// desde el refresh token sin rotarlo primero (<see cref="IRefreshTokenService.ValidateWithoutRotatingAsync"/>)
/// — así una contraseña incorrecta no quema la sesión vigente. Solo tras verificar la contraseña
/// se revoca el token anterior y se emite uno nuevo vía <see cref="IRefreshTokenService.CreateAsync"/>
/// (no <c>ValidateAndRotateAsync</c>): a propósito, reautenticarse reinicia la ventana absoluta de
/// 8 horas, igual que un login — el usuario demostró su contraseña de nuevo, es una sesión nueva,
/// no una rotación silenciosa.
/// </summary>
public sealed class ReauthenticateHandler
    : IRequestHandler<ReauthenticateCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IPasswordHasher _passwordHasher;

    public ReauthenticateHandler(
        IRefreshTokenService refreshTokenService,
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        ICompanyRepository companyRepository,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher
    )
    {
        _refreshTokenService = refreshTokenService;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _companyRepository = companyRepository;
        _accessTokenService = accessTokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        ReauthenticateCommand command,
        CancellationToken cancellationToken
    )
    {
        var v = await _refreshTokenService.ValidateWithoutRotatingAsync(
            command.RawRefreshToken,
            cancellationToken
        );
        if (!v.IsValid)
            return Result<AuthResponseDto>.Failure(
                v.Error ?? "Sesión no válida. Inicia sesión nuevamente."
            );

        if (v.UserType != RefreshUserType.Identity)
            return Result<AuthResponseDto>.Failure(
                "Tipo de sesión no soportado. Inicia sesión nuevamente."
            );

        var user = await _accessRepository.GetUserByIdAsync(v.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario no válido. Inicia sesión nuevamente.");

        // Verificar contraseña ANTES de tocar el refresh token: una contraseña incorrecta no debe
        // afectar en nada la sesión vigente (el usuario puede reintentar o cerrar el modal).
        if (!_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
            return Result<AuthResponseDto>.Failure("Contraseña incorrecta.");

        var tenant = await _tenantRepository.GetByIdAsync(v.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<AuthResponseDto>.Failure(
                "Tenant no encontrado o inactivo. Inicia sesión nuevamente."
            );

        if (v.CompanyId is not Guid companyId || companyId == Guid.Empty)
            return Result<AuthResponseDto>.Failure(
                "Selecciona nuevamente tu empresa. Inicia sesión nuevamente."
            );

        var company = await _companyRepository.GetByIdForTenantAsync(
            companyId,
            v.TenantId,
            cancellationToken
        );
        if (company is null)
            return Result<AuthResponseDto>.Failure(
                "Empresa no válida para el tenant. Inicia sesión nuevamente."
            );

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            companyId,
            user.Id,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<AuthResponseDto>.Failure(
                "Membresía no activa para la empresa. Inicia sesión nuevamente."
            );

        var accessToken = _accessTokenService.GenerateSessionToken(user, v.TenantId, membership.Role);

        await _refreshTokenService.RevokeAsync(
            command.RawRefreshToken,
            "Reautenticación",
            cancellationToken
        );
        var (newRefresh, newRefreshExpiry) = await _refreshTokenService.CreateAsync(
            user.Id,
            v.TenantId,
            companyId,
            RefreshUserType.Identity,
            cancellationToken
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
                OnboardingCompleted = company.OnboardingCompleted,
                OperationalStatus = company.OperationalStatus,
                RefreshToken = newRefresh,
                RefreshTokenExpiry = newRefreshExpiry,
            }
        );
    }
}
