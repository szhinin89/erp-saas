using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Application.Auth.UseCases.ClaimInitialSuperAdmin;

public sealed class ClaimInitialSuperAdminHandler
{
    private const int MinPasswordLength = 10;

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IDeploymentFeatureFlags _deployment;

    public ClaimInitialSuperAdminHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IDeploymentFeatureFlags deployment)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _deployment = deployment;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        ClaimInitialSuperAdminCommand command,
        CancellationToken ct = default)
    {
        if (!_deployment.AuthorizeInitialSuperAdminSetup(command.SetupToken))
            return Result<AuthResponseDto>.Failure("Token de instalación inválido o no configurado.");

        if (await _userRepository.AnySuperAdminAsync(ct))
            return Result<AuthResponseDto>.Failure("Ya existe un SuperAdmin en el sistema.");

        var email = command.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result<AuthResponseDto>.Failure("Email requerido.");

        if (string.IsNullOrWhiteSpace(command.FirstName) || string.IsNullOrWhiteSpace(command.LastName))
            return Result<AuthResponseDto>.Failure("Nombre y apellido son requeridos.");

        try
        {
            if (await _userRepository.ExistsByEmailGloballyAsync(email, ct))
                return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");
        }
        catch (ArgumentException ex)
        {
            return Result<AuthResponseDto>.Failure(ex.Message);
        }

        var password = command.Password ?? string.Empty;
        if (password.Length < MinPasswordLength)
            return Result<AuthResponseDto>.Failure($"La contraseña debe tener al menos {MinPasswordLength} caracteres.");

        User user;
        try
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var newId = Guid.NewGuid();
            user = User.Create(
                Guid.Empty,
                command.FirstName.Trim(),
                command.LastName.Trim(),
                email,
                passwordHash,
                "SuperAdmin",
                createdBy: newId);
        }
        catch (ArgumentException ex)
        {
            return Result<AuthResponseDto>.Failure(ex.Message);
        }

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        var token = _jwtService.GenerateToken(user, Guid.Empty);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            Guid.Empty,
            token,
            PlanCode: null,
            EnabledModules: TenantSubscriptionCatalog.AllModuleKeys));
    }
}
