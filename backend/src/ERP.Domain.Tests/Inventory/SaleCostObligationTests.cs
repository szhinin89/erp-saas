using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

public sealed class SaleCostObligationTests
{
    private static StockMovement Movement(decimal quantity, StockMovementType type, decimal? cost = null) =>
        StockMovement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type,
            quantity, "UNIT", 0m, 1, cost ?? 0m, 0m, DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), Guid.NewGuid(),
            unitCost: type == StockMovementType.PurchaseEntry ? cost : null,
            sourceDocLineId: Guid.NewGuid(), valuationUnitCost: cost);

    [Theory]
    [InlineData(null, 3, 0)]
    [InlineData(2, 1, -2)]
    public void Pending_units_are_returned_first_and_only_recognized_cost_is_reversed(int? initialBasis, int coverageAdjustment, int returnedAdjustment)
    {
        decimal? basis = initialBasis;
        var obligation = SaleCostObligation.Create(Movement(-3m, StockMovementType.SaleExit, basis), 3m, basis);
        obligation.Cover(Movement(1m, StockMovementType.PurchaseEntry, 3m), 1m, 3m)
            .CogsAdjustment.Should().Be(coverageAdjustment);
        var allocation = obligation.Return(Movement(1m, StockMovementType.SaleReturn), 1m);
        allocation.PendingQuantity.Should().Be(1m);
        allocation.ResolvedQuantity.Should().Be(0m);
        allocation.CogsAdjustment.Should().Be(returnedAdjustment);
        obligation.PendingQuantity.Should().Be(1m);
        obligation.ResolvedQuantity.Should().Be(1m);
        obligation.ResolvedCost.Should().Be(3m);
    }

    [Fact]
    public void Multiple_regularizations_are_returned_proportionally_after_pending_units()
    {
        var obligation = SaleCostObligation.Create(Movement(-4m, StockMovementType.SaleExit), 4m, null);
        obligation.Cover(Movement(1m, StockMovementType.PurchaseEntry, 2m), 1m, 2m);
        obligation.Cover(Movement(1m, StockMovementType.PurchaseEntry, 4m), 1m, 4m);
        var allocation = obligation.Return(Movement(3m, StockMovementType.SaleReturn), 3m);
        allocation.PendingQuantity.Should().Be(2m);
        allocation.ResolvedQuantity.Should().Be(1m);
        allocation.CogsAdjustment.Should().Be(-3m);
        obligation.PendingQuantity.Should().Be(0m);
        obligation.ResolvedCost.Should().Be(3m);
        var final = obligation.Return(Movement(1m, StockMovementType.SaleReturn), 1m);
        final.CogsAdjustment.Should().Be(-3m);
        obligation.ResolvedCost.Should().Be(0m);
    }

    [Fact]
    public void Returned_units_cannot_be_regularized_or_returned_again()
    {
        var obligation = SaleCostObligation.Create(Movement(-3m, StockMovementType.SaleExit), 3m, null);
        obligation.Return(Movement(3m, StockMovementType.SaleReturn), 3m);
        var cover = () => obligation.Cover(Movement(1m, StockMovementType.PurchaseEntry, 3m), 1m, 3m);
        cover.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
        var duplicate = () => obligation.Return(Movement(1m, StockMovementType.SaleReturn), 1m);
        duplicate.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }
}
