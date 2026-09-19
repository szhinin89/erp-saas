using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — CRUD del catálogo de motivos de movimiento manual. Cubre
/// el scope obligatorio Tenant+Company (siempre de <c>ICurrentTenant</c>/<c>ICurrentCompany</c>,
/// nunca del body) y el filtro por tipo usado por el select del formulario.
/// </summary>
public sealed class CashMovementReasonUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICashMovementReasonRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
        }
    }

    [Fact]
    public async Task List_filtra_por_tenant_company_y_movementType_activos()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.ListAsync(TenantId, CompanyId, CashMovementType.ManualIncome, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CashMovementReason.Create(TenantId, CompanyId, "A", "A", CashMovementType.ManualIncome, 1, UserId),
            });

        var handler = new GetCashMovementReasonsHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object);
        var result = await handler.Handle(
            new GetCashMovementReasonsQuery("ManualIncome"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().ContainSingle();
        f.Repo.Verify(
            r => r.ListAsync(TenantId, CompanyId, CashMovementType.ManualIncome, false, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Create_rechaza_codigo_duplicado_en_la_misma_empresa()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetByCodeAsync(TenantId, CompanyId, "DUP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashMovementReason.Create(TenantId, CompanyId, "DUP", "Existente", CashMovementType.ManualIncome, 1, UserId));

        var handler = new CreateCashMovementReasonHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object, f.User.Object);
        var result = await handler.Handle(
            new CreateCashMovementReasonCommand("DUP", "Nuevo", "ManualIncome"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<CashMovementReason>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_persiste_con_TenantId_y_CompanyId_del_contexto_actual_nunca_del_body()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetByCodeAsync(TenantId, CompanyId, "NUEVO", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashMovementReason?)null);
        CashMovementReason? captured = null;
        f.Repo
            .Setup(r => r.AddAsync(It.IsAny<CashMovementReason>(), It.IsAny<CancellationToken>()))
            .Callback<CashMovementReason, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var handler = new CreateCashMovementReasonHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object, f.User.Object);
        var result = await handler.Handle(
            new CreateCashMovementReasonCommand("NUEVO", "Motivo nuevo", "Withdrawal", 3),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.MovementType.Should().Be(CashMovementType.Withdrawal);
    }

    [Fact]
    public async Task Update_de_un_motivo_de_otra_empresa_devuelve_NotFound()
    {
        var f = new Fixture();
        // El repo real filtra por (Tenant, Company) — un motivo de otra empresa nunca se
        // resuelve aquí, así que el mock simula "no encontrado" en ese scope.
        f.Repo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashMovementReason?)null);

        var handler = new UpdateCashMovementReasonHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object, f.User.Object);
        var result = await handler.Handle(
            new UpdateCashMovementReasonCommand(Guid.NewGuid(), "X", 1),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A ────────────────────────────
    // MovementType inmutable tras crear, igual que Code — UpdateCashMovementReasonCommand ya ni
    // siquiera acepta el campo, así que no hay forma de que el handler lo cambie.

    [Fact]
    public async Task Update_cambia_Name_y_SortOrder_pero_nunca_MovementType_ni_Code()
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "ORIGINAL", "Nombre original", CashMovementType.Withdrawal, 1, UserId
        );
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reason);

        var handler = new UpdateCashMovementReasonHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object, f.User.Object);
        var result = await handler.Handle(
            new UpdateCashMovementReasonCommand(reason.Id, "Nombre nuevo", 9),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Name.Should().Be("Nombre nuevo");
        result.Value.SortOrder.Should().Be(9);
        result.Value.Code.Should().Be("ORIGINAL");
        result.Value.MovementType.Should().Be(CashMovementType.Withdrawal.ToString());
        reason.MovementType.Should().Be(
            CashMovementType.Withdrawal,
            "un registro existente nunca cambia de clasificación por una edición"
        );
    }

    [Fact]
    public async Task Toggle_alterna_IsActive()
    {
        var reason = CashMovementReason.Create(TenantId, CompanyId, "X", "X", CashMovementType.ManualIncome, 1, UserId);
        var f = new Fixture();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, CompanyId, reason.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reason);

        var handler = new ToggleCashMovementReasonHandler(f.Repo.Object, f.Tenant.Object, f.Company.Object, f.User.Object);
        var result = await handler.Handle(new ToggleCashMovementReasonCommand(reason.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.IsActive.Should().BeFalse();
    }
}
