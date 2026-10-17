using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — política operativa de Accounting aplicada a la apertura: la decisión se
/// toma sobre <c>Company.OpeningBalanceDate</c>. Si esa fecha no está sujeta (Disabled) no se publican
/// asientos de lote ni ASI y el cierre de la carga inicial omite los requisitos contables. El modelo
/// propio de apertura (OpeningBalancePosting/ASI) se conserva cuando está sujeta (accounting.md §3/§10).
/// </summary>
public sealed class OpeningAccountingGate(
    IAccountingOperationalPolicyRepository policies,
    AccountingOperationalDecision decision,
    IOpeningBalanceConstraintsReader openingBalance,
    IOperationalContext ctx)
{
    public const string NotApplicableCode = "OPENING_ACCOUNTING_NOT_APPLICABLE";

    public const string NotApplicableMessage =
        "Contabilidad no aplica en la fecha de apertura de la empresa (modo Deshabilitado): "
        + "la apertura no genera asientos.";

    /// <summary>Lock compartido de política: serializa con cierre de período y cambios de modo.</summary>
    public Task AcquireSharedLockAsync(CancellationToken ct) => policies.AcquireSharedLockAsync(ctx.CompanyId, ct);

    /// <summary>Aplicabilidad en la fecha de apertura, o null si la fecha aún no está definida.</summary>
    public async Task<AccountingApplicability?> ResolveAsync(CancellationToken ct)
    {
        var date = await openingBalance.GetOpeningBalanceDateAsync(ct);
        return date is { } openingDate
            ? await decision.ResolveAsync(ctx.TenantId, ctx.CompanyId, openingDate, ct)
            : null;
    }

    /// <summary>Motivo para no publicar un asiento de apertura, o null si puede intentarse.</summary>
    public async Task<(string Code, string Message)?> CheckPostableAsync(CancellationToken ct) =>
        await ResolveAsync(ct) switch
        {
            AccountingApplicability.NotConfigured => (
                AccountingOperationalDecision.NotConfiguredCode,
                AccountingOperationalDecision.NotConfiguredMessage
            ),
            AccountingApplicability.NotApplicable => (NotApplicableCode, NotApplicableMessage),
            _ => null,
        };
}
