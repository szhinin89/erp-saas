using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — <see cref="RecordCashMovementHandler"/> ahora exige un
/// <c>CashMovementReason</c> válido para movimientos manuales, y rechaza explícitamente los tipos
/// de sistema (Opening/SaleIncome/SaleRefund), que nunca deben crearse por esta vía genérica.
/// </summary>
public sealed class RecordCashMovementHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession OpenSession() =>
        CashSession.Open(
            TenantId, CompanyId, BranchId, UserId, Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 100m, UserId
        );

    private static CashMovementReason ValidReason(
        CashMovementType type = CashMovementType.ManualIncome,
        Guid? tenantId = null,
        Guid? companyId = null,
        bool active = true
    )
    {
        var reason = CashMovementReason.Create(
            tenantId ?? TenantId, companyId ?? CompanyId, "MOTIVO", "Motivo de prueba", type, 1, UserId
        );
        if (!active)
            reason.Disable(UserId);
        return reason;
    }

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> CashRepo { get; } = new();
        public Mock<ICashMovementReasonRepository> ReasonRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            User.Setup(u => u.FullName).Returns("Cajero Test");
        }

        public RecordCashMovementHandler BuildHandler() =>
            new(CashRepo.Object, ReasonRepo.Object, Tenant.Object, Branch.Object, User.Object);
    }

    private static RecordCashMovementCommand Command(
        Guid sessionId,
        Guid reasonId,
        string movementType = "ManualIncome",
        decimal amount = 20m
    ) => new(sessionId, movementType, reasonId, amount, "Descripción de prueba");

    // ── "motivo correcto" ────────────────────────────────────────────────

    [Fact]
    public async Task Motivo_correcto_registra_el_movimiento_con_ReasonId_y_ReasonName()
    {
        var session = OpenSession();
        var reason = ValidReason();
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        f.ReasonRepo.Setup(r => r.GetByIdAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reason);

        var result = await f.BuildHandler().Handle(Command(session.Id, reason.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ReasonId.Should().Be(reason.Id);
        result.Value.ReasonName.Should().Be("Motivo de prueba");
        result.Value.CreatedByName.Should().Be("Cajero Test");
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── "motivo otra empresa rechazado" ─────────────────────────────────

    [Fact]
    public async Task Motivo_de_otra_empresa_es_rechazado()
    {
        var session = OpenSession();
        // El repo real filtraría por (TenantId, session.CompanyId) — un motivo de OtherCompanyId
        // nunca aparece en esa consulta, así que el mock simplemente no lo registra (simula "no existe").
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        f.ReasonRepo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashMovementReason?)null);

        var result = await f.BuildHandler().Handle(Command(session.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no pertenece a esta empresa");
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── "motivo otro tenant rechazado" ───────────────────────────────────

    [Fact]
    public async Task Motivo_de_otro_tenant_es_rechazado()
    {
        var session = OpenSession();
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        // Búsqueda scoped a (TenantId, CompanyId) — un motivo que solo existe bajo OtherTenantId
        // jamás se resuelve aquí, exactamente igual que "no existe".
        f.ReasonRepo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashMovementReason?)null);

        var result = await f.BuildHandler().Handle(Command(session.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no pertenece a esta empresa");
    }

    // ── "motivo inactivo rechazado" ──────────────────────────────────────

    [Fact]
    public async Task Motivo_inactivo_es_rechazado()
    {
        var session = OpenSession();
        var reason = ValidReason(active: false);
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        f.ReasonRepo.Setup(r => r.GetByIdAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reason);

        var result = await f.BuildHandler().Handle(Command(session.Id, reason.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactivo");
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── "motivo incompatible rechazado" ──────────────────────────────────

    [Fact]
    public async Task Motivo_de_otro_MovementType_es_rechazado()
    {
        var session = OpenSession();
        var reason = ValidReason(type: CashMovementType.Withdrawal); // motivo de Retiro
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        f.ReasonRepo.Setup(r => r.GetByIdAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reason);

        // Se intenta usar con ManualIncome — incompatible con el motivo (Withdrawal).
        var result = await f.BuildHandler().Handle(Command(session.Id, reason.Id, movementType: "ManualIncome"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no corresponde a este tipo de movimiento");
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── "Opening/SaleIncome/SaleRefund manual rechazado" ─────────────────

    [Theory]
    [InlineData("Opening")]
    [InlineData("SaleIncome")]
    [InlineData("SaleRefund")]
    public async Task Tipos_de_sistema_son_rechazados_para_registro_manual(string systemType)
    {
        var session = OpenSession();
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var result = await f.BuildHandler().Handle(
            Command(session.Id, Guid.NewGuid(), movementType: systemType),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no se puede registrar manualmente");
        f.ReasonRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "ni siquiera debe consultarse el catálogo de motivos para un tipo de sistema"
        );
        f.CashRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sesion_de_otra_sucursal_devuelve_NotFound_sin_consultar_el_motivo()
    {
        var session = CashSession.Open(
            TenantId, CompanyId, Guid.NewGuid(), UserId, Guid.NewGuid(),
            "CAJA-01", "Caja Principal", Guid.NewGuid(), "001", 0m, UserId
        );
        var f = new Fixture();
        f.CashRepo.Setup(r => r.GetByIdAsync(TenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var result = await f.BuildHandler().Handle(Command(session.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
