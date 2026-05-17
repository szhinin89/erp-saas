using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Products.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Extensions;

/// <summary>
/// Seed opcional de desarrollo: tenant-demo + admin identity + datos mÃ­nimos contables.
/// <b>No se ejecuta por defecto.</b> ActÃ­valo con <c>Development:SeedDemoTenant = true</c> en appsettings.Development.json.
/// </summary>
internal static class DevDatabaseSeeder
{
    private static readonly Guid SeederActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedMinimumAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db             = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var onboarding     = scope.ServiceProvider.GetRequiredService<ITenantOnboardingService>();

        const string adminEmail = "admin@erp.com";
        const string adminPassword = "Admin123!";
        const string tenantSlug = "tenant-demo";

        var existingAdmins = await db.IdentityUsers
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        var existingAdmin = existingAdmins
            .FirstOrDefault(u => string.Equals(u.Email.Value, adminEmail, StringComparison.OrdinalIgnoreCase));

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == tenantSlug, ct);

        if (tenant is not null && existingAdmin is not null)
            return;

        var tenantJustCreated = false;
        if (tenant is null)
        {
            tenant = Tenant.Create(
                name: "Tenant Demo",
                slug: tenantSlug,
                createdBy: SeederActorId,
                passwordResetMode: PasswordResetMode.Direct);

            db.Tenants.Add(tenant);
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

        var hasMembership = await db.Memberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenant.Id && m.IdentityUserId == admin.Id && m.IsActive, ct);
        if (!hasMembership)
        {
            db.Memberships.Add(Membership.Create(
                tenantId: tenant.Id,
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

        var branch = Branch.Create(
            tenantId:        tenant.Id,
            name:            "Sucursal Principal",
            address:         "Dirección Principal",
            code:            "SUC-SEED-001",
            branchType:      null,
            reference:       null,
            phones:          null,
            email:           null,
            managerName:     null,
            countryId:       null,
            provinceId:      null,
            cantonId:        null,
            parishId:        null,
            latitude:        null,
            longitude:       null,
            storageCapacity: null,
            dailySalesGoal:  null,
            rechargeOption:  null,
            isMainBranch:    true,
            createdBy:       SeederActorId);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);

        db.Warehouses.Add(Warehouse.Create(
            tenantId:          tenant.Id,
            branchId:          branch.Id,
            name:              "Warehouse Principal",
            code:              "WH-SEED-001",
            storageType:       null,
            address:           null,
            phone:             null,
            email:             null,
            manager:           null,
            latitude:          null,
            longitude:         null,
            capacity:          null,
            dailyDispatchGoal: null,
            createdBy:         SeederActorId));

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
    }

    /// <summary>
    /// Seeds the default business access profiles (Facturador, Bodeguero, Contador).
    /// Idempotent: skips profiles that already exist by name.
    /// </summary>
    public static async Task SeedDefaultProfilesAsync(
        ErpDbContext db, Guid tenantId, CancellationToken ct = default)
    {
        var profiles = new[]
        {
            ("Facturador",  "Billing operator — can create and void invoices.",   Permissions.FacilitadorProfile),
            ("Bodeguero",   "Warehouse operator — manages stock and transfers.",  Permissions.BodegueroProfile),
            ("Contador",    "Accountant — read-only access to accounting data.", Permissions.ContadorProfile),
        };

        foreach (var (name, description, permKeys) in profiles)
        {
            var existing = await db.AccessProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == name, ct);

            if (existing is not null) continue;

            var profile = AccessProfile.Create(
                tenantId:    tenantId,
                name:        name,
                description: description,
                createdBy:   SeederActorId);

            db.AccessProfiles.Add(profile);
            await db.SaveChangesAsync(ct);

            var permissions = permKeys.Select(key =>
                AccessProfilePermission.Create(
                    tenantId:      tenantId,
                    profileId:     profile.Id,
                    permissionKey: key,
                    isAllowed:     true,
                    createdBy:     SeederActorId));

            db.AccessProfilePermissions.AddRange(permissions);
            await db.SaveChangesAsync(ct);
        }
    }
}


