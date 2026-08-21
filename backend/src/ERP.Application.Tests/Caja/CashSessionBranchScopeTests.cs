using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
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

        public CloseFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public CloseCashSessionHandler BuildHandler() =>
            new(Repo.Object, EpRepo.Object, CrRepo.Object, Tenant.Object, Branch.Object, User.Object);
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
}
