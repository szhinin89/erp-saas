using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Communications.EventHandlers;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — ÚNICO handler "comprobante autorizado → comunicación" (ADR-038/ADR-039
/// fase 5). Reemplaza al handler específico de factura: no conoce tipos de documento; delega en
/// <see cref="IElectronicDocumentCommunicationService"/>, que enruta al contributor del módulo dueño.
/// <para>
/// Corre dentro de la transacción que autorizó el comprobante (publicación in-process antes del
/// commit): solo inserta, de forma idempotente, la fila de outbox — sin RIDE, sin lectura de archivos,
/// sin SMTP. Cualquier fallo se absorbe y registra: la autorización SRI ya ocurrió y no se revierte por
/// el correo; la reconciliación recupera lo que no se haya encolado.
/// </para>
/// </summary>
public sealed partial class ElectronicDocumentAuthorizedCommunicationHandler
    : INotificationHandler<ElectronicDocumentAuthorizedEvent>
{
    private readonly IElectronicDocumentRepository _electronicDocuments;
    private readonly IElectronicDocumentCommunicationService _communications;
    private readonly ILogger<ElectronicDocumentAuthorizedCommunicationHandler> _logger;

    public ElectronicDocumentAuthorizedCommunicationHandler(
        IElectronicDocumentRepository electronicDocuments,
        IElectronicDocumentCommunicationService communications,
        ILogger<ElectronicDocumentAuthorizedCommunicationHandler> logger
    )
    {
        _electronicDocuments = electronicDocuments;
        _communications = communications;
        _logger = logger;
    }

    public async Task Handle(ElectronicDocumentAuthorizedEvent notification, CancellationToken ct)
    {
        try
        {
            if (notification.TenantId is not { } tenantId)
                return;

            var document = await _electronicDocuments.GetByIdAsync(tenantId, notification.ElectronicDocumentId, ct);
            if (document is null)
            {
                LogDocumentMissing(notification.ElectronicDocumentId);
                return;
            }

            await _communications.RequestAsync(document, ElectronicDocumentCommunicationTrigger.AuthorizedEvent, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnexpectedFailure(notification.ElectronicDocumentId, ex);
        }
    }

    [LoggerMessage(EventId = 4233, EventName = "ElectronicDocumentCommunicationSourceMissing", Level = LogLevel.Warning,
        Message = "Communications: authorized ElectronicDocument {ElectronicDocumentId} not found; no communication requested")]
    private partial void LogDocumentMissing(Guid electronicDocumentId);

    [LoggerMessage(EventId = 4234, EventName = "ElectronicDocumentCommunicationFailed", Level = LogLevel.Warning,
        Message = "Communications: could not request communication for authorized ElectronicDocument {ElectronicDocumentId}; SRI authorization is kept and reconciliation will retry")]
    private partial void LogUnexpectedFailure(Guid electronicDocumentId, Exception ex);
}
