using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Expenses.Events;

/// <summary>Se levanta cuando <c>ExpenseDocument.Cancel()</c> anula un gasto confirmado.</summary>
public sealed class ExpenseDocumentCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ExpenseDocumentId { get; }
    public Guid SupplierId { get; }
    public string DocumentNumber { get; }
    public Guid CompanyId { get; }
    public string CancelReason { get; }

    public ExpenseDocumentCancelledEvent(
        Guid tenantId,
        Guid expenseDocumentId,
        Guid supplierId,
        string documentNumber,
        Guid companyId,
        string cancelReason
    )
    {
        TenantId = tenantId;
        ExpenseDocumentId = expenseDocumentId;
        SupplierId = supplierId;
        DocumentNumber = documentNumber;
        CompanyId = companyId;
        CancelReason = cancelReason;
    }

    Guid IAuditEvent.EntityId => ExpenseDocumentId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => CancelReason;
}
