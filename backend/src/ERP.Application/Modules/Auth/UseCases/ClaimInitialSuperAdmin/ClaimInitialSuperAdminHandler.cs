using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.ClaimInitialSuperAdmin;

public sealed class ClaimInitialSuperAdminHandler : IRequestHandler<ClaimInitialSuperAdminCommand, Result<AuthResponseDto>>
{
    private const int MinPasswordLength = 10;

    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IFirstRunSetupService _firstRunSetupService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public ClaimInitialSuperAdminHandler(
        IAccessRepository accessRepository,
        IAccessTokenService accessTokenService,
        IFirstRunSetupService firstRunSetupService,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService)
    {
        _accessRepository = accessRepository;
        _accessTokenService = accessTokenService;
        _firstRunSetupService = firstRunSetupService;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        ClaimInitialSuperAdminCommand command,
        CancellationToken ct)
    {
        if (!await _firstRunSetupService.ValidateSetupTokenAsync(command.SetupToken, ct))
            return Result<AuthResponseDto>.Failure("Token de instalación inválido o no configurado.");

        if (await _accessRepository.AnyPlatformSuperAdminAsync(ct))
            return Result<AuthResponseDto>.Failure("Ya existe un SuperAdmin en el sistema.");

        var email = command.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result<AuthResponseDto>.Failure("Email requerido.");

        if (string.IsNullOrWhiteSpace(command.FirstName) || string.IsNullOrWhiteSpace(command.LastName))
            return Result<AuthResponseDto>.Failure("Nombre y apellido son requeridos.");

        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");

        var password = command.Password ?? string.Empty;
        if (password.Length < MinPasswordLength)
            return Result<AuthResponseDto>.Failure($"La contraseña debe tener al menos {MinPasswordLength} caracteres.");

        IdentityUser user;
        try
        {
            var passwordHash = _passwordHasher.HashPassword(password);
            var newId = Guid.NewGuid();
            user = IdentityUser.CreatePlatformSuperAdmin(
                command.FirstName.Trim(),
                command.LastName.Trim(),
                email,
                passwordHash,
                createdBy: newId);
        }
        catch (ArgumentException ex)
        {
            return Result<AuthResponseDto>.Failure(ex.Message);
        }

        await _accessRepository.AddUserAsync(user, ct);
        await _accessRepository.SaveChangesAsync(ct);
        await _firstRunSetupService.MarkFirstRunCompletedAsync(ct);

        var token = _accessTokenService.GeneratePlatformSessionToken(user);
        var (refresh, refreshExpiry) = await _refreshTokenService.CreateAsync(
            user.Id, Guid.Empty, null, RefreshUserType.Platform, ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            "SuperAdmin",
            Guid.Empty,
            token,
            PlanCode: null,
            EnabledModules: SubscriberSubscriptionCatalog.AllModuleKeys)
        {
            RefreshToken = refresh,
            RefreshTokenExpiry = refreshExpiry,
        });
    }
}
