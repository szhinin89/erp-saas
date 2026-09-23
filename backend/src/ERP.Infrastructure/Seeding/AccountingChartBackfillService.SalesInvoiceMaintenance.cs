using System.Data;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding;

public sealed partial class AccountingChartBackfillService
{
    // Explicit deployment command, available in Production. Never invokes the general bootstrap.
    public async Task<IReadOnlyList<SalesInvoiceRuleMaintenanceResult>> RunSalesInvoiceRuleMaintenanceAsync(
        bool apply = false, CancellationToken cancellationToken = default)
    {
        var companies = await _db.Companies.IgnoreQueryFilters().AsNoTracking()
            .Select(c => new { c.Id, c.TenantId }).ToListAsync(cancellationToken);
        var results = new List<SalesInvoiceRuleMaintenanceResult>();
        foreach (var company in companies)
        {
            using var context = JobExecutionContext.Begin(company.TenantId, company.Id);
            // Serializable protects recognition and account validation from concurrent edits.
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var diagnostic = await _accountingBootstrapStep.MaintainSalesInvoiceRuleAsync(
                company.TenantId, company.Id, apply, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            results.Add(new(company.TenantId, company.Id, diagnostic));
        }
        return results;
    }
}

public sealed record SalesInvoiceRuleMaintenanceResult(Guid TenantId, Guid CompanyId, string Diagnostic);
