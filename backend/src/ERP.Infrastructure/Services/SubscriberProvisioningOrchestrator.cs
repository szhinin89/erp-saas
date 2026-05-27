using System.Data;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Access.Entities;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class SubscriberProvisioningOrchestrator : ISubscriberProvisioningOrchestrator
{
    private readonly ErpDbContext _db;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly ISubscriberOnboardingService _onboarding;
    private readonly ISubscriptionFeatureOverridesService _overrides;
    private readonly IUserActivityRepository _activity;
    private readonly ICommercialPlanLimitService _planLimits;
    private readonly IPasswordHasher _passwordHasher;

    public SubscriberProvisioningOrchestrator(
        ErpDbContext db,
        ICompanyProvisioningService companyProvisioning,
        ISubscriberOnboardingService onboarding,
        ISubscriptionFeatureOverridesService overrides,
        IUserActivityRepository activity,
        ICommercialPlanLimitService planLimits,
        IPasswordHasher passwordHasher)
    {
        _db = db;
        _companyProvisioning = companyProvisioning;
        _onboarding = onboarding;
        _overrides = overrides;
        _activity = activity;
        _planLimits = planLimits;
        _passwordHasher = passwordHasher;
    }

    public Task<SubscriberProvisioningResult> ProvisionNewSubscriberWithAdminAsync(
        SubscriberProvisioningRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AdminPassword))
            throw new ArgumentException("La contraseña del administrador es obligatoria.");

        var email = request.AdminEmail.Trim().ToLowerInvariant();
        var hash = _passwordHasher.HashPassword(request.AdminPassword);
        var adminUser = IdentityUser.Create(
            request.AdminFirstName,
            request.AdminLastName,
            email,
            hash,
            request.ActorId);

        return ProvisionCoreAsync(request, adminUser, ct);
    }

    public Task<SubscriberProvisioningResult> ProvisionSubscriberWithExistingAdminAsync(
        SubscriberProvisioningRequest request,
        IdentityUser existingAdmin,
        CancellationToken ct = default)
        => ProvisionCoreAsync(request, existingAdmin, ct);

    private async Task<SubscriberProvisioningResult> ProvisionCoreAsync(
        SubscriberProvisioningRequest request,
        IdentityUser adminUser,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var subscriber = Subscriber.Create(
                request.SubscriberName,
                request.SubscriberSlug,
                createdBy: request.ActorId,
                passwordResetMode: request.PasswordResetMode,
                displayOrder: request.DisplayOrder,
                priority: request.Priority,
                planCode: request.PlanCode);

            _db.Subscribers.Add(subscriber);

            await EnsureBillingAccountInContextAsync(subscriber.Id, adminUser.Email.Value, request.ActorId, ct);

            if (!request.LinkExistingAdmin)
                _db.IdentityUsers.Add(adminUser);

            if (!string.IsNullOrWhiteSpace(request.AuditDescription))
            {
                await _activity.AddAsync(UserActivity.Create(
                    subscriber.Id,
                    request.ActorId,
                    adminUser.Email.Value,
                    adminUser.FullName,
                    module: "subscribers",
                    action: "tenant.create",
                    entityType: "Subscriber",
                    entityId: subscriber.Id,
                    description: request.AuditDescription), ct);
            }

            await _db.SaveChangesAsync(ct);

            await _planLimits.EnsureCanIncrementAsync(
                subscriber.Id,
                CommercialPlanLimit.Codes.MaxCompanies,
                increment: 1,
                ct);

            var company = await _companyProvisioning.CreateDefaultCompanyForSubscriberAsync(
                subscriber,
                countryCode: request.CountryCode,
                timezone: request.Timezone,
                ct: ct);

            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);

            var membership = CompanyUserMembership.Create(
                company.Id,
                adminUser.Id,
                "Admin",
                profileId: null,
                createdBy: request.ActorId);
            _db.CompanyUserMemberships.Add(membership);

            if (request.EnabledModules is { Count: > 0 })
            {
                await _overrides.ApplyModuleOverridesAsync(
                    subscriber.Id,
                    SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(request.EnabledModules.ToList()),
                    request.ActorId,
                    ct);
            }

            await _db.SaveChangesAsync(ct);
            await _onboarding.OnboardAsync(subscriber.Id, request.ActorId, ct);
            await tx.CommitAsync(ct);

            return new SubscriberProvisioningResult(subscriber, adminUser, company, membership);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task EnsureBillingAccountInContextAsync(
        Guid subscriberId,
        string billingEmail,
        Guid actorId,
        CancellationToken ct)
    {
        var exists = await _db.SubscriberBillingAccounts
            .AsNoTracking()
            .AnyAsync(a => a.SubscriberId == subscriberId, ct);
        if (exists)
            return;

        var account = SubscriberBillingAccount.Create(subscriberId, billingEmail, actorId);
        _db.SubscriberBillingAccounts.Add(account);
        _db.BillingEvents.Add(BillingEvent.Record(
            subscriberId,
            BillingEvent.Types.AccountCreated,
            BillingEventSource.System,
            actorId));
    }
}
