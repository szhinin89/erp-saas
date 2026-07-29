using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

/// <summary>
/// Fase 3 (Branch Ownership) — invariantes de dominio de StockMovement.BranchId: obligatorio,
/// inmutable, sin setter público. La regla especial de resolución real (BranchId = Warehouse.
/// BranchId, no ICurrentBranch de sesión) se prueba con PostgreSQL real en
/// ERP.Infrastructure.Tests/Persistence/StockMovementBranchOwnershipIntegrationTests — este
/// factory de bajo nivel no conoce Warehouse, solo recibe el BranchId ya resuelto por el
/// repositorio.
/// </summary>
public sealed class StockMovementBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static StockMovement Create(Guid branchId) =>
        StockMovement.Create(
            TenantId,
            branchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            StockMovementType.PositiveAdjust,
            10m,
            "UNIT",
            previousQuantity: 0,
            sequenceNumber: 1,
            runningAverageCost: 5m,
            runningStockValue: 50m,
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reference: null,
            sourceDocId: null,
            sourceDocType: null,
            createdBy: UserId,
            companyId: CompanyId,
            unitCost: 5m
        );

    [Fact]
    public void Create_con_sucursal_valida_persiste_BranchId()
    {
        var branchId = Guid.NewGuid();

        var movement = Create(branchId);

        movement.BranchId.Should().Be(branchId);
    }

    [Fact]
    public void Create_con_BranchId_vacio_lanza_ArgumentException()
    {
        var act = () => Create(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }

    [Fact]
    public void BranchId_no_expone_setter_publico_ni_metodo_ChangeBranch()
    {
        var property = typeof(StockMovement).GetProperty(nameof(StockMovement.BranchId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("BranchId solo se asigna en Create");

        typeof(StockMovement)
            .GetMethods()
            .Any(m => m.Name is "ChangeBranch" or "SetBranch" or "UpdateBranch")
            .Should()
            .BeFalse("un movimiento de Kardex ya registrado nunca cambia de sucursal");
    }

    [Fact]
    public void Dos_movimientos_creados_con_sucursales_distintas_mantienen_su_BranchId_independiente()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var movementA = Create(branchA);
        var movementB = Create(branchB);

        movementA.BranchId.Should().Be(branchA);
        movementB.BranchId.Should().Be(branchB);
        movementA.BranchId.Should().NotBe(movementB.BranchId);
    }
}
