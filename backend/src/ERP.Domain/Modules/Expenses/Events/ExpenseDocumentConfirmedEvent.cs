using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Expenses.Events;

/// <summary>
/// EXPENSES-CONFIRM-07 — allocation por línea ya resuelta por <c>ExpenseDocument.Confirm()</c>:
/// cuenta contable snapshot (recongelada al confirmar) + base imponible de esa línea. Traducido a
/// <c>PostingAllocation</c> (Application) por <c>ExpenseDocumentConfirmedPostingTranslator</c> —
/// Domain no conoce ese tipo (no depende de Application).
/// </summary>
public sealed record ExpenseDocumentConfirmedLineAllocation(
    Guid ExpenseLineId,
    Guid AccountingAccountId,
    decimal Amount,
    string? Description
);

/// <summary>Se levanta cuando <c>ExpenseDocument.Confirm()</c> pasa el gasto de Draft a Confirmed.</summary>
public sealed class ExpenseDocumentConfirmedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ExpenseDocumentId { get; }
    public Guid SupplierId { get; }
    public string DocumentNumber { get; }
    public Guid CompanyId { get; }
    public DateOnly AccountingDate { get; }

    /// <summary>
    /// Montos ya resueltos por Expenses (líneas + IVA por línea) — ADR-026 §4. Accounting los
    /// consume tal cual, nunca los recalcula.
    /// </summary>
    public decimal TotalVat { get; }
    public decimal GrandTotal { get; }

    /// <summary>
    /// Una allocation por línea de gasto (EXPENSES-POSTING-ALLOCATIONS-06) — cardinalidad variable,
    /// a diferencia de los montos únicos de arriba. Cada una debita la cuenta contable snapshot de
    /// su línea, nunca una cuenta fija de PostingRule.
    /// </summary>
    public IReadOnlyList<ExpenseDocumentConfirmedLineAllocation> LineAllocations { get; }

    public ExpenseDocumentConfirmedEvent(
        Guid tenantId,
        Guid expenseDocumentId,
        Guid supplierId,
        string documentNumber,
        Guid companyId,
        DateOnly accountingDate,
        decimal totalVat,
        decimal grandTotal,
        IReadOnlyList<ExpenseDocumentConfirmedLineAllocation> lineAllocations
    )
    {
        TenantId = tenantId;
        ExpenseDocumentId = expenseDocumentId;
        SupplierId = supplierId;
        DocumentNumber = documentNumber;
        CompanyId = companyId;
        AccountingDate = accountingDate;
        TotalVat = totalVat;
        GrandTotal = grandTotal;
        LineAllocations = lineAllocations;
    }

    Guid IAuditEvent.EntityId => ExpenseDocumentId;
    string IAuditEvent.Action => "Confirmed";
    string? IAuditEvent.Reason => null;
}
