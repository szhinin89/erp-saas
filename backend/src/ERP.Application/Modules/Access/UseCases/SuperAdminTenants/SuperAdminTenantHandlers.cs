using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Access.UseCases.SuperAdminSubscribers;

/// <summary>Alta de empresa + Admin en <c>identity_users</c>/<c>company_user_memberships</c>; el Admin debe poder iniciar sesión y operar solo en ese tenant.</summary>
public class SuperAdminCreateSubscriberWithAdminHandler : IRequestHandler<SuperAdminCreateSubscriberWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ISubscriberRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICommercialCatalogQuery _saasCatalogQuery;
    private readonly ISubscriberOnboardingService _onboarding;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ISubscriptionFeatureOverridesService _overrides;
    private readonly ICompanyProvisioningService _companyProvisioning;

    public SuperAdminCreateSubscriberWithAdminHandler(
        ISubscriberRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IUserActivityRepository activity,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ICommercialCatalogQuery saasCatalogQuery,
        ISubscriberOnboardingService onboarding,
        ISessionModulesResolver sessionModules,
        ISubscriptionFeatureOverridesService overrides,
        ICompanyProvisioningService companyProvisioning)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _activity = activity;
        _currentUser = currentUser;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _saasCatalogQuery = saasCatalogQuery;
        _onboarding = onboarding;
        _sessionModules = sessionModules;
        _overrides = overrides;
        _companyProvisioning = companyProvisioning;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(SuperAdminCreateSubscriberWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(SuperAdminCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.SubscriberSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        var existingSubscriber = await _tenantRepository.GetBySlugAsync(slug, ct);
        if (existingSubscriber is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result<SessionResponseDto>.Failure("El email del administrador es obligatorio.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveSubscriberCapAsync(_deployment, _tenantRepository, ct);
        if (tenantQuota is not null)
            return Result<SessionResponseDto>.Failure(tenantQuota);

        if (command.LinkExistingAdmin)
            return await HandleLinkExistingAdminAsync(command, slug, email, ct);

        if (string.IsNullOrWhiteSpace(command.AdminPassword))
            return Result<SessionResponseDto>.Failure("La contraseña del administrador es obligatoria.");

        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<SessionResponseDto>.Failure("El email ya está registrado en el sistema.");

        var userQuota = await DeploymentQuota.GetBlockingReasonIfAtIdentityUserCapAsync(_deployment, _accessRepository, ct);
        if (userQuota is not null)
            return Result<SessionResponseDto>.Failure(userQuota);

        var planErr = await ValidatePlanAndModulesAsync(command.PlanCode, command.EnabledModules, ct);
        if (planErr is not null)
            return Result<SessionResponseDto>.Failure(planErr);

        var (planCode, moduleKeys) = NormalizeSubscription(command.PlanCode, command.EnabledModules);

        var tenant = Subscriber.Create(
            command.SubscriberName,
            slug,
            createdBy: _currentUser.UserId,
            passwordResetMode: command.PasswordResetMode,
            ruc: command.Ruc,
            shortName: command.ShortName,
            tradeName: command.TradeName,
            dinardap: command.Dinardap,
            logoUrl: command.LogoUrl,
            displayOrder: command.DisplayOrder,
            priority: command.Priority,
            planCode: planCode);
        await _tenantRepository.AddAsync(tenant, ct);

        var hash = _passwordHasher.HashPassword(command.AdminPassword);
        var adminUser = IdentityUser.Create(command.AdminFirstName, command.AdminLastName, email, hash, _currentUser.UserId);
        await _accessRepository.AddUserAsync(adminUser, ct);

        await _activity.AddAsync(UserActivity.Create(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "subscribers",
            action: "tenant.create",
            entityType: "Tenant",
            entityId: tenant.Id,
            description: $"{tenant.Name} ({tenant.Slug})"), ct);

        await _accessRepository.SaveChangesAsync(ct);

        var defaultCompany = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
            _deployment, _accessRepository, tenant.Id, ct);
        if (membershipCap is not null)
            return Result<SessionResponseDto>.Failure(membershipCap);

        var membership = CompanyUserMembership.Create(defaultCompany.Id, adminUser.Id, "Admin", profileId: null, createdBy: _currentUser.UserId);
        await _accessRepository.AddCompanyUserMembershipAsync(membership, ct);
        await _accessRepository.SaveChangesAsync(ct);

        if (moduleKeys is { Count: > 0 })
        {
            await _overrides.ApplyModuleOverridesAsync(
                tenant.Id,
                SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(moduleKeys),
                _currentUser.UserId,
                ct);
            await _accessRepository.SaveChangesAsync(ct);
        }

        await _onboarding.OnboardAsync(tenant.Id, _currentUser.UserId, ct);

        var sessionToken = _tokenService.GenerateSessionToken(adminUser, tenant.Id, "Admin", defaultCompany.Id);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: adminUser.Id,
            FullName: adminUser.FullName,
            Email: adminUser.Email.Value,
            SubscriberId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            modules)
        {
            CompanyId = defaultCompany.Id,
        });
    }

    private async Task<Result<SessionResponseDto>> HandleLinkExistingAdminAsync(
        SuperAdminCreateSubscriberWithAdminCommand command,
        string slug,
        string email,
        CancellationToken ct)
    {
        var existingUser = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (existingUser is null)
            return Result<SessionResponseDto>.Failure(
                "No existe un usuario con ese email. Cree la primera empresa con administrador nuevo o use otro correo.");

        var planErr = await ValidatePlanAndModulesAsync(command.PlanCode, command.EnabledModules, ct);
        if (planErr is not null)
            return Result<SessionResponseDto>.Failure(planErr);

        var (planCode, moduleKeys) = NormalizeSubscription(command.PlanCode, command.EnabledModules);

        var tenant = Subscriber.Create(
            command.SubscriberName,
            slug,
            createdBy: _currentUser.UserId,
            passwordResetMode: command.PasswordResetMode,
            ruc: command.Ruc,
            shortName: command.ShortName,
            tradeName: command.TradeName,
            dinardap: command.Dinardap,
            logoUrl: command.LogoUrl,
            displayOrder: command.DisplayOrder,
            priority: command.Priority,
            planCode: planCode);
        await _tenantRepository.AddAsync(tenant, ct);

        await _activity.AddAsync(UserActivity.Create(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "subscribers",
            action: "tenant.create",
            entityType: "Tenant",
            entityId: tenant.Id,
            description: $"{tenant.Name} ({tenant.Slug}); admin vinculado {email}"), ct);

        await _accessRepository.SaveChangesAsync(ct);

        var defaultCompany = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var existingCompanyUserMembership = await _accessRepository.GetCompanyUserMembershipAsync(defaultCompany.Id, existingUser.Id, ct);
        if (existingCompanyUserMembership is null)
        {
            var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
                _deployment, _accessRepository, tenant.Id, ct);
            if (membershipCap is not null)
                return Result<SessionResponseDto>.Failure(membershipCap);

            var membership = CompanyUserMembership.Create(defaultCompany.Id, existingUser.Id, "Admin", profileId: null, createdBy: _currentUser.UserId);
            await _accessRepository.AddCompanyUserMembershipAsync(membership, ct);
        }
        else
        {
            existingCompanyUserMembership.Activate("Admin", profileId: null, updatedBy: _currentUser.UserId);
        }

        await _accessRepository.SaveChangesAsync(ct);

        if (moduleKeys is { Count: > 0 })
        {
            await _overrides.ApplyModuleOverridesAsync(
                tenant.Id,
                SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(moduleKeys),
                _currentUser.UserId,
                ct);
            await _accessRepository.SaveChangesAsync(ct);
        }

        await _onboarding.OnboardAsync(tenant.Id, _currentUser.UserId, ct);

        var sessionToken = _tokenService.GenerateSessionToken(existingUser, tenant.Id, "Admin", defaultCompany.Id);
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: existingUser.Id,
            FullName: existingUser.FullName,
            Email: existingUser.Email.Value,
            SubscriberId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            modules)
        {
            CompanyId = defaultCompany.Id,
        });
    }

    /// <summary>Devuelve mensaje de error o null si OK. Exige al menos un plan en catálogo y un plan activo elegido.</summary>
    private async Task<string?> ValidatePlanAndModulesAsync(string? planCode, List<string>? enabledModules, CancellationToken ct)
    {
        var plans = await _saasCatalogQuery.GetPlansWithFeaturesAsync(ct);
        if (plans.Count == 0)
            return "No hay planes comerciales definidos. Cree al menos un plan antes de crear una empresa.";

        var pc = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        if (pc is null)
            return "Debe elegir un plan comercial.";

        var match = plans.FirstOrDefault(p =>
            string.Equals(p.Code, pc, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return $"El plan comercial '{pc}' no existe en el catálogo SaaS.";
        if (!match.IsActive)
            return $"El plan comercial '{pc}' está inactivo.";

        if (enabledModules is null || enabledModules.Count == 0)
            return null;

        try
        {
            SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(enabledModules);
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }

        return null;
    }

    /// <summary>Plan normalizado; módulos null si la lista viene vacía (sin restricción JSON).</summary>
    private static (string? PlanCode, IReadOnlyList<string>? ModuleKeys) NormalizeSubscription(string? planCode, List<string>? enabledModules)
    {
        var pc = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        if (enabledModules is null || enabledModules.Count == 0)
            return (pc, null);

        var cleaned = SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(enabledModules);
        return cleaned.Count == 0 ? (pc, null) : (pc, cleaned);
    }
}

public class GetSuperAdminSubscribersHandler : IRequestHandler<GetSuperAdminSubscribersQuery, Result<IReadOnlyList<SuperAdminSubscriberItemDto>>>
{
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ISubscriberMenuAdminService _tenantMenuAdmin;
    private readonly ISessionModulesResolver _sessionModules;

    public GetSuperAdminSubscribersHandler(
        ISubscriberRepository tenantRepository,
        ISubscriberMenuAdminService tenantMenuAdmin,
        ISessionModulesResolver sessionModules)
    {
        _tenantRepository = tenantRepository;
        _tenantMenuAdmin = tenantMenuAdmin;
        _sessionModules = sessionModules;
    }

    public async Task<Result<IReadOnlyList<SuperAdminSubscriberItemDto>>> Handle(GetSuperAdminSubscribersQuery request, CancellationToken ct)
    {
        var withCustom = await _tenantMenuAdmin.GetSubscriberIdsWithCustomMenuAsync(ct);
        var ordered = (await _tenantRepository.GetAllAsync(ct))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subscribers = new List<SuperAdminSubscriberItemDto>(ordered.Count);
        foreach (var t in ordered)
        {
            var modules = await _sessionModules.GetEnabledModuleKeysAsync(t.Id, ct);
            subscribers.Add(new SuperAdminSubscriberItemDto(
                t.Id,
                t.Name,
                t.Slug,
                t.IsActive,
                t.PlanCode,
                modules,
                SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(modules),
                withCustom.Contains(t.Id)));
        }

        return Result<IReadOnlyList<SuperAdminSubscriberItemDto>>.Success(subscribers);
    }
}
