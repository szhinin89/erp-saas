using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — backfill idempotente de
/// <see cref="PaymentMethodAccount"/> para el método de pago "EFECTIVO" de cada Company activa que
/// ya existía antes de este ticket. Preserva exactamente el comportamiento previo (Efectivo
/// contabiliza contra "1.1.01.001 Caja general"): sin este backfill, toda venta al contado de una
/// company existente empezaría a fallar fail-closed (ningún PaymentMethodAccount configurado) en
/// vez de seguir contabilizando contra Caja general como siempre.
///
/// Deliberadamente NO siembra cuenta alguna para TRANSFERENCIA/TARJETA/CHEQUE — a diferencia de
/// Efectivo, esos métodos nunca tuvieron una cuenta real asociada (el bug reportado por este mismo
/// ticket), así que asignarles un default automático (p. ej. "1.1.02.001 Bancos cuenta corriente")
/// sería inventar una decisión contable que corresponde al administrador de cada empresa — el
/// operador debe configurarlas explícitamente en Ventas > Métodos de pago después de este backfill.
/// Mismo criterio de "fail-closed en vez de default oculto" que el resto de este ticket.
///
/// Reglas de seguridad (mismo criterio que <see cref="PaymentMethodSriMappingBackfillService"/>):
/// - Nunca pisa una fila ya existente (INSERT únicamente si no hay PaymentMethodAccount para esa
///   (TenantId, CompanyId, PaymentMethodId)) — respeta cualquier configuración manual previa.
/// - Solo actúa si la company tiene la cuenta "1.1.01.001" activa y postable — si no la tiene
///   (plan de cuentas atípico), se omite esa company y se reporta, nunca se crea con una cuenta
///   inventada.
/// - Multi-tenant/multi-company seguro: itera vía IgnoreQueryFilters() porque corre fuera de
///   cualquier contexto de tenant autenticado (operación de despliegue).
///
/// Invocación: <c>dotnet run -- backfill-payment-method-account</c> (ver Program.cs). No expone
/// endpoint HTTP — operación de despliegue de una sola vez, re-ejecutable sin riesgo.
/// </summary>
public sealed class PaymentMethodAccountBackfillService
{
    private const string EfectivoCode = "EFECTIVO";
    private const string CajaGeneralAccountCode = "1.1.01.001";

    /// <summary>Actor de sistema para operaciones sin usuario autenticado (mismo criterio que MasterDataClassificationBackfillService).</summary>
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly ILogger<PaymentMethodAccountBackfillService> _logger;

    public PaymentMethodAccountBackfillService(
        ErpDbContext db,
        ILogger<PaymentMethodAccountBackfillService> logger
    )
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PaymentMethodAccountBackfillResult> RunAsync(CancellationToken ct = default)
    {
        var companies = await _db
            .Companies.IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(ct);

        var existingLinks = await _db
            .PaymentMethodAccounts.IgnoreQueryFilters()
            .Select(x => new { x.TenantId, x.CompanyId, x.PaymentMethodId })
            .ToListAsync(ct);
        var existingLinkKeys = existingLinks
            .Select(x => (x.TenantId, x.CompanyId, x.PaymentMethodId))
            .ToHashSet();

        var companiesProcessed = 0;
        var rowsCreated = 0;
        var skippedNoEfectivoMethod = 0;
        var skippedNoCajaGeneralAccount = 0;
        var skippedAlreadyConfigured = 0;

        foreach (var company in companies)
        {
            companiesProcessed++;

            var efectivo = await _db
                .PaymentMethods.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    pm => pm.TenantId == company.TenantId && pm.Code == EfectivoCode,
                    ct
                );
            if (efectivo is null)
            {
                skippedNoEfectivoMethod++;
                continue;
            }

            if (existingLinkKeys.Contains((company.TenantId, company.Id, efectivo.Id)))
            {
                skippedAlreadyConfigured++;
                continue;
            }

            var cajaGeneral = await _db
                .Accounts.IgnoreQueryFilters()
                .Where(a =>
                    a.TenantId == company.TenantId
                    && a.CompanyId == company.Id
                    && a.Code.Value == CajaGeneralAccountCode
                )
                .Select(a => new { a.Id, a.IsActive, a.AllowsPosting })
                .FirstOrDefaultAsync(ct);
            if (cajaGeneral is null || !cajaGeneral.IsActive || !cajaGeneral.AllowsPosting)
            {
                skippedNoCajaGeneralAccount++;
                _logger.LogWarning(
                    "Backfill PaymentMethodAccount: company {CompanyId} (tenant {TenantId}) no tiene "
                        + "una cuenta {AccountCode} activa/postable — se omite, requiere revisión manual.",
                    company.Id,
                    company.TenantId,
                    CajaGeneralAccountCode
                );
                continue;
            }

            var link = PaymentMethodAccount.Create(
                company.TenantId,
                company.Id,
                efectivo.Id,
                cajaGeneral.Id,
                SystemActor
            );
            _db.PaymentMethodAccounts.Add(link);
            rowsCreated++;
        }

        if (rowsCreated > 0)
            await _db.SaveChangesAsync(ct);

        var result = new PaymentMethodAccountBackfillResult(
            companiesProcessed,
            rowsCreated,
            skippedNoEfectivoMethod,
            skippedNoCajaGeneralAccount,
            skippedAlreadyConfigured
        );

        _logger.LogInformation(
            "Backfill PaymentMethodAccount: {CompaniesProcessed} company(s) procesadas, "
                + "{RowsCreated} fila(s) creadas (EFECTIVO -> Caja general), "
                + "{SkippedNoEfectivoMethod} sin método EFECTIVO, "
                + "{SkippedNoCajaGeneralAccount} sin cuenta Caja general activa/postable, "
                + "{SkippedAlreadyConfigured} ya configuradas previamente.",
            result.CompaniesProcessed,
            result.RowsCreated,
            result.SkippedNoEfectivoMethod,
            result.SkippedNoCajaGeneralAccount,
            result.SkippedAlreadyConfigured
        );

        return result;
    }
}

public sealed record PaymentMethodAccountBackfillResult(
    int CompaniesProcessed,
    int RowsCreated,
    int SkippedNoEfectivoMethod,
    int SkippedNoCajaGeneralAccount,
    int SkippedAlreadyConfigured
);
