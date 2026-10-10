using System.Data;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding;

public sealed partial class AccountingChartBackfillService
{
    /// <summary>
    /// IL-7A — comando de despliegue explícito, disponible en Production, mismo patrón que
    /// <see cref="RunSupplierCreditRefundRuleMaintenanceAsync"/>: dry-run por defecto; con
    /// <paramref name="apply"/> solo crea la cuenta puente "Saldos de apertura" y las reglas
    /// <c>InitialLoad/*</c> que falten. Nunca modifica cuentas ni reglas existentes ni genera asientos.
    /// Idempotente.
    /// </summary>
    public async Task<
        IReadOnlyList<OpeningBalancePostingSetupMaintenanceResult>
    > RunOpeningBalancePostingSetupMaintenanceAsync(
        bool apply = false,
        CancellationToken cancellationToken = default
    )
    {
        var companies = await _db
            .Companies.AsPlatformQuery()
            .AsNoTracking()
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(cancellationToken);
        var results = new List<OpeningBalancePostingSetupMaintenanceResult>();
        // Actor de sistema, mismo criterio que EnsureAsync (fuera de cualquier request/usuario).
        var systemActorId = Guid.NewGuid();
        foreach (var company in companies)
        {
            using var context = JobExecutionContext.Begin(company.TenantId, company.Id);
            // Serializable protects code-availability checks from concurrent chart edits.
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken
                )
                : null;
            var diagnostics = await _accountingBootstrapStep.MaintainOpeningBalancePostingSetupAsync(
                company.TenantId,
                company.Id,
                systemActorId,
                apply,
                cancellationToken
            );
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            results.AddRange(
                diagnostics.Select(d => new OpeningBalancePostingSetupMaintenanceResult(
                    company.TenantId,
                    company.Id,
                    d.Item,
                    d.Diagnostic
                ))
            );
        }
        return results;
    }
}

public sealed record OpeningBalancePostingSetupMaintenanceResult(
    Guid TenantId,
    Guid CompanyId,
    string Item,
    string Diagnostic
);
