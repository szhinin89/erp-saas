using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — backfill idempotente de
/// <see cref="ERP.Domain.Modules.Caja.Entities.CashRegister.AccountingAccountId"/> para cajas de
/// companies que ya existían antes de este ticket (creadas cuando Efectivo aún resolvía su cuenta
/// vía <c>PaymentMethodAccount</c>, retirado por este mismo ticket). Preserva exactamente el
/// comportamiento previo (Efectivo contabiliza contra "1.1.01.001 Caja general"): sin este
/// backfill, toda venta al contado de una company existente con una caja sin cuenta configurada
/// empezaría a fallar fail-closed en <c>AuthorizeSalesInvoiceHandler</c>.
///
/// Reglas de seguridad (mismo criterio que <c>PaymentMethodSriMappingBackfillService</c>/
/// <c>BankCatalogBackfillService</c>):
/// - Nunca pisa una caja que ya tiene AccountingAccountId (manual o de otro backfill) — solo
///   completa las que están en null.
/// - Solo actúa si la company tiene la cuenta "1.1.01.001" activa y postable — si no la tiene
///   (plan de cuentas atípico), se omite esa caja y se reporta, nunca se inventa una cuenta.
/// - Multi-tenant/multi-company seguro: itera vía IgnoreQueryFilters() porque corre fuera de
///   cualquier contexto de tenant autenticado (operación de despliegue).
///
/// Invocación: <c>dotnet run -- backfill-cash-register-accounting-account</c> (ver Program.cs). No
/// expone endpoint HTTP — operación de despliegue de una sola vez, re-ejecutable sin riesgo.
/// </summary>
public sealed class CashRegisterAccountingAccountBackfillService
{
    private const string CajaGeneralAccountCode = "1.1.01.001";

    /// <summary>Actor de sistema para operaciones sin usuario autenticado (mismo criterio que MasterDataClassificationBackfillService).</summary>
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly ILogger<CashRegisterAccountingAccountBackfillService> _logger;

    public CashRegisterAccountingAccountBackfillService(
        ErpDbContext db,
        ILogger<CashRegisterAccountingAccountBackfillService> logger
    )
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CashRegisterAccountingAccountBackfillResult> RunAsync(
        CancellationToken ct = default
    )
    {
        var cashRegistersWithoutAccount = await _db
            .CashRegisters.IgnoreQueryFilters()
            .Where(cr => cr.AccountingAccountId == null)
            .Select(cr => new
            {
                cr.Id,
                cr.TenantId,
                cr.CompanyId,
            })
            .ToListAsync(ct);

        var companiesProcessed = 0;
        var rowsUpdated = 0;
        var skippedNoCajaGeneralAccount = 0;

        foreach (var group in cashRegistersWithoutAccount.GroupBy(x => (x.TenantId, x.CompanyId)))
        {
            companiesProcessed++;

            var cajaGeneral = await _db
                .Accounts.IgnoreQueryFilters()
                .Where(a =>
                    a.TenantId == group.Key.TenantId
                    && a.CompanyId == group.Key.CompanyId
                    && a.Code == AccountCode.Create(CajaGeneralAccountCode)
                )
                .Select(a => new { a.Id, a.IsActive, a.AllowsPosting })
                .FirstOrDefaultAsync(ct);
            if (cajaGeneral is null || !cajaGeneral.IsActive || !cajaGeneral.AllowsPosting)
            {
                skippedNoCajaGeneralAccount++;
                _logger.LogWarning(
                    "Backfill CashRegister.AccountingAccountId: company {CompanyId} (tenant {TenantId}) "
                        + "no tiene una cuenta {AccountCode} activa/postable — se omite, requiere revisión manual.",
                    group.Key.CompanyId,
                    group.Key.TenantId,
                    CajaGeneralAccountCode
                );
                continue;
            }

            var cashRegisters = await _db
                .CashRegisters.IgnoreQueryFilters()
                .Where(cr => group.Select(g => g.Id).Contains(cr.Id))
                .ToListAsync(ct);
            foreach (var cashRegister in cashRegisters)
            {
                cashRegister.SetAccountingAccount(cajaGeneral.Id, SystemActor);
                rowsUpdated++;
            }
        }

        if (rowsUpdated > 0)
            await _db.SaveChangesAsync(ct);

        var result = new CashRegisterAccountingAccountBackfillResult(
            companiesProcessed,
            rowsUpdated,
            skippedNoCajaGeneralAccount
        );

        _logger.LogInformation(
            "Backfill CashRegister.AccountingAccountId: {CompaniesProcessed} company(s) con cajas "
                + "pendientes, {RowsUpdated} caja(s) actualizadas (-> Caja general), "
                + "{SkippedNoCajaGeneralAccount} company(s) sin cuenta Caja general activa/postable.",
            result.CompaniesProcessed,
            result.RowsUpdated,
            result.SkippedNoCajaGeneralAccount
        );

        return result;
    }
}

public sealed record CashRegisterAccountingAccountBackfillResult(
    int CompaniesProcessed,
    int RowsUpdated,
    int SkippedNoCajaGeneralAccount
);
