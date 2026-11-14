using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Events;
using MediatR;

namespace ERP.Application.MasterData.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="SupplierExpenseSubcategory"/> a
/// <see cref="SupplierExpenseSubcategoryAudit"/> (ADR-022). Mismo patrón que
/// <c>PriceListItemAuditHandler</c> — único lugar que conoce ambos lados.
/// </summary>
public sealed class SupplierExpenseSubcategoryAuditHandler
    : INotificationHandler<SupplierExpenseSubcategoryAssignedEvent>,
        INotificationHandler<SupplierExpenseSubcategoryEnabledEvent>,
        INotificationHandler<SupplierExpenseSubcategoryDisabledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public SupplierExpenseSubcategoryAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(SupplierExpenseSubcategoryAssignedEvent e, CancellationToken ct) =>
        Record(e.AssignmentId, e.BusinessPartnerId, e.ExpenseSubcategoryId, (IAuditEvent)e, ct);

    public Task Handle(SupplierExpenseSubcategoryEnabledEvent e, CancellationToken ct) =>
        Record(e.AssignmentId, e.BusinessPartnerId, e.ExpenseSubcategoryId, (IAuditEvent)e, ct);

    public Task Handle(SupplierExpenseSubcategoryDisabledEvent e, CancellationToken ct) =>
        Record(e.AssignmentId, e.BusinessPartnerId, e.ExpenseSubcategoryId, (IAuditEvent)e, ct);

    private Task Record(
        Guid assignmentId,
        Guid businessPartnerId,
        Guid expenseSubcategoryId,
        IAuditEvent e,
        CancellationToken ct
    ) =>
        _audit.RecordAsync(
            SupplierExpenseSubcategoryAudit.Create(
                _context.Actor,
                _context.CompanyId,
                assignmentId,
                businessPartnerId,
                expenseSubcategoryId,
                e.Action,
                e.Reason
            ),
            ct
        );
}
