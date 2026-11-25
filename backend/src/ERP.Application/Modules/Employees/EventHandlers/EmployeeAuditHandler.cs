using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Events;
using MediatR;

namespace ERP.Application.Modules.Employees.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="Employee"/> a <see cref="EmployeeAudit"/> (EMP-BR-20).
/// Único lugar que conoce ambos lados; la infraestructura de auditoría es genérica.
/// </summary>
public sealed class EmployeeAuditHandler
    : INotificationHandler<EmployeeCreatedEvent>,
      INotificationHandler<EmployeeFieldChangedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public EmployeeAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(EmployeeCreatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            EmployeeAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.EmployeeId,
                e.BusinessPartnerId,
                ((IAuditEvent)e).Action,
                changedField: "StartDate",
                newValue: e.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            ),
            ct
        );

    public Task Handle(EmployeeFieldChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            EmployeeAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.EmployeeId,
                e.BusinessPartnerId,
                ((IAuditEvent)e).Action,
                changedField: e.ChangedField,
                oldValue: e.OldValue,
                newValue: e.NewValue
            ),
            ct
        );
}
