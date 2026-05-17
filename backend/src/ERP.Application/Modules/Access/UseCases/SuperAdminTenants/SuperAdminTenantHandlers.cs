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
using ERP.Domain.Tenants.Interfaces;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Access.UseCases.SuperAdminTenants;

/// <summary>Alta de empresa + Admin en <c>identity_users</c>/<c>memberships</c>; el Admin debe poder iniciar sesión y operar solo en ese tenant.</summary>
public class SuperAdminCreateTenantWithAdminHandler : IRequestHandler<SuperAdminCreateTenantWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISaasCatalogQuery _saasCatalogQuery;
    private readonly IDefaultProfileSeeder _profileSeeder;

    public SuperAdminCreateTenantWithAdminHandler(
        ITenantRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IUserActivityRepository activity,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ISaasCatalogQuery saasCatalogQuery,
        IDefaultProfileSeeder profileSeeder)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _activity = activity;
        _currentUser = currentUser;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _saasCatalogQuery = saasCatalogQuery;
        _profileSeeder = profileSeeder;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(SuperAdminCreateTenantWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(SuperAdminCreateTenantWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.TenantSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        var existingTenant = await _tenantRepository.GetBySlugAsync(slug, ct);
        if (existingTenant is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result<SessionResponseDto>.Failure("El email del administrador es obligatorio.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveTenantCapAsync(_deployment, _tenantRepository, ct);
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

        var tenant = Tenant.Create(
            command.TenantName,
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
            planCode: planCode,
            enabledModuleKeys: moduleKeys);
        await _tenantRepository.AddAsync(tenant, ct);

        var hash = _passwordHasher.HashPassword(command.AdminPassword);
        var adminUser = IdentityUser.Create(command.AdminFirstName, command.AdminLastName, email, hash, _currentUser.UserId);
        await _accessRepository.AddUserAsync(adminUser, ct);

        var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(
            _deployment, _accessRepository, tenant.Id, ct);
        if (membershipCap is not null)
            return Result<SessionResponseDto>.Failure(membershipCap);

        var membership = Membership.Create(tenant.Id, adminUser.Id, "Admin", profileId: null, createdBy: _currentUser.UserId);
        await _accessRepository.AddMembershipAsync(membership, ct);

        await _activity.AddAsync(UserActivity.Create(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "tenants",
            action: "tenant.create",
            entityType: "Tenant",
            entityId: tenant.Id,
            description: $"{tenant.Name} ({tenant.Slug})"), ct);

        // Un solo SaveChanges (mismo DbContext): evita confusión y persiste tenant + identity + membership en una transacción.
        await _accessRepository.SaveChangesAsync(ct);

        // Seed default profiles (Facturador / Bodeguero / Contador) for the new tenant.
        await _profileSeeder.SeedForTenantAsync(tenant.Id, _currentUser.UserId, ct);

        var sessionToken = _tokenService.GenerateSessionToken(adminUser, tenant.Id, "Admin");
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: adminUser.Id,
            FullName: adminUser.FullName,
            Email: adminUser.Email.Value,
            TenantId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant)));
    }

    private async Task<Result<SessionResponseDto>> HandleLinkExistingAdminAsync(
        SuperAdminCreateTenantWithAdminCommand command,
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

        var tenant = Tenant.Create(
            command.TenantName,
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
            planCode: planCode,
            enabledModuleKeys: moduleKeys);
        await _tenantRepository.AddAsync(tenant, ct);

        var existingMembership = await _accessRepository.GetMembershipAsync(tenant.Id, existingUser.Id, ct);
        if (existingMembership is null)
        {
            var membershipCap = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(
                _deployment, _accessRepository, tenant.Id, ct);
            if (membershipCap is not null)
                return Result<SessionResponseDto>.Failure(membershipCap);

            var membership = Membership.Create(tenant.Id, existingUser.Id, "Admin", profileId: null, createdBy: _currentUser.UserId);
            await _accessRepository.AddMembershipAsync(membership, ct);
        }
        else
        {
            existingMembership.Activate("Admin", profileId: null, updatedBy: _currentUser.UserId);
        }

        await _activity.AddAsync(UserActivity.Create(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "tenants",
            action: "tenant.create",
            entityType: "Tenant",
            entityId: tenant.Id,
            description: $"{tenant.Name} ({tenant.Slug}); admin vinculado {email}"), ct);

        await _accessRepository.SaveChangesAsync(ct);

        var sessionToken = _tokenService.GenerateSessionToken(existingUser, tenant.Id, "Admin");
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: existingUser.Id,
            FullName: existingUser.FullName,
            Email: existingUser.Email.Value,
            TenantId: tenant.Id,
            Role: "Admin",
            Token: sessionToken,
            tenant.PlanCode,
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant)));
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
            TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(enabledModules);
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

        var cleaned = enabledModules
            .Select(x => (x ?? string.Empty).Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return cleaned.Count == 0 ? (pc, null) : (pc, cleaned);
    }
}

public class GetSuperAdminTenantsHandler : IRequestHandler<GetSuperAdminTenantsQuery, Result<IReadOnlyList<SuperAdminTenantItemDto>>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantMenuAdminService _tenantMenuAdmin;

    public GetSuperAdminTenantsHandler(ITenantRepository tenantRepository, ITenantMenuAdminService tenantMenuAdmin)
    {
        _tenantRepository = tenantRepository;
        _tenantMenuAdmin = tenantMenuAdmin;
    }

    public async Task<Result<IReadOnlyList<SuperAdminTenantItemDto>>> Handle(GetSuperAdminTenantsQuery request, CancellationToken ct)
    {
        var withCustom = await _tenantMenuAdmin.GetTenantIdsWithCustomMenuAsync(ct);
        var tenants = (await _tenantRepository.GetAllAsync(ct))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new SuperAdminTenantItemDto(
                t.Id,
                t.Name,
                t.Slug,
                t.IsActive,
                t.PlanCode,
                TenantSubscriptionCatalog.GetEffectiveEnabledModules(t),
                !string.IsNullOrWhiteSpace(t.EnabledModulesJson),
                withCustom.Contains(t.Id)))
            .ToList();

        return Result<IReadOnlyList<SuperAdminTenantItemDto>>.Success(tenants);
    }
}
