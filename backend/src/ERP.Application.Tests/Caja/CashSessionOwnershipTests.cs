using ERP.Application.Common;
using ERP.Application.Modules.Caja;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B — una sesión de caja solo la opera quien la abrió
/// (<see cref="CashSession.IsControlledBy"/>). El permiso (`caja.record`/`caja.close`, exigido por
/// política en el controller) decide QUÉ puede hacer el usuario; la propiedad decide SOBRE QUÉ
/// sesión. Estos tests llegan al handler — es decir, con el permiso ya concedido — y prueban que
/// el permiso sin propiedad no basta. El caso inverso (propiedad sin permiso) lo cubre
/// <c>CashSessionOwnershipPolicyTests</c> (Architecture) verificando las políticas de los endpoints.
/// </summary>
public sealed class CashSessionOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static CashSession OpenSessionOwnedBy(Guid ownerId) =>
        CashSession.Open(
            TenantId, CompanyId, BranchId, ownerId, Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 100m, ownerId
        );

    private static OperationalPreferences Preferences() =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(true, true, 0m, false, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    private static Mock<ICurrentUser> User(Guid userId)
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(userId);
        user.Setup(u => u.FullName).Returns("Usuario Test");
        return user;
    }

    private static Mock<ICurrentTenant> Tenant()
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        return tenant;
    }

    private static Mock<ICurrentBranch> Branch()
    {
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        return branch;
    }

    private static Mock<IOperationalPreferencesResolver> PreferencesResolver()
    {
        var resolver = new Mock<IOperationalPreferencesResolver>();
        resolver.Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Preferences());
        return resolver;
    }

    // ── Regla de dominio (SSOT) ───────────────────────────────────────────

    [Fact]
    public void IsControlledBy_solo_para_el_usuario_que_abrio_la_sesion_mientras_esta_abierta()
    {
        var session = OpenSessionOwnedBy(OwnerId);

        session.IsControlledBy(OwnerId).Should().BeTrue();
        session.IsControlledBy(OtherUserId).Should().BeFalse();
        session.IsControlledBy(Guid.Empty).Should().BeFalse();

        session.Close(OwnerId, new List<CashClosingCount>(), "cierre");
        session.IsControlledBy(OwnerId).Should().BeFalse("una sesión cerrada ya no la controla nadie");
    }

    // ── Movimiento manual ─────────────────────────────────────────────────

    private static (RecordCashMovementHandler Handler, Mock<ICashSessionRepository> Repo) BuildRecordHandler(
        CashSession session,
        Guid currentUserId
    )
    {
        var repo = new Mock<ICashSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var reasons = new Mock<ICashMovementReasonRepository>();
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "MOTIVO", "Motivo", CashMovementType.ManualExpense, 1, OwnerId
        );
        reasons
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reason);
        var handler = new RecordCashMovementHandler(
            repo.Object, reasons.Object, Tenant().Object, Branch().Object, User(currentUserId).Object,
            PreferencesResolver().Object
        );
        return (handler, repo);
    }

    private static RecordCashMovementCommand ManualExpense(Guid sessionId) =>
        new(sessionId, "ManualExpense", Guid.NewGuid(), 20m, "Compra menor");

    [Fact]
    public async Task Dueño_registra_movimiento_manual_en_su_caja()
    {
        var session = OpenSessionOwnedBy(OwnerId);
        var (handler, repo) = BuildRecordHandler(session, OwnerId);

        var result = await handler.Handle(ManualExpense(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        session.CurrentBalance.Should().Be(80m);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Otro_usuario_con_permiso_caja_record_no_puede_mover_la_caja_ajena()
    {
        var session = OpenSessionOwnedBy(OwnerId);
        var (handler, repo) = BuildRecordHandler(session, OtherUserId);

        var result = await handler.Handle(ManualExpense(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be(CashSessionOwnership.OperatedByAnotherUserMessage);
        session.Movements.Should().ContainSingle("solo la apertura: ningún movimiento ajeno");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Cierre ────────────────────────────────────────────────────────────

    private static (CloseCashSessionHandler Handler, Mock<ICashSessionRepository> Repo) BuildCloseHandler(
        CashSession session,
        Guid currentUserId
    )
    {
        var repo = new Mock<ICashSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var handler = new CloseCashSessionHandler(
            repo.Object,
            new Mock<IEmissionPointRepository>().Object,
            new Mock<ICashRegisterRepository>().Object,
            Tenant().Object,
            Branch().Object,
            User(currentUserId).Object,
            PreferencesResolver().Object
        );
        return (handler, repo);
    }

    private static CloseCashSessionCommand CloseCommand(Guid sessionId) =>
        new(sessionId, new List<CashClosingCountInput> { new(1m, "Billete $1", 100) });

    [Fact]
    public async Task Dueño_puede_cerrar_su_caja()
    {
        var session = OpenSessionOwnedBy(OwnerId);
        var (handler, repo) = BuildCloseHandler(session, OwnerId);

        var result = await handler.Handle(CloseCommand(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        session.Status.Should().Be(CashSessionStatus.Closed);
        session.ClosedBy.Should().Be(OwnerId);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Otro_usuario_con_permiso_caja_close_no_puede_cerrar_la_caja_ajena()
    {
        var session = OpenSessionOwnedBy(OwnerId);
        var (handler, repo) = BuildCloseHandler(session, OtherUserId);

        var result = await handler.Handle(CloseCommand(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CashSessionOwnership.OperatedByAnotherUserMessage);
        session.Status.Should().Be(CashSessionStatus.Open, "la sesión ajena queda intacta");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cerrar_una_sesion_ya_cerrada_informa_que_no_esta_abierta_no_que_es_ajena()
    {
        var session = OpenSessionOwnedBy(OwnerId);
        session.Close(OwnerId, new List<CashClosingCount>(), "cierre");
        var (handler, _) = BuildCloseHandler(session, OwnerId);

        var result = await handler.Handle(CloseCommand(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CashSessionOwnership.NotOpenMessage);
    }
}
