using ERP.Application.Access.DTOs;
using ERP.Application.Admin;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.UseCases.SwitchTenant;

public class SwitchTenantHandler : IRequestHandler<SwitchTenantCommand, Result<SessionResponseDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly IConfigService _configService;
    private readonly ISessionModulesResolver _sessionModules;

    public SwitchTenantHandler(
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser,
        ITenantRepository tenantRepository,
        IConfigService configService,
        ISessionModulesResolver sessionModules)
    {
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _tenantRepository = tenantRepository;
        _configService = configService;
        _sessionModules = sessionModules;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(SwitchTenantCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(SwitchTenantCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var user = await _accessRepository.GetUserByIdAsync(userId, ct);
        if (user is null)
        {
            var role = _currentUser.Role;
            var email = _currentUser.Email;
            var fullName = _currentUser.FullName;

            if (!string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(fullName))
                return Result<SessionResponseDto>.Failure("Unauthorized");

            if (command.TenantId == Guid.Empty)
            {
                var superSessionGlobal = _tokenService.GenerateSessionToken(userId, email, fullName, Guid.Empty, "SuperAdmin");
                return Result<SessionResponseDto>.Success(new SessionResponseDto(
                    UserId: userId,
                    FullName: fullName,
                    Email: email,
                    TenantId: Guid.Empty,
                    Role: "SuperAdmin",
                    Token: superSessionGlobal,
                    PlanCode: null,
                    EnabledModules: TenantSubscriptionCatalog.AllModuleKeys));
            }

            var tenantSa = await _tenantRepository.GetByIdAsync(command.TenantId, ct);
            if (tenantSa is null || !tenantSa.IsActive)
                return Result<SessionResponseDto>.Failure("Unauthorized");

            var superSessionTenant = _tokenService.GenerateSessionToken(userId, email, fullName, command.TenantId, "SuperAdmin");
            await _configService.WarmupTenantAsync(command.TenantId, ct);
            var superModules = await _sessionModules.GetEnabledModuleKeysAsync(command.TenantId, ct);

            return Result<SessionResponseDto>.Success(new SessionResponseDto(
                UserId: userId,
                FullName: fullName,
                Email: email,
                TenantId: command.TenantId,
                Role: "SuperAdmin",
                Token: superSessionTenant,
                tenantSa.PlanCode,
                superModules));
        }

        if (!user.IsActive)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var membership = await _accessRepository.GetMembershipAsync(command.TenantId, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<SessionResponseDto>.Failure("No tienes acceso a esta empresa.");

        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result<SessionResponseDto>.Failure("No tienes acceso a esta empresa.");

        var membershipSessionToken = _tokenService.GenerateSessionToken(user, command.TenantId, membership.Role);
        await _configService.WarmupTenantAsync(command.TenantId, ct);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(command.TenantId, ct);

        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email.Value,
            TenantId: command.TenantId,
            Role: membership.Role,
            Token: membershipSessionToken,
            tenant.PlanCode,
            modules));
    }
}
