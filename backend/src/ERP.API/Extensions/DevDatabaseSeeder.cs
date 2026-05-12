using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Contabilidad.Entities;
using ERP.Domain.Modules.Contabilidad.Enums;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Products.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Extensions;

internal static class DevDatabaseSeeder
{
    private static readonly Guid SeederActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedMinimumAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

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

        // Si el tenant ya existía, solo completamos identidad/membresía faltante.
        if (!tenantJustCreated)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Sucursal Principal",
            address: "Dirección Principal",
            reference: null,
            phones: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            rechargeOption: null,
            isMainBranch: true,
            createdBy: SeederActorId);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);

        db.Bodegas.Add(Bodega.Create(
            tenantId: tenant.Id,
            sucursalId: branch.Id,
            nombre: "Bodega Principal",
            ubicacion: null,
            encargado: null,
            createdBy: SeederActorId));

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
    }
}
