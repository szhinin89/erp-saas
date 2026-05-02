using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Access.UseCases.SuperAdminTenants;

public class SuperAdminCreateTenantWithAdminHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;

    public SuperAdminCreateTenantWithAdminHandler(
        ITenantRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        IUserActivityRepository activity,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _activity = activity;
        _currentUser = currentUser;
        _deployment = deployment;
    }

    public async Task<Result<SessionResponseDto>> HandleAsync(SuperAdminCreateTenantWithAdminCommand command, CancellationToken ct = default)
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
            priority: command.Priority);
        await _tenantRepository.AddAsync(tenant, ct);

        var hash = BCrypt.Net.BCrypt.HashPassword(command.AdminPassword);
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

        await _tenantRepository.SaveChangesAsync(ct);
        await _accessRepository.SaveChangesAsync(ct);

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
            priority: command.Priority);
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

        await _tenantRepository.SaveChangesAsync(ct);
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
}
