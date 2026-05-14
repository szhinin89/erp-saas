using MediatR;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

public class SuperAdminLoginHandler : IRequestHandler<SuperAdminLoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;

    public SuperAdminLoginHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> Handle(SuperAdminLoginCommand command, CancellationToken ct)
    {
        if (!_deployment.IsSuperAdminPanelEnabled)
            return Result<AuthResponseDto>.Failure(DeploymentAuthMessages.SuperAdminPanelDisabled);

        var email = command.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Result<AuthResponseDto>.Failure("Email requerido.");

        var user = await _userRepository.GetSingleSuperAdminByEmailAsync(email, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        if (!string.Equals(user.Role, "SuperAdmin", StringComparison.Ordinal))
            return Result<AuthResponseDto>.Failure("No autorizado.");

        var passwordValid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

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
