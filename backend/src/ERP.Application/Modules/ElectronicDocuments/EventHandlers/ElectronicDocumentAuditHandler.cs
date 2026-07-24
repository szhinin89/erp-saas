using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="ElectronicDocument"/> a <see cref="ElectronicDocumentAudit"/>.
/// Único lugar que conoce ambos lados — el resto de la infraestructura de auditoría es
/// genérica y no conoce ElectronicDocuments. Registra únicamente metadatos del ciclo
/// electrónico, nunca información comercial (que no existe en estos eventos).
/// </summary>
public sealed class ElectronicDocumentAuditHandler :
    INotificationHandler<ElectronicDocumentCreatedEvent>,
    INotificationHandler<ElectronicDocumentXmlGeneratedEvent>,
    INotificationHandler<ElectronicDocumentSignedEvent>,
    INotificationHandler<ElectronicDocumentSentEvent>,
    INotificationHandler<ElectronicDocumentReceivedEvent>,
    INotificationHandler<ElectronicDocumentAuthorizedEvent>,
    INotificationHandler<ElectronicDocumentRejectedEvent>,
    INotificationHandler<ElectronicDocumentDeadLetterEvent>,
    INotificationHandler<ElectronicDocumentFailedEvent>,
    INotificationHandler<ElectronicDocumentCancelledEvent>,
    INotificationHandler<ElectronicDocumentRetryAttemptedEvent>,
    INotificationHandler<ElectronicDocumentReactivatedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public ElectronicDocumentAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(ElectronicDocumentCreatedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, fromState: null, toState: ElectronicDocumentState.Draft),
        ct);

    public Task Handle(ElectronicDocumentXmlGeneratedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);

    public Task Handle(ElectronicDocumentSignedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);

    public Task Handle(ElectronicDocumentSentEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);

    public Task Handle(ElectronicDocumentReceivedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);

    public Task Handle(ElectronicDocumentAuthorizedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);

    public Task Handle(ElectronicDocumentRejectedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState, ((IAuditEvent)e).Reason),
        ct);

    public Task Handle(ElectronicDocumentDeadLetterEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState, ((IAuditEvent)e).Reason),
        ct);

    public Task Handle(ElectronicDocumentFailedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState, ((IAuditEvent)e).Reason),
        ct);

    public Task Handle(ElectronicDocumentCancelledEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState, ((IAuditEvent)e).Reason),
        ct);

    public Task Handle(ElectronicDocumentRetryAttemptedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState, ((IAuditEvent)e).Reason),
        ct);

    public Task Handle(ElectronicDocumentReactivatedEvent e, CancellationToken ct) => _audit.RecordAsync(
        ElectronicDocumentAudit.Create(
            _context.Actor, _context.CompanyId, e.ElectronicDocumentId, ((IAuditEvent)e).Action,
            e.DocumentType, e.FromState, e.ToState),
        ct);
}
