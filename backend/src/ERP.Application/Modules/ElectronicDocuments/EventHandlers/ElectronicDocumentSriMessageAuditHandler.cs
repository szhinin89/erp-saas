using ERP.Application.Audit;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.EventHandlers;

/// <summary>
/// Persiste, uno por uno, los mensajes SRI reales que trae <see cref="ElectronicDocumentRejectedEvent.SriMessages"/>
/// — coexiste con <see cref="ElectronicDocumentAuditHandler"/> (que audita la transición de
/// estado) sin reemplazarlo; MediatR ya soporta múltiples handlers por evento. Si el evento no
/// trae mensajes estructurados (rechazo sin respuesta SRI real de por medio), no escribe nada —
/// <c>ElectronicDocument.LastError</c> sigue siendo la única fuente de verdad en ese caso.
/// </summary>
public sealed class ElectronicDocumentSriMessageAuditHandler
    : INotificationHandler<ElectronicDocumentRejectedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public ElectronicDocumentSriMessageAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public async Task Handle(ElectronicDocumentRejectedEvent e, CancellationToken ct)
    {
        if (e.SriMessages is null or { Count: 0 })
            return;

        foreach (var message in e.SriMessages)
        {
            await _audit.RecordAsync(
                ElectronicDocumentSriMessage.Create(
                    _context.Actor,
                    _context.CompanyId,
                    e.ElectronicDocumentId,
                    message
                ),
                ct
            );
        }
    }
}
