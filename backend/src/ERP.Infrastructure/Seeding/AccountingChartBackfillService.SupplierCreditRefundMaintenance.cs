using System.Data;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding;

public sealed partial class AccountingChartBackfillService
{
    /// <summary>
    /// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — comando de despliegue explícito, disponible en
    /// Production, mismo patrón exacto que <see cref="RunSupplierPaymentRuleMaintenanceAsync"/>:
    /// dry-run por defecto; con <paramref name="apply"/> solo crea las reglas canónicas
    /// "Purchases"/"SupplierCreditRefunded"/"SupplierCreditRefundReversed" que falten por completo.
    /// Nunca modifica reglas existentes ni invoca el bootstrap general. Idempotente.
    /// </summary>
    public async Task<IReadOnlyList<SupplierCreditRefundRuleMaintenanceResult>> RunSupplierCreditRefundRuleMaintenanceAsync(
        bool apply = false, CancellationToken cancellationToken = default)
    {
        var companies = await _db.Companies.AsPlatformQuery().AsNoTracking()
            .Select(c => new { c.Id, c.TenantId }).ToListAsync(cancellationToken);
        var results = new List<SupplierCreditRefundRuleMaintenanceResult>();
        // Actor de sistema, mismo criterio que EnsureAsync (fuera de cualquier request/usuario).
        var systemActorId = Guid.NewGuid();
        foreach (var company in companies)
        {
            using var context = JobExecutionContext.Begin(company.TenantId, company.Id);
            // Serializable protects recognition and account validation from concurrent edits.
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var diagnostics = await _accountingBootstrapStep.MaintainSupplierCreditRefundRulesAsync(
                company.TenantId, company.Id, systemActorId, apply, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            results.AddRange(diagnostics.Select(d =>
                new SupplierCreditRefundRuleMaintenanceResult(company.TenantId, company.Id, d.FactType, d.Diagnostic)));
        }
        return results;
    }
}

public sealed record SupplierCreditRefundRuleMaintenanceResult(
    Guid TenantId,
    Guid CompanyId,
    string FactType,
    string Diagnostic
);
