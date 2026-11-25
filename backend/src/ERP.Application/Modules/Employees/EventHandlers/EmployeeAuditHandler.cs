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
      INotificationHandler<EmployeeFieldChangedEvent>,
      INotificationHandler<EmployeeTerminatedEvent>,
      INotificationHandler<EmployeeRehiredEvent>,
      INotificationHandler<EmployeeBranchAssignmentChangedEvent>
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

    public Task Handle(EmployeeTerminatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            EmployeeAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.EmployeeId,
                e.BusinessPartnerId,
                ((IAuditEvent)e).Action,
                changedField: "EndDate",
                newValue: e.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                reason: e.Reason
            ),
            ct
        );

    public Task Handle(EmployeeRehiredEvent e, CancellationToken ct) =>
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

    public Task Handle(EmployeeBranchAssignmentChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            EmployeeAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.EmployeeId,
                e.BusinessPartnerId,
                e.Action,
                changedField: "BranchAssignment",
                oldValue: e.Action == EmployeeBranchAssignmentChangedEvent.ClosedAction ? Describe(e, e.OldEndDate) : null,
                newValue: Describe(e, e.NewEndDate)
            ),
            ct
        );

    // «sucursal inicio/fin» (fechas yyyy-MM-dd; fin vacío = abierta). Sin datos personales.
    private static string Describe(EmployeeBranchAssignmentChangedEvent e, DateOnly? end)
    {
        var f = System.Globalization.CultureInfo.InvariantCulture;
        return $"{e.BranchId} {e.StartDate.ToString("yyyy-MM-dd", f)}/{end?.ToString("yyyy-MM-dd", f)}";
    }
}
