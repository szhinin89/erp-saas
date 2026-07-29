using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Configuración comercial mínima de Ventas: formas de pago por defecto, cliente Consumidor
/// Final, lista de precios general y condición de pago Contado. Todos los pasos son idempotentes.
/// </summary>
public sealed partial class SalesBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Sales;

    private const string DefaultPriceListCode = "GENERAL";
    private const string DefaultPriceListName = "Lista General";
    private const string DefaultPaymentTermCode = "CONTADO";
    private const string DefaultPaymentTermName = "Contado";
    private const string ConsumidorFinalName = "Consumidor Final";

    private static readonly (string Code, string Name, bool RequiresRef, bool CreditAllowed, int Sort, PaymentMethodDetailType DetailType)[] DefaultPaymentMethods =
    [
        ("EFECTIVO",      "Efectivo",              false, false, 1, PaymentMethodDetailType.None),
        ("TARJETA",       "Tarjeta de Crédito",    true,  false, 2, PaymentMethodDetailType.Card),
        ("TRANSFERENCIA", "Transferencia Bancaria", true,  false, 3, PaymentMethodDetailType.Transfer),
        ("CHEQUE",        "Cheque",                 true,  false, 4, PaymentMethodDetailType.Check),
        ("CREDITO",       "Crédito",                false, true,  5, PaymentMethodDetailType.None),
    ];

    private readonly ErpDbContext _db;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ILogger<SalesBootstrapStep> _logger;

    public SalesBootstrapStep(ErpDbContext db, IDatabaseExceptionTranslator dbEx, ILogger<SalesBootstrapStep> logger)
    {
        _db = db;
        _dbEx = dbEx;
        _logger = logger;
    }

    public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
    {
        var (tenantId, companyId, actorId) = context;

        await SeedDefaultPaymentMethodsAsync(tenantId, actorId, cancellationToken);
        await SeedConsumidorFinalAsync(tenantId, actorId, cancellationToken);
        await SeedDefaultPriceListAsync(tenantId, companyId, actorId, cancellationToken);
        await SeedDefaultPaymentTermAsync(tenantId, actorId, cancellationToken);
    }

    private async Task SeedDefaultPaymentMethodsAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var existingCodes = await _db.PaymentMethods.IgnoreQueryFilters()
            .Where(pm => pm.TenantId == tenantId)
            .Select(pm => pm.Code)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (code, name, requiresRef, creditAllowed, sort, detailType) in DefaultPaymentMethods)
        {
            if (existingSet.Contains(code))
            {
                LogPaymentMethodSkipped(code, tenantId);
                continue;
            }

            var pm = PaymentMethod.CreateSystemSeeded(tenantId, code, name, requiresRef, creditAllowed, sort, actorId, detailType);
            _db.PaymentMethods.Add(pm);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            LogPaymentMethodsSeeded(added, tenantId);
        }
    }

    private async Task SeedConsumidorFinalAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.BusinessPartners.IgnoreQueryFilters()
            .AnyAsync(bp => bp.TenantId == tenantId
                         && bp.Identification.Type == TaxIdentification.SriConsumidorFinal
                         && bp.Identification.Number == TaxIdentification.ConsumidorFinalNumber, ct);

        if (exists)
        {
            LogConsumidorFinalSkipped(tenantId);
            return;
        }

        var companyCountryCode = await _db.Companies.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.CountryCode)
            .FirstOrDefaultAsync(ct);

        // Company.CountryCode es alpha-3 (convención SRI — ver SriCountryConfiguration/"ECU"), pero
        // BusinessPartner.CountryCode exige alpha-2 (BusinessPartner.NormalizeCountryCode). Se
        // resuelve desde el catálogo oficial sri_country (Code alpha-3 → Iso2 alpha-2) — nunca se
        // asume ni se trunca el código a mano; si no hay match, queda null (BusinessPartner lo acepta).
        var iso2CountryCode = string.IsNullOrWhiteSpace(companyCountryCode)
            ? null
            : await _db.SriCountries.AsNoTracking()
                .Where(c => c.Code == companyCountryCode)
                .Select(c => c.Iso2)
                .FirstOrDefaultAsync(ct);

        var bp = BusinessPartner.CreateSystemSeeded(
            tenantId: tenantId,
            identificationType: TaxIdentification.SriConsumidorFinal,
            identificationNumber: TaxIdentification.ConsumidorFinalNumber,
            personType: PersonType.Natural,
            legalName: ConsumidorFinalName,
            createdBy: actorId,
            countryCode: iso2CountryCode);

        _db.BusinessPartners.Add(bp);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            // Carrera de bootstrap: otra ejecución concurrente ya sembró el Consumidor Final
            // de este tenant entre el chequeo IgnoreQueryFilters() y este SaveChanges. La
            // restricción UNIQUE de BD (uq_mbp_identification) es la barrera real; el chequeo
            // previo es solo optimización para el caso común (evita el roundtrip de excepción).
            LogConsumidorFinalRaceDetected(tenantId);
            return;
        }

        var role = BusinessPartnerRole.Create(
            tenantId: tenantId,
            businessPartnerId: bp.Id,
            roleType: RoleType.Customer,
            assignedBy: actorId);

        _db.BusinessPartnerRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        LogConsumidorFinalSeeded(tenantId, bp.Id);
    }

    private async Task SeedDefaultPriceListAsync(Guid tenantId, Guid companyId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.PriceLists.IgnoreQueryFilters()
            .AnyAsync(pl => pl.TenantId == tenantId
                         && pl.CompanyId == companyId
                         && pl.Code == DefaultPriceListCode, ct);

        if (exists)
        {
            LogPriceListSkipped(DefaultPriceListCode, companyId);
            return;
        }

        var currencyCode = await _db.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => c.CurrencyCode)
            .FirstAsync(ct);

        var priceList = PriceList.CreateSystemSeeded(
            tenantId: tenantId,
            companyId: companyId,
            code: DefaultPriceListCode,
            name: DefaultPriceListName,
            currencyCode: currencyCode,
            isDefault: true,
            createdBy: actorId);

        _db.PriceLists.Add(priceList);
        await _db.SaveChangesAsync(ct);

        LogPriceListSeeded(DefaultPriceListCode, companyId, priceList.Id);
    }

    private async Task SeedDefaultPaymentTermAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.PaymentTerms.IgnoreQueryFilters()
            .AnyAsync(pt => pt.TenantId == tenantId && pt.Code == DefaultPaymentTermCode, ct);

        if (exists)
        {
            LogPaymentTermSkipped(DefaultPaymentTermCode, tenantId);
            return;
        }

        var paymentTerm = PaymentTerm.CreateSystemSeeded(
            tenantId: tenantId,
            code: DefaultPaymentTermCode,
            name: DefaultPaymentTermName,
            installments: 1,
            daysBetweenInstallments: 0,
            createdBy: actorId);

        _db.PaymentTerms.Add(paymentTerm);
        await _db.SaveChangesAsync(ct);

        LogPaymentTermSeeded(DefaultPaymentTermCode, tenantId, paymentTerm.Id);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "PaymentMethod {Code} already exists for tenant {TenantId}. Skipping.")]
    private partial void LogPaymentMethodSkipped(string code, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} default payment method(s) seeded for tenant {TenantId}.")]
    private partial void LogPaymentMethodsSeeded(int count, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumidor Final already exists for tenant {TenantId}. Skipping.")]
    private partial void LogConsumidorFinalSkipped(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumidor Final seeded for tenant {TenantId} (id={BusinessPartnerId}).")]
    private partial void LogConsumidorFinalSeeded(Guid tenantId, Guid businessPartnerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumidor Final unique violation for tenant {TenantId} — already seeded by a concurrent bootstrap run.")]
    private partial void LogConsumidorFinalRaceDetected(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PriceList {Code} already exists for company {CompanyId}. Skipping.")]
    private partial void LogPriceListSkipped(string code, Guid companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "PriceList {Code} seeded for company {CompanyId} (id={PriceListId}).")]
    private partial void LogPriceListSeeded(string code, Guid companyId, Guid priceListId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PaymentTerm {Code} already exists for tenant {TenantId}. Skipping.")]
    private partial void LogPaymentTermSkipped(string code, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "PaymentTerm {Code} seeded for tenant {TenantId} (id={PaymentTermId}).")]
    private partial void LogPaymentTermSeeded(string code, Guid tenantId, Guid paymentTermId);
}
