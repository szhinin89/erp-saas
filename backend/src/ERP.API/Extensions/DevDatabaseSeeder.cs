using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Products.Entities;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Extensions;

/// <summary>
/// Seed opcional de desarrollo: subscriber-demo + admin identity + datos mÃ­nimos contables.
/// <b>No se ejecuta por defecto.</b> ActÃ­valo con <c>Development:SeedDemoSubscriber = true</c> en appsettings.Development.json.
/// </summary>
internal static class DevDatabaseSeeder
{
    private static readonly Guid SeederActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly (string Code, string ResourceRef)[] DemoStarterModules =
    [
        ("SALES", "sales"),
        ("INVENTORY", "inventory"),
        ("PURCHASES", "purchases"),
        ("EXPENSES", "expenses"),
        ("ACCOUNTING", "accounting"),
        ("ACCESS", "access"),
        ("LOGISTICS", "logistics"),
    ];

    public static async Task SeedMinimumAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db             = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var platform       = scope.ServiceProvider.GetRequiredService<IPlatformQueryAccessor>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var onboarding          = scope.ServiceProvider.GetRequiredService<ISubscriberOnboardingService>();
        var companyProvisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
        var companyBootstrap    = scope.ServiceProvider.GetRequiredService<ICompanyBootstrapService>();

        const string adminEmail = "admin@erp.com";
        const string adminPassword = "Admin123!";
        const string platformOperatorEmail = "platform@erp.com";
        const string platformOperatorPassword = "Admin123!";
        const string subscriberSlug = "subscriber-demo";

        await EnsureDevPlatformOperatorAsync(db, platform, passwordHasher, platformOperatorEmail, platformOperatorPassword, ct);

        var existingAdmins = await platform
            .Unfiltered(db.IdentityUsers, PlatformQueryReason.DevOnly)
            .ToListAsync(ct);
        var existingAdmin = existingAdmins
            .FirstOrDefault(u => string.Equals(u.Email.Value, adminEmail, StringComparison.OrdinalIgnoreCase));

        var tenant = await platform
            .Unfiltered(db.Subscribers, PlatformQueryReason.DevOnly)
            .FirstOrDefaultAsync(t => t.Slug == subscriberSlug, ct);

        if (tenant is not null && existingAdmin is not null)
        {
            if (string.IsNullOrWhiteSpace(tenant.PlanCode))
            {
                tenant.SetPlanCode("starter", SeederActorId);
            }

            // Dev: restablecer credenciales demo conocidas (p. ej. tras cambio manual de contraseña).
            existingAdmin.SetPasswordHash(passwordHasher.HashPassword(adminPassword), SeederActorId);

            var defaultCompany = await companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
            var hasActiveMembership = await platform
                .Unfiltered(db.CompanyUserMemberships, PlatformQueryReason.DevOnly)
                .AnyAsync(m => m.CompanyId == defaultCompany.Id && m.IdentityUserId == existingAdmin.Id && m.IsActive, ct);
            if (!hasActiveMembership)
            {
                db.CompanyUserMemberships.Add(CompanyUserMembership.Create(
                    companyId: defaultCompany.Id,
                    identityUserId: existingAdmin.Id,
                    role: "Admin",
                    profileId: null,
                    createdBy: SeederActorId));
            }

            await db.SaveChangesAsync(ct);
            await EnsureDemoStarterEntitlementsAsync(db, platform, ct);
            return;
        }

        var tenantJustCreated = false;
        if (tenant is null)
        {
            tenant = Subscriber.Create(
                name: "Subscriber Demo",
                slug: subscriberSlug,
                createdBy: SeederActorId,
                passwordResetMode: PasswordResetMode.Direct,
                planCode: "starter");

            db.Subscribers.Add(tenant);
            await db.SaveChangesAsync(ct);
            tenantJustCreated = true;
        }

        var admin = existingAdmin;
        if (admin is null)
        {
            admin = IdentityUser.Create(
                firstName: "Admin",
                lastName: "ERP",
                email: adminEmail,
                passwordHash: passwordHasher.HashPassword(adminPassword),
                createdBy: SeederActorId);
            db.IdentityUsers.Add(admin);
            await db.SaveChangesAsync(ct);
        }

        var defaultCompanyForSeed = await companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        var hasCompanyUserMembership = await platform
            .Unfiltered(db.CompanyUserMemberships, PlatformQueryReason.DevOnly)
            .AnyAsync(m => m.CompanyId == defaultCompanyForSeed.Id && m.IdentityUserId == admin.Id && m.IsActive, ct);
        if (!hasCompanyUserMembership)
        {
            db.CompanyUserMemberships.Add(CompanyUserMembership.Create(
                companyId: defaultCompanyForSeed.Id,
                identityUserId: admin.Id,
                role: "Admin",
                profileId: null,
                createdBy: SeederActorId));
        }

        // Si el tenant ya existÃ­a, solo completamos identidad/membresÃ­a faltante.
        if (!tenantJustCreated)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        await companyBootstrap.BootstrapCompanyAsync(tenant.Id, defaultCompanyForSeed.Id, SeederActorId, ct);

        db.Accounts.AddRange(
            Account.Create(tenant.Id, "1.1.01", "Caja General", AccountType.Asset, AccountNature.Debit, SeederActorId),
            Account.Create(tenant.Id, "1.1.02", "Inventario Mercaderia", AccountType.Asset, AccountNature.Debit, SeederActorId),
            Account.Create(tenant.Id, "2.1.01", "Cuentas por Pagar", AccountType.Liability, AccountNature.Credit, SeederActorId),
            Account.Create(tenant.Id, "4.1.01", "Ventas", AccountType.Revenue, AccountNature.Credit, SeederActorId),
            Account.Create(tenant.Id, "5.1.01", "Gastos Operativos", AccountType.Expense, AccountNature.Debit, SeederActorId));

        db.TaxRates.AddRange(
            TaxRate.Create(tenant.Id, "IVA12", "IVA 12%", TaxRateType.VAT, 12m, SeederActorId),
            TaxRate.Create(tenant.Id, "IVA0", "IVA 0%", TaxRateType.VAT, 0m, SeederActorId));

        await db.SaveChangesAsync(ct);

        // ── Full tenant onboarding (profiles + Consumidor Final + branch + warehouse) ──
        await onboarding.OnboardAsync(tenant.Id, SeederActorId, ct);
        await EnsureDemoStarterEntitlementsAsync(db, platform, ct);
    }

    /// <summary>Features Module en plan starter para probar entitlements (Fase A) en desarrollo.</summary>
    private static async Task EnsureDemoStarterEntitlementsAsync(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        CancellationToken ct)
    {
        const string subscriberSlug = "subscriber-demo";
        var plan = await db.CommercialPlans.FirstOrDefaultAsync(p => p.Code == "starter", ct);
        if (plan is null)
            return;

        var tenant = await platform
            .Unfiltered(db.Subscribers, PlatformQueryReason.DevOnly)
            .FirstOrDefaultAsync(t => t.Slug == subscriberSlug, ct);
        if (tenant is not null)
        {
            var subscription = await platform
                .Unfiltered(db.SubscriberSubscriptions, PlatformQueryReason.DevOnly)
                .FirstOrDefaultAsync(s => s.SubscriberId == tenant.Id, ct);
            if (subscription is null)
            {
                db.SubscriberSubscriptions.Add(
                    SubscriberSubscription.Create(tenant.Id, plan.Id, SeederActorId));
                await db.SaveChangesAsync(ct);
            }
        }

        foreach (var (code, resourceRef) in DemoStarterModules)
        {
            var feature = await db.PlatformFeatures
                .FirstOrDefaultAsync(f => f.Code == code, ct);
            if (feature is null)
            {
                feature = PlatformFeature.Create(
                    code, code, null, isMetered: false, PlatformFeatureKind.Module, resourceRef);
                db.PlatformFeatures.Add(feature);
                await db.SaveChangesAsync(ct);
            }

            var linked = await db.CommercialPlanFeatures
                .AnyAsync(pf => pf.PlanId == plan.Id && pf.FeatureId == feature.Id, ct);
            if (!linked)
            {
                db.CommercialPlanFeatures.Add(
                    CommercialPlanFeature.Create(plan.Id, feature.Id, isIncluded: true, limitPerPeriod: null));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the default business access profiles (Facturador, Bodeguero, Contador).
    /// Idempotent: skips profiles that already exist by name.
    /// </summary>
    public static async Task SeedDefaultProfilesAsync(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        Guid subscriberId,
        CancellationToken ct = default)
    {
        var profiles = new[]
        {
            ("Facturador",  "Billing operator — can create and void invoices.",   Permissions.FacilitadorProfile),
            ("Bodeguero",   "Warehouse operator — manages stock and transfers.",  Permissions.BodegueroProfile),
            ("Contador",    "Accountant — read-only access to accounting data.", Permissions.ContadorProfile),
        };

        foreach (var (name, description, permKeys) in profiles)
        {
            var existing = await platform
                .Unfiltered(db.AccessProfiles, PlatformQueryReason.DevOnly)
                .FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.Name == name, ct);

            if (existing is not null) continue;

            var profile = AccessProfile.Create(
                subscriberId:    subscriberId,
                name:        name,
                description: description,
                createdBy:   SeederActorId);

            db.AccessProfiles.Add(profile);
            await db.SaveChangesAsync(ct);

            var permissions = permKeys.Select(key =>
                AccessProfilePermission.Create(
                    subscriberId:      subscriberId,
                    profileId:     profile.Id,
                    permissionKey: key,
                    isAllowed:     true,
                    createdBy:     SeederActorId));

            db.AccessProfilePermissions.AddRange(permissions);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureDevPlatformOperatorAsync(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        IPasswordHasher passwordHasher,
        string email,
        string password,
        CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await platform
            .Unfiltered(db.IdentityUsers, PlatformQueryReason.DevOnly)
            .FirstOrDefaultAsync(u => u.EmailNormalized == normalized, ct);

        if (existing is not null)
        {
            if (existing.UserType == IdentityUserType.Platform && existing.PlatformRole == PlatformRole.PlatformOperator)
                existing.SetPasswordHash(passwordHasher.HashPassword(password), SeederActorId);
            await db.SaveChangesAsync(ct);
            return;
        }

        db.IdentityUsers.Add(IdentityUser.CreatePlatformOperator(
            "Super",
            "Admin",
            email,
            passwordHasher.HashPassword(password),
            SeederActorId));
        await db.SaveChangesAsync(ct);
    }
}


