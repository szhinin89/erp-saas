using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.Domain.Tests.Caja;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — <see cref="CashMovementReason"/> es el catálogo
/// administrable de motivos de movimiento manual de caja. Scope obligatorio Tenant+Company
/// (a diferencia de <c>InventoryAdjustmentReason</c>, aquí CompanyId nunca es nullable).
/// </summary>
public sealed class CashMovementReasonTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_TenantId_y_CompanyId()
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "INGRESO_X", "Ingreso X", CashMovementType.ManualIncome, 1, UserId
        );

        reason.TenantId.Should().Be(TenantId);
        reason.CompanyId.Should().Be(CompanyId);
        reason.Code.Should().Be("INGRESO_X");
        reason.Name.Should().Be("Ingreso X");
        reason.MovementType.Should().Be(CashMovementType.ManualIncome);
        reason.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_normaliza_Code_a_mayusculas()
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "ingreso_x", "Ingreso X", CashMovementType.ManualIncome, 1, UserId
        );

        reason.Code.Should().Be("INGRESO_X");
    }

    [Fact]
    public void Create_sin_CompanyId_falla()
    {
        var act = () => CashMovementReason.Create(
            TenantId, Guid.Empty, "X", "X", CashMovementType.ManualIncome, 1, UserId
        );

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(CashMovementType.Opening)]
    [InlineData(CashMovementType.SaleIncome)]
    [InlineData(CashMovementType.SaleRefund)]
    public void Create_con_tipo_de_sistema_falla(CashMovementType systemType)
    {
        var act = () => CashMovementReason.Create(
            TenantId, CompanyId, "X", "X", systemType, 1, UserId
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Opening/SaleIncome/SaleRefund*");
    }

    [Theory]
    [InlineData(CashMovementType.ManualIncome)]
    [InlineData(CashMovementType.ManualExpense)]
    [InlineData(CashMovementType.Withdrawal)]
    public void Create_con_tipo_manual_funciona(CashMovementType manualType)
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "X", "X", manualType, 1, UserId
        );

        reason.MovementType.Should().Be(manualType);
    }

    [Fact]
    public void Update_cambia_Name_y_SortOrder_pero_no_Code_ni_MovementType()
    {
        // TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A — MovementType es inmutable tras crear, igual
        // que Code: Update ya ni siquiera recibe un parámetro para cambiarlo.
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "ORIGINAL", "Nombre original", CashMovementType.ManualIncome, 1, UserId
        );

        reason.Update("Nombre nuevo", 5, UserId);

        reason.Code.Should().Be("ORIGINAL", "Code es inmutable tras la creación");
        reason.MovementType.Should().Be(
            CashMovementType.ManualIncome,
            "MovementType es inmutable tras la creación"
        );
        reason.Name.Should().Be("Nombre nuevo");
        reason.SortOrder.Should().Be(5);
    }

    [Fact]
    public void Update_con_nombre_vacio_falla()
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "X", "X", CashMovementType.ManualIncome, 1, UserId
        );

        var act = () => reason.Update("", 1, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disable_y_Enable_alternan_IsActive_sin_borrar_el_registro()
    {
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, "X", "X", CashMovementType.ManualIncome, 1, UserId
        );

        reason.Disable(UserId);
        reason.IsActive.Should().BeFalse();

        reason.Enable(UserId);
        reason.IsActive.Should().BeTrue();
    }
}
