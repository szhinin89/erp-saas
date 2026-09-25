using ERP.Domain.Modules.Inventory.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

/// <summary>
/// ZH-DATETIME-KIND-HARDENING-01 — TransferDate/AdjustmentDate son fechas de negocio (día operativo
/// de la empresa, DateOnly resuelto por ICompanyClock) almacenadas en columnas timestamptz. La
/// convención del ERP para fechas sin zona es medianoche UTC (ver UtcDateTime.Normalize):
/// <c>DateOnly.ToDateTime(TimeOnly.MinValue)</c> produce Kind=Unspecified, que
/// UtcDateTimeGuardInterceptor rechaza en SaveChanges (INVALID_DATETIME_KIND, HTTP 500).
/// </summary>
public sealed class StockDocumentDateKindTests
{
    private static readonly DateOnly BusinessDate = new(2026, 9, 25);

    [Fact]
    public void StockTransfer_Create_StoresBusinessDateAsUtcMidnight()
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

        transfer.TransferDate.Kind.Should().Be(DateTimeKind.Utc);
        DateOnly.FromDateTime(transfer.TransferDate).Should().Be(BusinessDate);
        transfer.TransferDate.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void StockAdjustment_Create_StoresBusinessDateAsUtcMidnight()
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

        adjustment.AdjustmentDate.Kind.Should().Be(DateTimeKind.Utc);
        DateOnly.FromDateTime(adjustment.AdjustmentDate).Should().Be(BusinessDate);
        adjustment.AdjustmentDate.TimeOfDay.Should().Be(TimeSpan.Zero);
    }
}
