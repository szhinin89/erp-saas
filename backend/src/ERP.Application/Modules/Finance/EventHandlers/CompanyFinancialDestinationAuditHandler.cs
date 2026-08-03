using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Events;
using MediatR;

namespace ERP.Application.Modules.Finance.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="CompanyFinancialDestination"/> a
/// <see cref="CompanyFinancialDestinationAudit"/> (diseño P0-02 §20.1, Fase 4). Único lugar que
/// conoce ambos lados (evento de dominio ↔ entidad de auditoría) — el resto de la infraestructura
/// de auditoría es genérica y no conoce este catálogo.
/// </summary>
public sealed class CompanyFinancialDestinationAuditHandler
    : INotificationHandler<CompanyFinancialDestinationCreatedEvent>,
        INotificationHandler<CompanyFinancialDestinationRenamedEvent>,
        INotificationHandler<CompanyFinancialDestinationActiveChangedEvent>,
        INotificationHandler<CompanyFinancialDestinationAccountChangedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public CompanyFinancialDestinationAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(CompanyFinancialDestinationCreatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            CompanyFinancialDestinationAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.DestinationId,
                ((IAuditEvent)e).Action,
                code: e.Code,
                newName: e.Name,
                newIsActive: e.IsActive,
                newAccountingAccountId: e.AccountingAccountId
            ),
            ct
        );

    public Task Handle(CompanyFinancialDestinationRenamedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            CompanyFinancialDestinationAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.DestinationId,
                ((IAuditEvent)e).Action,
                code: e.Code,
                oldName: e.OldName,
                newName: e.NewName
            ),
            ct
        );

    public Task Handle(CompanyFinancialDestinationActiveChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            CompanyFinancialDestinationAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.DestinationId,
                ((IAuditEvent)e).Action,
                code: e.Code,
                oldIsActive: e.OldIsActive,
                newIsActive: e.NewIsActive
            ),
            ct
        );

    public Task Handle(CompanyFinancialDestinationAccountChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            CompanyFinancialDestinationAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.DestinationId,
                ((IAuditEvent)e).Action,
                code: e.Code,
                oldAccountingAccountId: e.OldAccountingAccountId,
                newAccountingAccountId: e.NewAccountingAccountId
            ),
            ct
        );
}
