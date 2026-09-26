using System.Data;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding;

public sealed partial class AccountingChartBackfillService
{
    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C-PROD-CLOSE (ADR-035) — comando de despliegue
    /// explícito, disponible en Production, mismo patrón exacto que
    /// <see cref="RunSalesInvoiceRuleMaintenanceAsync"/>: dry-run por defecto; con
    /// <paramref name="apply"/> solo actualiza reglas "Payables"/"SupplierPayment*" con la forma
    /// canónica anterior exacta. Nunca invoca el bootstrap general. Idempotente.
    /// </summary>
    public async Task<IReadOnlyList<SupplierPaymentRuleMaintenanceResult>> RunSupplierPaymentRuleMaintenanceAsync(
        bool apply = false, CancellationToken cancellationToken = default)
    {
        var companies = await _db.Companies.AsPlatformQuery().AsNoTracking()
            .Select(c => new { c.Id, c.TenantId }).ToListAsync(cancellationToken);
        var results = new List<SupplierPaymentRuleMaintenanceResult>();
        foreach (var company in companies)
        {
            using var context = JobExecutionContext.Begin(company.TenantId, company.Id);
            // Serializable protects recognition and account validation from concurrent edits.
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var diagnostics = await _accountingBootstrapStep.MaintainSupplierPaymentRulesAsync(
                company.TenantId, company.Id, apply, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            results.AddRange(diagnostics.Select(d =>
                new SupplierPaymentRuleMaintenanceResult(company.TenantId, company.Id, d.FactType, d.Diagnostic)));
        }
        return results;
    }
}

public sealed record SupplierPaymentRuleMaintenanceResult(
    Guid TenantId,
    Guid CompanyId,
    string FactType,
    string Diagnostic
);
