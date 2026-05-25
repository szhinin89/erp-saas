using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.PlatformSubscribers;

/// <summary>Alta de empresa + Admin en <c>identity_users</c>/<c>company_user_memberships</c>; transaccional vía orchestrator.</summary>
public class PlatformCreateSubscriberWithAdminHandler : IRequestHandler<PlatformCreateSubscriberWithAdminCommand, Result<SessionResponseDto>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly ICommercialCatalogQuery _saasCatalogQuery;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ISubscriberProvisioningOrchestrator _provisioning;

    public PlatformCreateSubscriberWithAdminHandler(
        ISubscriberRepository subscriberRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        ICommercialCatalogQuery saasCatalogQuery,
        ISessionModulesResolver sessionModules,
        ISubscriberProvisioningOrchestrator provisioning)
    {
        _subscriberRepository = subscriberRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _deployment = deployment;
        _saasCatalogQuery = saasCatalogQuery;
        _sessionModules = sessionModules;
        _provisioning = provisioning;
    }

    public Task<Result<SessionResponseDto>> HandleAsync(PlatformCreateSubscriberWithAdminCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<SessionResponseDto>> Handle(PlatformCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var slug = command.SubscriberSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        if (await _subscriberRepository.GetBySlugAsync(slug, ct) is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result<SessionResponseDto>.Failure("El email del administrador es obligatorio.");

        var tenantQuota = await DeploymentQuota.GetBlockingReasonIfAtActiveSubscriberCapAsync(_deployment, _subscriberRepository, ct);
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

        try
        {
            var provisioned = await _provisioning.ProvisionNewSubscriberWithAdminAsync(
                new SubscriberProvisioningRequest(
                    SubscriberName: command.SubscriberName,
                    SubscriberSlug: slug,
                    AdminFirstName: command.AdminFirstName,
                    AdminLastName: command.AdminLastName,
                    AdminEmail: email,
                    AdminPassword: command.AdminPassword,
                    ActorId: _currentUser.UserId,
                    PasswordResetMode: command.PasswordResetMode,
                    Ruc: command.Ruc,
                    ShortName: command.ShortName,
                    TradeName: command.TradeName,
                    Dinardap: command.Dinardap,
                    LogoUrl: command.LogoUrl,
                    DisplayOrder: command.DisplayOrder,
                    Priority: command.Priority,
                    PlanCode: planCode,
                    EnabledModules: moduleKeys,
                    LinkExistingAdmin: false,
                    CountryCode: command.CountryCode,
                    Timezone: command.Timezone,
                    AuditDescription: $"{command.SubscriberName} ({slug})"),
                ct);

            return await BuildSessionResultAsync(provisioned.Subscriber, provisioned.AdminUser, provisioned.DefaultCompany, ct);
        }
        catch (CompanyRucAlreadyExistsException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message, CompanyRucAlreadyExistsException.ErrorCode);
        }
        catch (ArgumentException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message);
        }
    }

    private async Task<Result<SessionResponseDto>> HandleLinkExistingAdminAsync(
        PlatformCreateSubscriberWithAdminCommand command,
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

        try
        {
            var provisioned = await _provisioning.ProvisionSubscriberWithExistingAdminAsync(
                new SubscriberProvisioningRequest(
                    SubscriberName: command.SubscriberName,
                    SubscriberSlug: slug,
                    AdminFirstName: existingUser.FirstName,
                    AdminLastName: existingUser.LastName,
                    AdminEmail: email,
                    AdminPassword: null,
                    ActorId: _currentUser.UserId,
                    PasswordResetMode: command.PasswordResetMode,
                    Ruc: command.Ruc,
                    ShortName: command.ShortName,
                    TradeName: command.TradeName,
                    Dinardap: command.Dinardap,
                    LogoUrl: command.LogoUrl,
                    DisplayOrder: command.DisplayOrder,
                    Priority: command.Priority,
                    PlanCode: planCode,
                    EnabledModules: moduleKeys,
                    LinkExistingAdmin: true,
                    CountryCode: command.CountryCode,
                    Timezone: command.Timezone,
                    AuditDescription: $"{command.SubscriberName} ({slug}); admin vinculado {email}"),
                existingUser,
                ct);

            return await BuildSessionResultAsync(provisioned.Subscriber, provisioned.AdminUser, provisioned.DefaultCompany, ct);
        }
        catch (CompanyRucAlreadyExistsException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message, CompanyRucAlreadyExistsException.ErrorCode);
        }
        catch (ArgumentException ex)
        {
            return Result<SessionResponseDto>.Failure(ex.Message);
        }
    }

    private async Task<Result<SessionResponseDto>> BuildSessionResultAsync(
        ERP.Domain.Subscribers.Entities.Subscriber tenant,
        ERP.Domain.Access.Entities.IdentityUser adminUser,
        ERP.Domain.Modules.Company.Entities.Company defaultCompany,
        CancellationToken ct)
    {
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

    private static (string? PlanCode, IReadOnlyList<string>? ModuleKeys) NormalizeSubscription(string? planCode, List<string>? enabledModules)
    {
        var pc = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        if (enabledModules is null || enabledModules.Count == 0)
            return (pc, null);

        var cleaned = SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(enabledModules);
        return cleaned.Count == 0 ? (pc, null) : (pc, cleaned);
    }
}

public class GetPlatformSubscribersHandler : IRequestHandler<GetPlatformSubscribersQuery, Result<IReadOnlyList<PlatformSubscriberItemDto>>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ISubscriberMenuAdminService _subscriberMenuAdmin;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly IAccessRepository _accessRepository;

    public GetPlatformSubscribersHandler(
        ISubscriberRepository subscriberRepository,
        ISubscriberMenuAdminService subscriberMenuAdmin,
        ISessionModulesResolver sessionModules,
        IAccessRepository accessRepository)
    {
        _subscriberRepository = subscriberRepository;
        _subscriberMenuAdmin = subscriberMenuAdmin;
        _sessionModules = sessionModules;
        _accessRepository = accessRepository;
    }

    public async Task<Result<IReadOnlyList<PlatformSubscriberItemDto>>> Handle(GetPlatformSubscribersQuery request, CancellationToken ct)
    {
        var withCustom = await _subscriberMenuAdmin.GetSubscriberIdsWithCustomMenuAsync(ct);
        var ordered = (await _subscriberRepository.GetAllAsync(ct))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subscribers = new List<PlatformSubscriberItemDto>(ordered.Count);
        foreach (var t in ordered)
        {
            var modules = await _sessionModules.GetEnabledModuleKeysAsync(t.Id, ct);
            var (totalUsers, activeUsers) = await _accessRepository.CountDistinctIdentityUsersForSubscriberAsync(t.Id, ct);
            subscribers.Add(new PlatformSubscriberItemDto(
                t.Id,
                t.Name,
                t.Slug,
                t.IsActive,
                t.PlanCode,
                modules,
                SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(modules),
                withCustom.Contains(t.Id),
                t.CreatedAt,
                totalUsers,
                activeUsers));
        }

        return Result<IReadOnlyList<PlatformSubscriberItemDto>>.Success(subscribers);
    }
}
