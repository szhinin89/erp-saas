using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Billing.Governance;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.RegisterSubscriberWithAdmin;

public class RegisterSubscriberWithAdminHandler : IRequestHandler<RegisterSubscriberWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ISubscriberRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISubscriberOnboardingService _onboarding;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IBillingGovernanceService _billingGovernance;

    public RegisterSubscriberWithAdminHandler(
        ISubscriberRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ISubscriberOnboardingService onboarding,
        ISessionModulesResolver sessionModules,
        ICompanyProvisioningService companyProvisioning,
        IBillingGovernanceService billingGovernance)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _onboarding = onboarding;
        _sessionModules = sessionModules;
        _companyProvisioning = companyProvisioning;
        _billingGovernance = billingGovernance;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(RegisterSubscriberWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(RegisterSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.SubscriberSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        var existingSubscriber = await _tenantRepository.GetBySlugAsync(slug, ct);
        if (existingSubscriber is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveSubscriberCapAsync(_deployment, _tenantRepository, ct);
        if (tenantQuota is not null)
            return Result<SessionResponseDto>.Failure(tenantQuota);

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<SessionResponseDto>.Failure("El email ya está registrado en el sistema.");

        var userQuota = await DeploymentQuota.GetBlockingReasonIfAtIdentityUserCapAsync(_deployment, _accessRepository, ct);
        if (userQuota is not null)
            return Result<SessionResponseDto>.Failure(userQuota);

        var tenant = Subscriber.Create(
            command.SubscriberName,
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
        await _tenantRepository.SaveChangesAsync(ct);

        await _billingGovernance.EnsureBillingAccountAsync(tenant.Id, email, Guid.Empty, ct);

        var passwordHash = _passwordHasher.HashPassword(command.AdminPassword);
        var identityUser = IdentityUser.Create(
            firstName: command.AdminFirstName,
            lastName: command.AdminLastName,
            email: email,
            passwordHash: passwordHash,
            createdBy: Guid.Empty);
        await _accessRepository.AddUserAsync(identityUser, ct);

        await _accessRepository.SaveChangesAsync(ct);

        var defaultCompany = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
            _deployment, _accessRepository, tenant.Id, ct);
        if (membershipCap is not null)
            return Result<SessionResponseDto>.Failure(membershipCap);

        var membership = CompanyUserMembership.Create(
            companyId: defaultCompany.Id,
            identityUserId: identityUser.Id,
            role: "Admin",
            profileId: null,
            createdBy: Guid.Empty);
        await _accessRepository.AddCompanyUserMembershipAsync(membership, ct);

        await _accessRepository.SaveChangesAsync(ct);

        // Onboard the new tenant: default profiles, Consumidor Final, main branch, main warehouse.
        await _onboarding.OnboardAsync(tenant.Id, actorId: identityUser.Id, ct);

        var sessionToken = _tokenService.GenerateSessionToken(identityUser, tenant.Id, "Admin", defaultCompany.Id);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: identityUser.Id,
            FullName: identityUser.FullName,
            Email: identityUser.Email.Value,
            SubscriberId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            modules));
    }
}

