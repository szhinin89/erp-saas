using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — Replace/RemovePurchaseCostDistributionHandler: alcance de
/// sucursal y empresa, naturaleza de ítems desde el maestro, reintento y concurrencia.
/// </summary>
public sealed class PurchaseCostDistributionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Item NewItem(ItemNature nature, Guid? companyId = null) =>
        Item.Create(TenantId, $"SKU-{Guid.NewGuid():N}"[..12], "Item", "Item", Guid.NewGuid(), "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(),
            ItemStockConfig.Create(nature == ItemNature.Product), UserId,
            companyId: companyId ?? CompanyId, nature: nature);

    /// <summary>Ítem Product de la empresa por defecto: un costo externo solo se asigna a ítems.</summary>
    private static readonly Item DefaultProduct = NewItem(ItemNature.Product);

    private static PurchaseInvoice Draft(params PurchaseInvoiceDetail[] lines)
    {
        var inv = PurchaseInvoice.CreateDraft(TenantId, CompanyId, BranchId, Guid.NewGuid(), "Proveedor",
            "1791352688001", "01", "001-001-000000010", new DateOnly(2026, 10, 1), UserId, Guid.NewGuid(),
            "Contado", 1, 0);
        inv.ReplaceLines(lines.Select(l => PurchaseInvoiceDetail.Create(inv.Id, TenantId, l.Description, l.Quantity,
            l.UnitPrice, "0", "UNIT", itemId: l.ItemId, purchaseReceptionLineId: l.PurchaseReceptionLineId)).ToList(), UserId);
        return inv;
    }

    private static PurchaseInvoiceDetail Line(string name, decimal price, Guid? itemId = null, bool xml = false) =>
        PurchaseInvoiceDetail.Create(Guid.NewGuid(), TenantId, name, 1, price, "0", "UNIT", itemId: itemId ?? DefaultProduct.Id,
            purchaseReceptionLineId: xml ? Guid.NewGuid() : null);

    private static Mock<IPurchaseInvoiceRepository> Repo(PurchaseInvoice inv)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repo;
    }

    private static ReplacePurchaseCostDistributionHandler Replace(Mock<IPurchaseInvoiceRepository> repo,
        IItemRepository? items = null, Guid? branchId = null) =>
        new(repo.Object, items ?? Items(DefaultProduct), Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == (branchId ?? BranchId)),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId), PrecisionPolicyTestDouble.Mock());

    private static IItemRepository Items(params Item[] items)
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdLightAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => items.SingleOrDefault(i => i.Id == id));
        return repo.Object;
    }

    [Fact]
    public async Task Replace_persists_once_and_identical_retry_keeps_single_amount()
    {
        var inv = Draft(Line("A", 100), Line("B", 300));
        var repo = Repo(inv);
        var cmd = new ReplacePurchaseCostDistributionCommand(inv.Id, Guid.NewGuid(), PurchaseCostType.Freight, 0,
            40m, inv.Lines.Select(l => l.Id).ToList(), null);

        var first = await Replace(repo).Handle(cmd, default);
        var retry = await Replace(repo).Handle(cmd, default);

        first.IsSuccess.Should().BeTrue(first.Error);
        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.TotalFreight.Should().Be(40m);
        retry.Value.Lines.Select(l => l.FreightAllocated).Should().Equal(10m, 30m);
        retry.Value.CostDistributions.Should().ContainSingle().Which.Revision.Should().Be(1);
    }

    [Fact]
    public async Task Other_branch_returns_NotFound_without_saving()
    {
        var inv = Draft(Line("A", 100));
        var repo = Repo(inv);

        var result = await Replace(repo, branchId: Guid.NewGuid()).Handle(new ReplacePurchaseCostDistributionCommand(
            inv.Id, Guid.NewGuid(), PurchaseCostType.Freight, 0, 10m, [inv.Lines[0].Id], null), default);

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Foreign_target_line_is_rejected_without_saving()
    {
        var inv = Draft(Line("A", 100));
        var repo = Repo(inv);

        var act = () => Replace(repo).Handle(new ReplacePurchaseCostDistributionCommand(inv.Id, Guid.NewGuid(),
            PurchaseCostType.Freight, 0, 10m, [Guid.NewGuid()], null), default);

        await act.Should().ThrowAsync<DomainRuleViolationException>();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Xml_transport_resolves_natures_from_master_data_and_keeps_total()
    {
        var product = NewItem(ItemNature.Product);
        var service = NewItem(ItemNature.Service);
        var inv = Draft(Line("Producto", 100, product.Id, xml: true), Line("Transporte", 10, service.Id, xml: true));
        var total = inv.GrandTotal;

        var result = await Replace(Repo(inv), Items(product, service)).Handle(new ReplacePurchaseCostDistributionCommand(
            inv.Id, Guid.NewGuid(), PurchaseCostType.Freight, 0, null, [inv.Lines[0].Id], inv.Lines[1].Id), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.GrandTotal.Should().Be(total);
        result.Value.Lines[0].TotalLineCost.Should().Be(110m);
        result.Value.Lines[1].TotalLineCost.Should().Be(0m);
        result.Value.CostDistributions!.Single().SourceLineId.Should().Be(inv.Lines[1].Id);
    }

    [Fact]
    public async Task Item_from_another_company_is_rejected()
    {
        var foreign = NewItem(ItemNature.Product, Guid.NewGuid());
        var inv = Draft(Line("Producto", 100, foreign.Id));
        var repo = Repo(inv);

        var result = await Replace(repo, Items(foreign)).Handle(new ReplacePurchaseCostDistributionCommand(inv.Id,
            Guid.NewGuid(), PurchaseCostType.Freight, 0, 10m, [inv.Lines[0].Id], null), default);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Database_concurrency_conflict_returns_Conflict()
    {
        var inv = Draft(Line("A", 100));
        var repo = Repo(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException());

        var result = await Replace(repo).Handle(new ReplacePurchaseCostDistributionCommand(inv.Id, Guid.NewGuid(),
            PurchaseCostType.Freight, 0, 10m, [inv.Lines[0].Id], null), default);

        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    [Fact]
    public async Task Remove_reverts_amounts_and_rejects_stale_revision()
    {
        var inv = Draft(Line("A", 100));
        var id = inv.Distribute(PurchaseCostType.OtherCost, 5m, [inv.Lines[0].Id], UserId);
        var repo = Repo(inv);
        var handler = new RemovePurchaseCostDistributionHandler(repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId), Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId), PrecisionPolicyTestDouble.Mock());

        var stale = () => handler.Handle(new RemovePurchaseCostDistributionCommand(inv.Id, id, 7), default);
        await stale.Should().ThrowAsync<ERP.Domain.Modules.Purchases.Exceptions.PurchaseCostDistributionStaleException>();
        var result = await handler.Handle(new RemovePurchaseCostDistributionCommand(inv.Id, id, 1), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TotalOtherCosts.Should().Be(0m);
        result.Value.GrandTotal.Should().Be(100m);
        result.Value.CostDistributions.Should().BeEmpty();
    }

    /// <summary>El handler reconoce el conflicto por nombre de tipo (Application no referencia EF).</summary>
    private sealed class DbUpdateConcurrencyException : Exception;
}
