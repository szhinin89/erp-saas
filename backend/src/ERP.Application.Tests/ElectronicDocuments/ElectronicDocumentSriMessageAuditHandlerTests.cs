using ERP.Application.Audit;
using ERP.Application.Modules.ElectronicDocuments.EventHandlers;
using ERP.Domain.Audit;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// Verifica que <see cref="ElectronicDocumentSriMessageAuditHandler"/> persiste cada mensaje SRI
/// individualmente (uno por <see cref="SriMessage"/>) sin resumir ni fusionar, y que no escribe
/// nada cuando el rechazo no trajo mensajes estructurados (rechazo sin respuesta SRI real de por
/// medio — ver ADR-024).
/// </summary>
public sealed class ElectronicDocumentSriMessageAuditHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static Mock<IAuditContext> Context()
    {
        var mock = new Mock<IAuditContext>();
        mock.SetupGet(c => c.TenantId).Returns(TenantId);
        mock.SetupGet(c => c.CompanyId).Returns(CompanyId);
        mock.SetupGet(c => c.Actor).Returns(new AuditActor(
            TenantId, Guid.NewGuid(), "tester", null, null, null, null, null, AuditSource.UserAction));
        return mock;
    }

    [Fact]
    public async Task Handle_with_structured_messages_records_one_audit_row_per_message()
    {
        var audit = new Mock<IAuditService>();
        var handler = new ElectronicDocumentSriMessageAuditHandler(audit.Object, Context().Object);

        var messages = new[]
        {
            new SriMessage("39", "ERROR", "FIRMA INVALIDA", "La firma es inválida."),
            new SriMessage("70", "ADVERTENCIA", "CLAVE ACCESO EN PROCESAMIENTO", null),
        };
        var evt = new ElectronicDocumentRejectedEvent(
            TenantId, DocumentId, ElectronicDocumentType.Invoice,
            ElectronicDocumentState.Sent, ElectronicDocumentState.Rejected,
            "[39] FIRMA INVALIDA: La firma es inválida.", messages);

        await handler.Handle(evt, CancellationToken.None);

        audit.Verify(a => a.RecordAsync(
            It.Is<ElectronicDocumentSriMessage>(m => m.Code == "39" && m.MessageType == "ERROR" && m.Message == "FIRMA INVALIDA"),
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RecordAsync(
            It.Is<ElectronicDocumentSriMessage>(m => m.Code == "70" && m.MessageType == "ADVERTENCIA"),
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RecordAsync(
            It.IsAny<ElectronicDocumentSriMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_without_structured_messages_records_nothing()
    {
        var audit = new Mock<IAuditService>();
        var handler = new ElectronicDocumentSriMessageAuditHandler(audit.Object, Context().Object);

        var evt = new ElectronicDocumentRejectedEvent(
            TenantId, DocumentId, ElectronicDocumentType.Invoice,
            ElectronicDocumentState.Sent, ElectronicDocumentState.Rejected,
            "El SRI devolvió el comprobante con estado 'ERROR_SOAP_FAULT'.", sriMessages: null);

        await handler.Handle(evt, CancellationToken.None);

        audit.Verify(a => a.RecordAsync(
            It.IsAny<ElectronicDocumentSriMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
