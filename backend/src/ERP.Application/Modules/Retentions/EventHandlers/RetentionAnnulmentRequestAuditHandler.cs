using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Retentions.EventHandlers;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — traduce cada paso de una solicitud de anulación ante el SRI a
/// <see cref="RetentionAnnulmentRequestAudit"/> (ADR-022): quién/cuándo solicitó, presentó, resolvió
/// (con la evidencia), desistió, y la finalización del origen o su fallo.
/// </summary>
public sealed class RetentionAnnulmentRequestAuditHandler
    : INotificationHandler<RetentionAnnulmentRequestTransitionedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public RetentionAnnulmentRequestAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(RetentionAnnulmentRequestTransitionedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            RetentionAnnulmentRequestAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.RequestId,
                e.RetentionDocumentId,
                ((IAuditEvent)e).Action,
                e.FromStatus,
                e.ToStatus,
                ((IAuditEvent)e).Reason
            ),
            ct
        );
}
