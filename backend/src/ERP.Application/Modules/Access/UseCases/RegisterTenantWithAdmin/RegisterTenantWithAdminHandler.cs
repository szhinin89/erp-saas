using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.UseCases.RegisterTenantWithAdmin;

public class RegisterTenantWithAdminHandler : IRequestHandler<RegisterTenantWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantOnboardingService _onboarding;
    private readonly ISessionModulesResolver _sessionModules;

    public RegisterTenantWithAdminHandler(
        ITenantRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ITenantOnboardingService onboarding,
        ISessionModulesResolver sessionModules)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _onboarding = onboarding;
        _sessionModules = sessionModules;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(RegisterTenantWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(RegisterTenantWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.TenantSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        var existingTenant = await _tenantRepository.GetBySlugAsync(slug, ct);
        if (existingTenant is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveTenantCapAsync(_deployment, _tenantRepository, ct);
        if (tenantQuota is not null)
            return Result<SessionResponseDto>.Failure(tenantQuota);

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<SessionResponseDto>.Failure("El email ya está registrado en el sistema.");

        var userQuota = await DeploymentQuota.GetBlockingReasonIfAtIdentityUserCapAsync(_deployment, _accessRepository, ct);
        if (userQuota is not null)
            return Result<SessionResponseDto>.Failure(userQuota);

        var tenant = Tenant.Create(
            command.TenantName,
            slug,
            createdBy: Guid.Empty,
            passwordResetMode: command.PasswordResetMode,
            ruc: command.Ruc,
            shortName: command.ShortName,
            tradeName: command.TradeName,
            dinardap: command.Dinardap,
            logoUrl: command.LogoUrl,
            displayOrder: command.DisplayOrder,
            priority: command.Priority);
        await _tenantRepository.AddAsync(tenant, ct);

        var passwordHash = _passwordHasher.HashPassword(command.AdminPassword);
        var identityUser = IdentityUser.Create(
            firstName: command.AdminFirstName,
            lastName: command.AdminLastName,
            email: email,
            passwordHash: passwordHash,
            createdBy: Guid.Empty);
        await _accessRepository.AddUserAsync(identityUser, ct);

        var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(
            _deployment, _accessRepository, tenant.Id, ct);
        if (membershipCap is not null)
            return Result<SessionResponseDto>.Failure(membershipCap);

        var membership = Membership.Create(
            tenantId: tenant.Id,
            identityUserId: identityUser.Id,
            role: "Admin",
            profileId: null,
            createdBy: Guid.Empty);
        await _accessRepository.AddMembershipAsync(membership, ct);

        await _accessRepository.SaveChangesAsync(ct);

        // Onboard the new tenant: default profiles, Consumidor Final, main branch, main warehouse.
        await _onboarding.OnboardAsync(tenant.Id, actorId: identityUser.Id, ct);

        var sessionToken = _tokenService.GenerateSessionToken(identityUser, tenant.Id, "Admin");
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, tenant, ct);
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: identityUser.Id,
            FullName: identityUser.FullName,
            Email: identityUser.Email.Value,
            TenantId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            modules));
    }
}

