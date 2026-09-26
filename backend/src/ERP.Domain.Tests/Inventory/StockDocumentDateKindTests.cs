using ERP.Domain.Modules.Inventory.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

/// <summary>
/// ZH-DATETIME-KIND-HARDENING-01 → ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — TransferDate/AdjustmentDate
/// son fechas de negocio (día operativo de la empresa, DateOnly resuelto por ICompanyClock). Ya no se
/// almacenan como medianoche UTC en timestamptz: el contrato único de fecha de negocio es
/// <c>DateOnly</c> ↔ PostgreSQL <c>date</c> ↔ API "YYYY-MM-DD" — el día entra y sale idéntico.
/// </summary>
public sealed class StockDocumentDateKindTests
{
    private static readonly DateOnly BusinessDate = new(2026, 9, 25);

    [Fact]
    public void StockTransfer_Create_ConservaElDiaDeNegocioComoDateOnly()
    {
        var transfer = StockTransfer.Create(
            tenantId: Guid.NewGuid(),
            sequential: 1,
            operationBranchId: Guid.NewGuid(),
            sourceWarehouseId: Guid.NewGuid(),
            targetWarehouseId: Guid.NewGuid(),
            reason: null,
            notes: null,
            createdBy: Guid.NewGuid(),
            transferDate: BusinessDate,
            companyId: Guid.NewGuid()
        );

        transfer.TransferDate.Should().Be(BusinessDate);
    }

    [Fact]
    public void StockAdjustment_Create_ConservaElDiaDeNegocioComoDateOnly()
    {
        var adjustment = StockAdjustment.Create(
            tenantId: Guid.NewGuid(),
            sequential: 1,
            warehouseId: Guid.NewGuid(),
            warehouseName: "Bodega Central",
            movementType: StockAdjustment.MovementTypeIngreso,
            reasonId: Guid.NewGuid(),
            notes: null,
            createdBy: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            adjustmentDate: BusinessDate
        );

        adjustment.AdjustmentDate.Should().Be(BusinessDate);
    }
}
