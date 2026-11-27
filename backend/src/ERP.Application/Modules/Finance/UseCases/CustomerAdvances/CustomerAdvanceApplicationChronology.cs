using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// ADR-052 §17 — la fecha efectiva de una aplicación no puede ser anterior a la fecha del anticipo ni
/// a la fecha de origen contable de ninguna CxC (factura: <c>SalesInvoice.IssueDate</c>; saldo
/// inicial: <c>Company.OpeningBalanceDate</c>) ni posterior al día de negocio de la empresa. Una
/// fecha de origen que no se puede determinar rechaza la operación.
/// </summary>
internal sealed class CustomerAdvanceApplicationChronology
{
    private readonly ISalesInvoiceRepository _invoices;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly ICompanyClock _clock;

    public CustomerAdvanceApplicationChronology(
        ISalesInvoiceRepository invoices,
        IOpeningBalanceConstraintsReader openingBalance,
        ICompanyClock clock
    )
    {
        _invoices = invoices;
        _openingBalance = openingBalance;
        _clock = clock;
    }

    public async Task EnsureAsync(
        CustomerAdvance advance,
        IReadOnlyCollection<SalesReceivable> receivables,
        DateOnly effectiveDate,
        CancellationToken ct
    )
    {
        var today = await _clock.TodayAsync(advance.CompanyId, advance.TenantId, ct);
        if (effectiveDate > today)
            throw Rejected(
                $"La fecha de aplicación {effectiveDate:yyyy-MM-dd} es posterior al día de negocio {today:yyyy-MM-dd}."
            );
        if (effectiveDate < advance.AdvanceDate)
            throw Rejected(
                $"La fecha de aplicación {effectiveDate:yyyy-MM-dd} es anterior a la fecha del anticipo {advance.AdvanceDate:yyyy-MM-dd}."
            );

        var invoices = await _invoices.GetReceivableSummariesByIdsAsync(
            advance.TenantId,
            receivables
                .Where(r => r.Origin == SalesReceivableOrigin.Invoice && r.InvoiceId is not null)
                .Select(r => r.InvoiceId!.Value)
                .Distinct()
                .ToList(),
            ct
        );
        var openingDate = receivables.Any(r => r.Origin == SalesReceivableOrigin.InitialBalance)
            ? await _openingBalance.GetOpeningBalanceDateAsync(ct)
            : null;

        foreach (var receivable in receivables)
        {
            DateOnly? origin = receivable.Origin switch
            {
                SalesReceivableOrigin.InitialBalance => openingDate,
                SalesReceivableOrigin.Invoice
                    when receivable.InvoiceId is { } invoiceId
                        && invoices.TryGetValue(invoiceId, out var invoice) => invoice.IssueDate,
                _ => null,
            };
            if (origin is not { } originDate)
                throw Rejected(
                    $"No se puede determinar la fecha de origen de la cuenta por cobrar {receivable.Id}."
                );
            if (effectiveDate < originDate)
                throw Rejected(
                    $"La fecha de aplicación {effectiveDate:yyyy-MM-dd} es anterior a la fecha de origen {originDate:yyyy-MM-dd} de la cuenta por cobrar {receivable.Id}."
                );
        }
    }

    /// <summary>
    /// ADR-052 §17 — la fecha de la evidencia de una reversa va de la operación que compensa al día de
    /// negocio. Es trazabilidad: la fecha contable sigue siendo la de la reversa (TA-07).
    /// </summary>
    public static void EnsureEvidenceDate(
        DateOnly evidenceDate,
        DateOnly originalDate,
        DateOnly today
    )
    {
        if (evidenceDate > today)
            throw Rejected(
                $"La fecha de la evidencia {evidenceDate:yyyy-MM-dd} es posterior al día de negocio {today:yyyy-MM-dd}."
            );
        if (evidenceDate < originalDate)
            throw Rejected(
                $"La fecha de la evidencia {evidenceDate:yyyy-MM-dd} es anterior a la operación original del {originalDate:yyyy-MM-dd}."
            );
    }

    private static DomainRuleViolationException Rejected(string message) => new(message);
}
