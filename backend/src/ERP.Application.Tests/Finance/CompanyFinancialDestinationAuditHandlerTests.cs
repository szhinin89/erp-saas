using ERP.Application.Audit;
using ERP.Application.Modules.Finance.EventHandlers;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// Verifica que <see cref="CompanyFinancialDestinationAuditHandler"/> traduce cada domain event de
/// <see cref="CompanyFinancialDestination"/> (Created/Renamed/ActiveChanged/AccountChanged) a
/// exactamente un registro de <see cref="CompanyFinancialDestinationAudit"/>, con los pares
/// Old/New correctos y los no aplicables en null — mismo patrón de test que los demás
/// *AuditHandler del ERP (Pricing, SalesReturn, ElectronicDocuments). Ejecuta el handler real,
/// nunca solo la construcción del evento.
/// </summary>
public sealed class CompanyFinancialDestinationAuditHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
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
    public async Task Handle_CreatedEvent_registra_exactamente_una_auditoria_con_los_valores_normativos()
    {
        var audit = new Mock<IAuditService>();
        var handler = new CompanyFinancialDestinationAuditHandler(audit.Object, Context().Object);
        using var cts = new CancellationTokenSource();

        var evt = new CompanyFinancialDestinationCreatedEvent(
            TenantId,
            DestinationId,
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            AccountingAccountId,
            true
        );

        await handler.Handle(evt, cts.Token);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<CompanyFinancialDestinationAudit>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.EntityId == DestinationId
                        && r.UserId == UserId
                        && r.Code == "BANCO-001"
                        && r.Action == "Created"
                        && r.NewName == "Cuenta corriente Pichincha"
                        && r.NewIsActive == true
                        && r.NewAccountingAccountId == AccountingAccountId
                        && r.OldName == null
                        && r.OldIsActive == null
                        && r.OldAccountingAccountId == null
                    ),
                    cts.Token
                ),
            Times.Once
        );
        audit.Verify(
            a =>
                a.RecordAsync(
                    It.IsAny<CompanyFinancialDestinationAudit>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RenamedEvent_puebla_OldName_NewName_y_deja_los_demas_pares_en_null()
    {
        var audit = new Mock<IAuditService>();
        var handler = new CompanyFinancialDestinationAuditHandler(audit.Object, Context().Object);
        using var cts = new CancellationTokenSource();

        var evt = new CompanyFinancialDestinationRenamedEvent(
            TenantId,
            DestinationId,
            "BANCO-001",
            "Cuenta corriente Pichincha",
            "Nueva razón social visible"
        );

        await handler.Handle(evt, cts.Token);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<CompanyFinancialDestinationAudit>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.EntityId == DestinationId
                        && r.UserId == UserId
                        && r.Code == "BANCO-001"
                        && r.Action == "Renamed"
                        && r.OldName == "Cuenta corriente Pichincha"
                        && r.NewName == "Nueva razón social visible"
                        && r.OldIsActive == null
                        && r.NewIsActive == null
                        && r.OldAccountingAccountId == null
                        && r.NewAccountingAccountId == null
                    ),
                    cts.Token
                ),
            Times.Once
        );
        audit.Verify(
            a =>
                a.RecordAsync(
                    It.IsAny<CompanyFinancialDestinationAudit>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ActiveChangedEvent_puebla_OldIsActive_NewIsActive_y_deja_los_demas_pares_en_null()
    {
        var audit = new Mock<IAuditService>();
        var handler = new CompanyFinancialDestinationAuditHandler(audit.Object, Context().Object);
        using var cts = new CancellationTokenSource();

        var evt = new CompanyFinancialDestinationActiveChangedEvent(
            TenantId,
            DestinationId,
            "BANCO-001",
            true,
            false
        );

        await handler.Handle(evt, cts.Token);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<CompanyFinancialDestinationAudit>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.EntityId == DestinationId
                        && r.UserId == UserId
                        && r.Code == "BANCO-001"
                        && r.Action == "Deactivated"
                        && r.OldIsActive == true
                        && r.NewIsActive == false
                        && r.OldName == null
                        && r.NewName == null
                        && r.OldAccountingAccountId == null
                        && r.NewAccountingAccountId == null
                    ),
                    cts.Token
                ),
            Times.Once
        );
        audit.Verify(
            a =>
                a.RecordAsync(
                    It.IsAny<CompanyFinancialDestinationAudit>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_AccountChangedEvent_puebla_OldAccountingAccountId_NewAccountingAccountId_y_deja_los_demas_pares_en_null()
    {
        var audit = new Mock<IAuditService>();
        var handler = new CompanyFinancialDestinationAuditHandler(audit.Object, Context().Object);
        using var cts = new CancellationTokenSource();
        var oldAccountId = Guid.NewGuid();
        var newAccountId = Guid.NewGuid();

        var evt = new CompanyFinancialDestinationAccountChangedEvent(
            TenantId,
            DestinationId,
            "BANCO-001",
            oldAccountId,
            newAccountId
        );

        await handler.Handle(evt, cts.Token);

        audit.Verify(
            a =>
                a.RecordAsync(
                    It.Is<CompanyFinancialDestinationAudit>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.EntityId == DestinationId
                        && r.UserId == UserId
                        && r.Code == "BANCO-001"
                        && r.Action == "AccountChanged"
                        && r.OldAccountingAccountId == oldAccountId
                        && r.NewAccountingAccountId == newAccountId
                        && r.OldName == null
                        && r.NewName == null
                        && r.OldIsActive == null
                        && r.NewIsActive == null
                    ),
                    cts.Token
                ),
            Times.Once
        );
        audit.Verify(
            a =>
                a.RecordAsync(
                    It.IsAny<CompanyFinancialDestinationAudit>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        audit.VerifyNoOtherCalls();
    }
}
