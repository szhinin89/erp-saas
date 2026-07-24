using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Único punto de creación de <see cref="Company"/> en producción. Ambos métodos públicos dejan
/// la empresa completamente bootstrapeada (sucursal, bodega, establecimiento, punto de emisión,
/// numeraciones, formas de pago, tipos de ítem, consumidor final, lista de precios, condición de
/// pago, perfiles de acceso por defecto) antes de retornar — un único punto de invocación de
/// <see cref="ICompanyBootstrapService"/>, nunca hay que llamarlo por separado desde el caller.
/// </summary>
public sealed class CompanyProvisioningService : ICompanyProvisioningService
{
    private readonly ICompanyRepository        _companies;
    private readonly IAccessRepository         _access;
    private readonly ICompanyBootstrapService  _bootstrap;

    public CompanyProvisioningService(
        ICompanyRepository companies,
        IAccessRepository access,
        ICompanyBootstrapService bootstrap)
    {
        _companies = companies;
        _access    = access;
        _bootstrap = bootstrap;
    }

    public async Task<Company> EnsureDefaultCompanyAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var active = await _companies.GetActiveByTenantIdAsync(tenant.Id, cancellationToken);
        if (active.Count > 0)
            return active[0];

        var company = await CreateDefaultCompanyForTenantAsync(tenant, cancellationToken: cancellationToken);
        await _companies.AddAsync(company, cancellationToken);

        // Actor de sistema: esta rama solo se activa cuando el tenant se quedó sin ninguna
        // empresa activa (auto-sanación fuera de un flujo de creación explícito con usuario real).
        var systemActorId = Guid.NewGuid();

        // El bootstrap (PriceList, PricingRule, etc.) dispara domain events que los *AuditHandler
        // resuelven vía IAuditContext → ICurrentCompany, la cual lee de HttpContext/header y no
        // tiene ningún valor todavía aquí (la empresa recién se está creando, nunca fue "la
        // empresa actual" de ningún request). JobExecutionContext es el mecanismo ya existente
        // para este caso (mismo usado por ElectronicDocumentRetryJob) — sin él, cualquier
        // *AuditHandler que dependa de CompanyId lanza ArgumentException("companyId requerido").
        using var _ = JobExecutionContext.Begin(tenant.Id, company.Id);
        await _bootstrap.BootstrapCompanyAsync(tenant.Id, company.Id, systemActorId, cancellationToken);

        return company;
    }

    public async Task<Company> CreateDefaultCompanyForTenantAsync(
        Tenant tenant,
        string? countryCode = "ECU",
        string? timezone    = "America/Guayaquil",
        CancellationToken cancellationToken = default)
    {
        var (taxId, isProvisional, status) = await ResolveTaxIdAsync(null, cancellationToken);

        return Company.CreateManaged(
            tenant.Id,
            taxId,
            legalName:    tenant.Name,
            tradeName:    null,
            corporateEmail: null,
            countryCode:  string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode,
            timezone:     string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone,
            currencyCode: "USD",
            isTemporaryTaxIdentification: isProvisional,
            taxIdentificationStatus:  status);
    }

    /// <summary>
    /// <paramref name="mainAddress"/> y <paramref name="phone"/> se aceptan por compatibilidad con
    /// <c>IntegrationCompanyCreateRequest</c>/<c>CompanyProvisionRequest</c> (Platform), pero ya no
    /// se persisten en <see cref="Company"/> (pendiente de moverse a Branch).
    /// </summary>
    public async Task<Company> CreateManagedCompanyAsync(
        Guid tenantId,
        string ruc,
        string legalName,
        string mainAddress,
        Guid createdByUserId,
        string creatorRole,
        string? tradeName    = null,
        string? email        = null,
        string? phone        = null,
        string countryCode   = "ECU",
        string timezone      = "America/Guayaquil",
        string currencyCode  = "USD",
        string? brandingJson = null,
        string? website      = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvidedTaxId(ruc);
        await EnsureTaxIdAvailableGloballyAsync(normalized, cancellationToken);

        var company = Company.CreateManaged(
            tenantId, normalized, legalName,
            tradeName, corporateEmail: email, countryCode, timezone, currencyCode,
            brandingJson, website,
            isTemporaryTaxIdentification: ProvisionalTaxIdGenerator.IsProvisional(normalized),
            taxIdentificationStatus: ProvisionalTaxIdGenerator.IsProvisional(normalized)
                ? TaxIdentificationStatus.Pending
                : TaxIdentificationStatus.Verified,
            createdBy: createdByUserId);

        await _companies.AddAsync(company, cancellationToken);
        await _companies.SaveChangesAsync(cancellationToken);

        var membership = CompanyUserMembership.Create(
            company.Id, createdByUserId, creatorRole, profileId: null, createdByUserId);
        await _access.AddCompanyUserMembershipAsync(membership, cancellationToken);
        await _access.SaveChangesAsync(cancellationToken);

        // Ver comentario equivalente en EnsureDefaultCompanyAsync: el bootstrap dispara domain
        // events auditados que necesitan CompanyId, y esta empresa nueva nunca fue "la empresa
        // actual" de ningún HttpContext todavía.
        using var _ = JobExecutionContext.Begin(tenantId, company.Id);
        await _bootstrap.BootstrapCompanyAsync(tenantId, company.Id, createdByUserId, cancellationToken);

        return company;
    }

    private async Task<(string TaxId, bool IsProvisional, TaxIdentificationStatus Status)> ResolveTaxIdAsync(
        string? providedRuc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedRuc))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var provisional = ProvisionalTaxIdGenerator.Create();
                if (await _companies.GetByTaxIdentificationNumberAsync(provisional, cancellationToken) is null)
                    return (provisional, true, TaxIdentificationStatus.Pending);
            }
            throw new InvalidOperationException("No se pudo generar un RUC provisional único.");
        }

        var normalized = NormalizeProvidedTaxId(providedRuc);
        await EnsureTaxIdAvailableGloballyAsync(normalized, cancellationToken);
        return (normalized, false, TaxIdentificationStatus.Verified);
    }

    private static string NormalizeProvidedTaxId(string ruc) =>
        ProvisionalTaxIdGenerator.IsProvisional(ruc)
            ? ruc.Trim()
            : ProvisionalTaxIdGenerator.NormalizeOfficial(ruc);

    private async Task EnsureTaxIdAvailableGloballyAsync(string taxId, CancellationToken cancellationToken)
    {
        var existing = await _companies.GetByTaxIdentificationNumberAsync(taxId, cancellationToken);
        if (existing is not null)
            throw new CompanyRucAlreadyExistsException(taxId);
    }
}
