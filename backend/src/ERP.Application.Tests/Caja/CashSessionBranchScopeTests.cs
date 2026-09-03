using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX01 (P0-1 y P0-2) — cierre de caja, registro de movimiento y lectura de
/// detalle por id resolvían la CashSession solo por TenantId (vía Scoped(tenantId), que además ya
/// filtra por Company), sin comparar <c>session.BranchId</c> contra la sucursal activa del llamante
/// (<see cref="ICurrentBranch"/>). Esto permitía cerrar/mutar/leer la caja de otra sucursal de la
/// misma empresa conociendo su GUID. Ahora las tres rutas devuelven NotFound cuando la sesión
/// pertenece a otra sucursal.
/// </summary>
public sealed class CashSessionBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static OperationalPreferences DefaultPreferences(
        bool requireReasonForDifference = true,
        bool allowCloseWithDifference = true,
        decimal maxAllowedDifference = 0m
    ) =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(
                true,
                allowCloseWithDifference,
                maxAllowedDifference,
                requireReasonForDifference,
                true,
                true
            ),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    private static CashSession CreateOpenSession(Guid branchId) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "002",
            100m,
            UserId
        );

    // ── CloseCashSessionHandler ──────────────────────────────────────────

    private sealed class CloseFixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<ICashRegisterRepository> CrRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();

        public CloseFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            Preferences
                .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DefaultPreferences());
        }

        public CloseCashSessionHandler BuildHandler() =>
            new(
                Repo.Object,
                EpRepo.Object,
                CrRepo.Object,
                Tenant.Object,
                Branch.Object,
                User.Object,
                Preferences.Object
            );
    }

    [Fact]
    public async Task Cerrar_caja_de_otra_sucursal_devuelve_NotFound()
    {
        var session = CreateOpenSession(BranchBId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 5) }
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cerrar_caja_de_la_sucursal_activa_funciona()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 100) }
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cerrar_con_diferencia_sin_notas_falla_si_la_preferencia_lo_exige()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences(requireReasonForDifference: true));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 80) },
                    CloseNotes: null
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cerrar_con_diferencia_sin_notas_funciona_si_la_preferencia_no_lo_exige()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences(requireReasonForDifference: false));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 80) },
                    CloseNotes: null
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cerrar_con_diferencia_falla_si_la_preferencia_no_permite_cerrar_con_diferencia()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences(allowCloseWithDifference: false));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 80) },
                    CloseNotes: "Diferencia por vuelto mal entregado"
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cerrar_con_diferencia_falla_si_supera_el_maximo_permitido()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences(maxAllowedDifference: 5m));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    // Sesión abierta con 100; conteo de 80 = diferencia de 20, supera el máximo de 5.
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 80) },
                    CloseNotes: "Diferencia por vuelto mal entregado"
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cerrar_con_diferencia_dentro_del_maximo_permitido_funciona()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new CloseFixture(activeBranchId: BranchAId);
        f.Preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences(maxAllowedDifference: 50m));
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new CloseCashSessionCommand(
                    session.Id,
                    new List<CashClosingCountInput> { new(1m, "Billete $1", 80) },
                    CloseNotes: "Diferencia por vuelto mal entregado"
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RecordCashMovementHandler ────────────────────────────────────────

    private sealed class MovementFixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public MovementFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public RecordCashMovementHandler BuildHandler() =>
            new(Repo.Object, Tenant.Object, Branch.Object, User.Object);
    }

    [Fact]
    public async Task Registrar_movimiento_en_caja_de_otra_sucursal_devuelve_NotFound()
    {
        var session = CreateOpenSession(BranchBId);
        var f = new MovementFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new RecordCashMovementCommand(session.Id, "ManualIncome", 20m, "Ingreso manual"),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Registrar_movimiento_en_caja_de_la_sucursal_activa_funciona()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new MovementFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(
                new RecordCashMovementCommand(session.Id, "ManualIncome", 20m, "Ingreso manual"),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetCashSessionByIdHandler ────────────────────────────────────────

    private sealed class GetByIdFixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<ICashRegisterRepository> CrRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public GetByIdFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
        }

        public GetCashSessionByIdHandler BuildHandler() =>
            new(Repo.Object, EpRepo.Object, CrRepo.Object, Tenant.Object, Branch.Object);
    }

    [Fact]
    public async Task Leer_caja_de_otra_sucursal_devuelve_NotFound()
    {
        var session = CreateOpenSession(BranchBId);
        var f = new GetByIdFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionByIdQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Leer_caja_de_la_sucursal_activa_devuelve_detalle()
    {
        var session = CreateOpenSession(BranchAId);
        var f = new GetByIdFixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await f.BuildHandler()
            .Handle(new GetCashSessionByIdQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Id.Should().Be(session.Id);
    }

    // ── GetCashSessionListHandler ────────────────────────────────────────

    /// <summary>
    /// Hallazgo ALTO auditoría de aislamiento (Caja): GetCashSessionListQuery está marcada
    /// IBranchScopedRequest, pero el handler no pasaba la sucursal activa al repositorio — el
    /// listado de sesiones de caja exponía sesiones de otras sucursales de la misma empresa. Ahora
    /// el handler pasa <c>_b.BranchId</c> a <c>GetPagedAsync</c>, que filtra en base de datos.
    /// </summary>
    [Fact]
    public async Task Listar_pasa_la_sucursal_activa_al_repositorio()
    {
        var repo = new Mock<ICashSessionRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(TenantId, BranchAId, null, 1, 25, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((new List<CashSession> { CreateOpenSession(BranchAId) }, 1));

        var handler = new GetCashSessionListHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchAId)
        );

        var result = await handler.Handle(new GetCashSessionListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Total.Should().Be(1);
        repo.Verify(
            r => r.GetPagedAsync(TenantId, BranchAId, null, 1, 25, It.IsAny<CancellationToken>()),
            Times.Once
        );
        // No debe consultarse jamás con la sucursal B ni sin sucursal (comportamiento previo al fix).
        repo.Verify(
            r =>
                r.GetPagedAsync(
                    It.IsAny<Guid>(),
                    It.Is<Guid>(b => b != BranchAId),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    /// <summary>Cambiar la sucursal activa a B debe pasar BranchB al repositorio — nunca reutilizar A.</summary>
    [Fact]
    public async Task Listar_en_sucursal_B_pasa_BranchB_y_no_BranchA()
    {
        var repo = new Mock<ICashSessionRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(TenantId, BranchBId, null, 1, 25, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((new List<CashSession> { CreateOpenSession(BranchBId) }, 1));

        var handler = new GetCashSessionListHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchBId)
        );

        var result = await handler.Handle(new GetCashSessionListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        repo.Verify(
            r => r.GetPagedAsync(TenantId, BranchBId, null, 1, 25, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
