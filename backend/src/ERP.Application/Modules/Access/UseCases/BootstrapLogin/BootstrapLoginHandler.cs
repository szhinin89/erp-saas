using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.UseCases.BootstrapLogin;

public class BootstrapLoginHandler : IRequestHandler<BootstrapLoginCommand, Result<BootstrapLoginResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IUserRepository _legacyUserRepository;
    private readonly IPasswordHasher _passwordHasher;

    public BootstrapLoginHandler(
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        IAccessTokenService tokenService,
        IUserRepository legacyUserRepository,
        IPasswordHasher passwordHasher)
    {
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _tokenService = tokenService;
        _legacyUserRepository = legacyUserRepository;
        _passwordHasher = passwordHasher;
    }

    public Task<Result<BootstrapLoginResponseDto>> HandleAsync(BootstrapLoginCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<BootstrapLoginResponseDto>> Handle(BootstrapLoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
        {
            // Puente de compatibilidad: permitir SuperAdmin legacy usar bootstrap-flow.
            var legacySuper = await _legacyUserRepository.GetSingleSuperAdminByEmailAsync(email, ct);
            if (legacySuper is null || !legacySuper.IsActive)
                return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

            var superValid = _passwordHasher.VerifyPassword(command.Password, legacySuper.PasswordHash);
            if (!superValid)
                return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

            var tenantsAll = await _tenantRepository.GetAllAsync(ct);
            var activeTenants = tenantsAll.Where(t => t.IsActive).ToList();
            var superTenantIds = activeTenants.Select(t => t.Id).ToList();

            var superBootstrapToken = _tokenService.GenerateBootstrapToken(
                userId: legacySuper.Id,
                email: legacySuper.Email.Value,
                fullName: legacySuper.FullName,
                role: "SuperAdmin",
                tenantIds: superTenantIds);

            var superAccessible = activeTenants
                .OrderBy(t => t.Name)
                .Select(t => new AccessibleTenantDto(t.Id, t.Name, t.Slug, "SuperAdmin"))
                .ToList();

            return Result<BootstrapLoginResponseDto>.Success(new BootstrapLoginResponseDto(
                UserId: legacySuper.Id,
                FullName: legacySuper.FullName,
                Email: legacySuper.Email.Value,
                BootstrapToken: superBootstrapToken,
                Tenants: superAccessible));
        }

        if (!user.IsActive)
            return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var valid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!valid)
            return Result<BootstrapLoginResponseDto>.Failure("Credenciales inválidas. Si olvidaste tus datos, comunícate con el administrador.");

        var memberships = await _accessRepository.GetActiveMembershipsForUserSystemAsync(user.Id, ct);
        if (memberships.Count == 0)
            return Result<BootstrapLoginResponseDto>.Failure("No estás registrado a una empresa. Comunícate con el administrador.");

        var membershipTenantIds = memberships.Select(m => m.TenantId).Distinct().ToList();
        var tenants = await _tenantRepository.GetAllAsync(ct);

        var membershipAccessible = memberships
            .Where(m => m.IsActive)
            .Join(tenants, m => m.TenantId, t => t.Id, (m, t) => new AccessibleTenantDto(
                TenantId: t.Id,
                Name: t.Name,
                Slug: t.Slug,
                Role: m.Role))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => x.Name)
            .ToList();

        var membershipBootstrapToken = _tokenService.GenerateBootstrapToken(user, membershipTenantIds);

        return Result<BootstrapLoginResponseDto>.Success(new BootstrapLoginResponseDto(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email.Value,
            BootstrapToken: membershipBootstrapToken,
            Tenants: membershipAccessible));
    }
}

