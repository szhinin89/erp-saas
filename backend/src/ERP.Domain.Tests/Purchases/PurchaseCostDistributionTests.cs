using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Purchases.Exceptions;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — operación canónica: reemplazo (nunca suma), reintento
/// idempotente, revisión, retiro, transporte XML sin duplicar el total y bloqueo de líneas referenciadas.
/// </summary>
public sealed class PurchaseCostDistributionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProductItem = Guid.NewGuid();
    private static readonly Guid ServiceItem = Guid.NewGuid();
    private static readonly Dictionary<Guid, ItemNature> Natures = new()
    {
        [ProductItem] = ItemNature.Product,
        [ServiceItem] = ItemNature.Service,
    };

    private static PurchaseInvoice Draft(params decimal[] prices)
    {
        var inv = PurchaseCostDistributionTestData.Draft(TenantId, UserId);
        inv.ReplaceLines(prices.Select(p => PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Producto", 1, p, "0",
            "UNIT", itemId: ProductItem, warehouseId: Guid.NewGuid())).ToList(), UserId);
        return inv;
    }

    /// <summary>Producto 100 + línea fiscal XML de transporte (Service) 10.</summary>
    private static PurchaseInvoice DraftWithXmlTransport()
    {
        var inv = PurchaseCostDistributionTestData.Draft(TenantId, UserId);
        inv.ReplaceLines([
            PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Producto", 1, 100, "0", "UNIT",
                itemId: ProductItem, warehouseId: Guid.NewGuid(), purchaseReceptionLineId: Guid.NewGuid()),
            PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Transporte", 1, 10, "0", "UNIT",
                itemId: ServiceItem, purchaseReceptionLineId: Guid.NewGuid()),
        ], UserId);
        return inv;
    }

    private static PurchaseCostDistributionChange External(Guid id, long revision, decimal amount, params Guid[] targets) =>
        new(id, revision, PurchaseCostType.Freight, amount, targets, null);

    private static PurchaseCostDistributionChange XmlTransport(PurchaseInvoice inv, params Guid[] targets) =>
        new(Guid.NewGuid(), 0, PurchaseCostType.Freight, null, targets, inv.Lines[1].Id);

    [Fact]
    public void Editing_replaces_the_previous_amount_instead_of_adding()
    {
        var inv = Draft(100, 100);
        var id = Guid.NewGuid();
        var lines = inv.Lines.Select(l => l.Id).ToArray();
        inv.ReplaceCostDistribution(External(id, 0, 10, lines), Natures, UserId);
        inv.ReplaceCostDistribution(External(id, 1, 15, lines), Natures, UserId);

        inv.TotalFreight.Should().Be(15);
        inv.GrandTotal.Should().Be(215);
        inv.CostDistributions.Single().Revision.Should().Be(2);
    }

    [Fact]
    public void Identical_retry_does_not_duplicate_amounts_or_revision()
    {
        var inv = Draft(100, 100);
        var change = External(Guid.NewGuid(), 0, 10, inv.Lines.Select(l => l.Id).ToArray());
        inv.ReplaceCostDistribution(change, Natures, UserId);
        inv.ReplaceCostDistribution(change, Natures, UserId);

        inv.TotalFreight.Should().Be(10);
        inv.CostDistributions.Should().ContainSingle().Which.Revision.Should().Be(1);
    }

    [Fact]
    public void Stale_revision_is_rejected_without_changes()
    {
        var inv = Draft(100, 100);
        var id = Guid.NewGuid();
        inv.ReplaceCostDistribution(External(id, 0, 10, inv.Lines[0].Id), Natures, UserId);

        var act = () => inv.ReplaceCostDistribution(External(id, 0, 20, inv.Lines[0].Id), Natures, UserId);
        act.Should().Throw<PurchaseCostDistributionStaleException>()
            .Which.ApiCode.Should().Be("PURCHASE_COST_DISTRIBUTION_STALE");
        inv.TotalFreight.Should().Be(10);
        var remove = () => inv.RemoveCostDistribution(id, 5, UserId);
        remove.Should().Throw<PurchaseCostDistributionStaleException>();
    }

    [Fact]
    public void External_targets_must_be_items_of_the_purchase()
    {
        var inv = PurchaseCostDistributionTestData.Draft(TenantId, UserId);
        inv.ReplaceLines([
            PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Servicio", 1, 50, "0", "UNIT", itemId: ServiceItem),
            PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Manual", 1, 50, "0", "UNIT"),
        ], UserId);

        inv.ReplaceCostDistribution(External(Guid.NewGuid(), 0, 10, inv.Lines[0].Id), Natures, UserId);
        var withoutItem = () => inv.ReplaceCostDistribution(External(Guid.NewGuid(), 0, 10, inv.Lines[1].Id), Natures, UserId);

        withoutItem.Should().Throw<DomainRuleViolationException>().WithMessage("*Product o Service*");
        inv.TotalFreight.Should().Be(10);
    }

    [Fact]
    public void Remove_reverts_derived_amounts_and_retry_is_noop()
    {
        var inv = Draft(100);
        var (total, landed) = (inv.GrandTotal, inv.Lines[0].LandedUnitCost);
        var id = Guid.NewGuid();
        inv.ReplaceCostDistribution(External(id, 0, 20, inv.Lines[0].Id), Natures, UserId);
        inv.Lines[0].LandedUnitCost.Should().Be(120);

        inv.RemoveCostDistribution(id, 1, UserId);
        inv.RemoveCostDistribution(id, 1, UserId);

        inv.CostDistributions.Should().BeEmpty();
        inv.GrandTotal.Should().Be(total);
        inv.Lines[0].FreightAllocated.Should().Be(0);
        inv.Lines[0].LandedUnitCost.Should().Be(landed);
    }

    [Fact]
    public void Global_discount_reprorates_only_among_original_targets()
    {
        var inv = Draft(100, 100, 100);
        inv.Distribute(PurchaseCostType.Freight, 10, [inv.Lines[0].Id, inv.Lines[1].Id], UserId);

        inv.ApplyGlobalDiscount(10, UserId);

        inv.Lines[2].FreightAllocated.Should().Be(0);
        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(10);
        inv.ExternalDistributedCost.Should().Be(10);
    }

    [Fact]
    public void External_cost_accepts_product_and_service_targets()
    {
        var inv = DraftWithXmlTransport();
        inv.Distribute(PurchaseCostType.OtherCost, 4, inv.Lines.Select(l => l.Id).ToArray(), UserId);

        inv.Lines.Should().OnlyContain(l => l.OtherCostsAllocated > 0);
        inv.GrandTotal.Should().Be(114);
    }

    [Fact]
    public void Xml_transport_moves_net_base_to_products_without_changing_total_or_taxes()
    {
        var inv = DraftWithXmlTransport();
        var (total, cost, taxBase) = (inv.GrandTotal, inv.TotalCostValue, inv.Lines.Sum(l => l.TaxableBase));

        inv.ReplaceCostDistribution(XmlTransport(inv, inv.Lines[0].Id), Natures, UserId);

        inv.GrandTotal.Should().Be(total);
        inv.ExternalDistributedCost.Should().Be(0);
        inv.TotalCostValue.Should().Be(cost);
        inv.Lines.Sum(l => l.TaxableBase).Should().Be(taxBase);
        inv.Lines[0].TotalLineCost.Should().Be(110);
        inv.Lines[0].FreightAllocated.Should().Be(10);
        inv.Lines[1].TotalLineCost.Should().Be(0);
        inv.Lines[1].TaxableBase.Should().Be(10);
    }

    [Fact]
    public void Xml_transport_rejects_service_targets_explicit_amount_and_second_distribution()
    {
        var inv = DraftWithXmlTransport();
        var toService = () => inv.ReplaceCostDistribution(
            new(Guid.NewGuid(), 0, PurchaseCostType.Freight, null, [inv.Lines[1].Id], inv.Lines[1].Id), Natures, UserId);
        toService.Should().Throw<DomainRuleViolationException>();
        var withAmount = () => inv.ReplaceCostDistribution(
            new(Guid.NewGuid(), 0, PurchaseCostType.Freight, 10, [inv.Lines[0].Id], inv.Lines[1].Id), Natures, UserId);
        withAmount.Should().Throw<DomainRuleViolationException>();

        inv.ReplaceCostDistribution(XmlTransport(inv, inv.Lines[0].Id), Natures, UserId);
        var second = () => inv.ReplaceCostDistribution(XmlTransport(inv, inv.Lines[0].Id), Natures, UserId);
        second.Should().Throw<DomainRuleViolationException>();
        inv.CostDistributions.Should().ContainSingle();
    }

    [Fact]
    public void Changing_type_or_source_and_unofficial_precision_are_rejected()
    {
        var inv = Draft(100);
        var id = Guid.NewGuid();
        inv.ReplaceCostDistribution(External(id, 0, 10, inv.Lines[0].Id), Natures, UserId);
        var changeType = () => inv.ReplaceCostDistribution(
            new(id, 1, PurchaseCostType.OtherCost, 10, [inv.Lines[0].Id], null), Natures, UserId);
        changeType.Should().Throw<DomainRuleViolationException>();
        var threeDecimals = () => inv.ReplaceCostDistribution(External(Guid.NewGuid(), 0, 1.005m, inv.Lines[0].Id), Natures, UserId);
        threeDecimals.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Lines_referenced_by_a_distribution_cannot_be_removed()
    {
        var inv = Draft(100, 100);
        inv.Distribute(PurchaseCostType.Freight, 10, [inv.Lines[1].Id], UserId);
        var kept = inv.Lines[0];

        var act = () => inv.ReplaceLines([kept], UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*distribución de costos*");
        inv.Lines.Should().HaveCount(2);
        inv.TotalFreight.Should().Be(10);
    }

    [Fact]
    public void Confirm_validates_xml_transport_natures_and_freezes_without_duplicating_payable()
    {
        var inv = DraftWithXmlTransport();
        var total = inv.GrandTotal;
        inv.ReplaceCostDistribution(XmlTransport(inv, inv.Lines[0].Id), Natures, UserId);

        var withoutNatures = () => inv.Confirm(UserId);
        withoutNatures.Should().Throw<DomainRuleViolationException>();
        inv.Status.Should().Be(PurchaseStatus.Draft);

        inv.Confirm(UserId, Natures);
        inv.ConfirmedGrandTotal.Should().Be(total);
        var confirmed = inv.DomainEvents.OfType<PurchaseInvoiceConfirmedEvent>().Single();
        confirmed.CostSubtotal.Should().Be(110);
        confirmed.GrandTotal.Should().Be(total);
        var edit = () => inv.RemoveCostDistribution(inv.CostDistributions[0].Id, 1, UserId);
        edit.Should().Throw<DomainRuleViolationException>();
    }
}
