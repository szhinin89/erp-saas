using ERP.Application.Audit;
using ERP.Application.Modules.Sales.EventHandlers;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Verifica que <see cref="SalesReturnAuditHandler"/> traduce cada domain event de
/// <see cref="SalesReturn"/> (Created/Authorized/Cancelled) a exactamente un registro de
/// <see cref="SalesReturnAudit"/>, con los datos del documento correcto — mismo patrón de test
/// que los demás *AuditHandler del ERP (Pricing, PurchaseInvoice, ElectronicDocuments).
/// </summary>
public sealed class SalesReturnAuditHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SalesReturnId = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Mock<IAuditContext> Context()
    {
        var mock = new Mock<IAuditContext>();
        mock.SetupGet(c => c.TenantId).Returns(TenantId);
        mock.SetupGet(c => c.CompanyId).Returns(CompanyId);
        mock.SetupGet(c => c.Actor)
            .Returns(
                new AuditActor(
                    TenantId,
                    UserId,
                    "tester",
                    null,
                    null,
                    null,
                    null,
                    null,
                    AuditSource.UserAction
                )
            );
        return mock;
    }

    [Fact]
    public async Task Handle_DraftCreatedEvent_registra_un_solo_audit_con_los_datos_del_borrador()
    {
        var audit = new Mock<IAuditService>();
        var handler = new SalesReturnAuditHandler(audit.Object, Context().Object);

        var evt = new SalesReturnDraftCreatedEvent(
            SalesReturnId,
            SalesInvoiceId,
            CustomerId,
            "DEV-000001",
            "Producto en mal estado",
            TenantId,
            CompanyId,
            UserId
        );

        await handler.Handle(evt, CancellationToken.None);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<SalesReturnAudit>(r =>
                        r.EntityId == SalesReturnId
                        && r.SalesInvoiceId == SalesInvoiceId
                        && r.CustomerId == CustomerId
                        && r.ReturnNumber == "DEV-000001"
                        && r.CompanyId == CompanyId
                        && r.Action == "Created"
                        && r.Reason == "Producto en mal estado"
                        && r.GrandTotal == null
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.Verify(
            a => a.RecordAsync(It.IsAny<SalesReturnAudit>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_AuthorizedEvent_registra_un_solo_audit_con_el_total_congelado()
    {
        var audit = new Mock<IAuditService>();
        var handler = new SalesReturnAuditHandler(audit.Object, Context().Object);

        var evt = new SalesReturnAuthorizedEvent(
            SalesReturnId,
            SalesInvoiceId,
            "DEV-000001",
            23m,
            UserId,
            TenantId,
            CompanyId,
            20m,
            3m,
            0m,
            0m,
            "Producto en mal estado"
        );

        await handler.Handle(evt, CancellationToken.None);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<SalesReturnAudit>(r =>
                        r.EntityId == SalesReturnId
                        && r.SalesInvoiceId == SalesInvoiceId
                        && r.ReturnNumber == "DEV-000001"
                        && r.CompanyId == CompanyId
                        && r.Action == "Authorized"
                        && r.GrandTotal == 23m
                        && r.Reason == "Producto en mal estado"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.Verify(
            a => a.RecordAsync(It.IsAny<SalesReturnAudit>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_CancelledEvent_registra_un_solo_audit_sin_total()
    {
        var audit = new Mock<IAuditService>();
        var handler = new SalesReturnAuditHandler(audit.Object, Context().Object);

        var evt = new SalesReturnCancelledEvent(
            SalesReturnId,
            SalesInvoiceId,
            CustomerId,
            "DEV-000001",
            "Producto en mal estado",
            TenantId,
            CompanyId,
            UserId
        );

        await handler.Handle(evt, CancellationToken.None);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<SalesReturnAudit>(r =>
                        r.EntityId == SalesReturnId
                        && r.SalesInvoiceId == SalesInvoiceId
                        && r.CustomerId == CustomerId
                        && r.ReturnNumber == "DEV-000001"
                        && r.CompanyId == CompanyId
                        && r.Action == "Cancelled"
                        && r.GrandTotal == null
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.Verify(
            a => a.RecordAsync(It.IsAny<SalesReturnAudit>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
