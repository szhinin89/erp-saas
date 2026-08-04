using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ERP.Infrastructure.Seeding.E2E;

/// <summary>
/// Provisiona las precondiciones oficiales de la suite E2E. Solo puede ejecutarse
/// fuera de Production y bajo una bandera explícita de configuración.
/// </summary>
public sealed class E2ESeedService(
    IConfiguration configuration,
    IHostEnvironment environment,
    IAccessRepository accessRepository,
    ITenantRepository tenantRepository,
    ICompanyProvisioningService companyProvisioning,
    IPasswordHasher passwordHasher,
    ICompanyUserBranchRepository companyUserBranchRepository,
    ErpDbContext db
)
{
    private const string DefaultUsername = "e2e.admin";
    private const string DefaultEmail = "e2e.admin@zh.local";
    private const string DefaultTenantSlug = "zh-e2e-tenant";
    private const string DefaultTenantName = "ZH Technologies E2E Company";
    private const string SecondaryCompanyTaxId = "1790016919001";
    private const string SecondaryCompanyName = "ZH Technologies E2E Company B";

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("E2E:SeedEnabled", false);
        if (environment.IsProduction())
        {
            if (enabled)
                throw new InvalidOperationException("El seed E2E no puede ejecutarse en Production.");

            return;
        }

        if (!enabled)
            return;

        var password = configuration["E2E:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "E2E:Password es obligatorio cuando E2E:SeedEnabled=true."
            );

        var username = GetContractValue("E2E:Username", DefaultUsername);
        var email = GetContractValue("E2E:Email", DefaultEmail);
        var tenantSlug = GetContractValue("E2E:TenantCode", DefaultTenantSlug);
        var tenantName = GetContractValue("E2E:TenantName", DefaultTenantName);

        var user = await accessRepository.GetUserByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            user = IdentityUser.Create(
                username,
                firstName: "E2E",
                lastName: "Administrator",
                email,
                passwordHasher.HashPassword(password),
                createdBy: Guid.Empty
            );
            await accessRepository.AddUserAsync(user, cancellationToken);
            await accessRepository.SaveChangesAsync(cancellationToken);
        }
        else if (!passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            // La cuenta está reservada para E2E; sincronizar el hash con la contraseña
            // entregada por el entorno hace repetibles las corridas y no toca usuarios reales.
            user.SetPasswordHash(passwordHasher.HashPassword(password), user.Id);
            await accessRepository.SaveChangesAsync(cancellationToken);
        }

        var tenant = await tenantRepository.GetBySlugAsync(tenantSlug, cancellationToken);
        if (tenant is null)
        {
            tenant = Tenant.Create(tenantName, tenantSlug, user.Id);
            await tenantRepository.AddAsync(tenant, cancellationToken);
            await tenantRepository.SaveChangesAsync(cancellationToken);
        }

        var company = await companyProvisioning.EnsureDefaultCompanyAsync(tenant, cancellationToken);
        await EnsureCompanyAccessAsync(tenant.Id, company.Id, user.Id, cancellationToken);

        var secondaryCompany = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.TaxIdentificationNumber == SecondaryCompanyTaxId,
                cancellationToken
            );
        if (secondaryCompany is null)
        {
            secondaryCompany = await companyProvisioning.CreateManagedCompanyAsync(
                tenant.Id,
                SecondaryCompanyTaxId,
                SecondaryCompanyName,
                mainAddress: "Quito",
                user.Id,
                SecurityRoles.Admin,
                tradeName: "ZH E2E B",
                countryCode: "ECU",
                timezone: "America/Guayaquil",
                currencyCode: "USD",
                cancellationToken: cancellationToken
            );
        }

        await EnsureCompanyAccessAsync(tenant.Id, secondaryCompany.Id, user.Id, cancellationToken);

        await EnsureBusinessPartnerAsync(tenant.Id, user.Id, "1791352688001", "Cliente E2E ZH", RoleType.Customer, cancellationToken);
        await EnsureBusinessPartnerAsync(tenant.Id, user.Id, "1791352688001", "Proveedor E2E ZH", RoleType.Supplier, cancellationToken);
        await EnsureBusinessPartnerAsync(tenant.Id, user.Id, SecondaryCompanyTaxId, "Cliente E2E ZH B", RoleType.Customer, cancellationToken);
    }

    private async Task EnsureCompanyAccessAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var membership = await accessRepository.GetCompanyUserMembershipAsync(
            companyId,
            userId,
            cancellationToken
        );

        if (membership is null)
        {
            membership = CompanyUserMembership.Create(
                companyId,
                userId,
                SecurityRoles.Admin,
                profileId: null,
                createdBy: userId
            );
            await accessRepository.AddCompanyUserMembershipAsync(membership, cancellationToken);
            await accessRepository.SaveChangesAsync(cancellationToken);
        }
        else if (!membership.IsActive || membership.Role != SecurityRoles.Admin)
        {
            membership.Activate(SecurityRoles.Admin, profileId: null, updatedBy: userId);
            await accessRepository.SaveChangesAsync(cancellationToken);
        }

        var defaultBranch = await db.Branches.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.IsActive)
            .OrderByDescending(x => x.IsMainBranch)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("La empresa E2E no tiene una sucursal activa provisionada.");
        var branchMembership = (await companyUserBranchRepository.GetByMembershipAsync(membership.Id, cancellationToken))
            .FirstOrDefault(x => x.BranchId == defaultBranch.Id);
        if (branchMembership is null)
        {
            await companyUserBranchRepository.AddAsync(
                CompanyUserBranch.Create(tenantId, companyId, membership.Id, defaultBranch.Id, userId),
                cancellationToken
            );
            await companyUserBranchRepository.SaveChangesAsync(cancellationToken);
        }
        else if (!branchMembership.IsActive)
        {
            branchMembership.Activate(userId);
            await companyUserBranchRepository.UpdateAsync(branchMembership, cancellationToken);
            await companyUserBranchRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureBusinessPartnerAsync(Guid tenantId, Guid actorId, string ruc, string name, RoleType roleType, CancellationToken ct)
    {
        var partner = await db.BusinessPartners.IgnoreQueryFilters().FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Identification.Type == TaxIdentification.SriRuc && x.Identification.Number == ruc, ct);
        if (partner is null)
        {
            partner = BusinessPartner.Create(tenantId, TaxIdentification.SriRuc, ruc, null, name, actorId, countryCode: "EC");
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync(ct);
        }

        var role = await db.BusinessPartnerRoles.IgnoreQueryFilters().FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.BusinessPartnerId == partner.Id && x.RoleType == roleType, ct);
        if (role is null)
        {
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(tenantId, partner.Id, roleType, actorId));
            await db.SaveChangesAsync(ct);
        }
        else if (!role.IsActive)
        {
            role.Reactivate(actorId);
            await db.SaveChangesAsync(ct);
        }
    }

    private string GetContractValue(string key, string expectedValue)
    {
        var configuredValue = configuration[key]?.Trim();
        if (
            !string.IsNullOrWhiteSpace(configuredValue)
            && !string.Equals(configuredValue, expectedValue, StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException(
                $"{key} debe ser '{expectedValue}' para preservar el contrato E2E oficial."
            );

        return expectedValue;
    }
}
