using ERP.Application.Audit;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Events;
using MediatR;

namespace ERP.Application.Modules.Employees.EventHandlers;

/// <summary>
/// Traduce <see cref="EmployeeCatalogChangedEvent"/> (cargo, profesión, especialidad) a la
/// auditoría común <see cref="EmployeeCatalogAudit"/> (EMP-T-07).
/// </summary>
public sealed class EmployeeCatalogAuditHandler
    : INotificationHandler<EmployeeCatalogChangedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public EmployeeCatalogAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(EmployeeCatalogChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            EmployeeCatalogAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.CatalogType,
                e.CatalogEntryId,
                e.Action,
                e.OldName,
                e.NewName,
                e.IsActive
            ),
            ct
        );
}
