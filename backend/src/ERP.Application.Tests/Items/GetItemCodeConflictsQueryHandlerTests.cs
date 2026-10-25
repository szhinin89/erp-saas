using ERP.Application.Common;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Application.Items.UseCases.CodeConflicts;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

public sealed class GetItemCodeConflictsQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private readonly Mock<IItemRepository> _items = new();

    private async Task<IReadOnlyList<ItemCodeConflictDto>> RunAsync(params ItemCodeCandidate[] codes)
    {
        var result = await new GetItemCodeConflictsQueryHandler(_items.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId))
            .Handle(new GetItemCodeConflictsQuery(codes), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    private void Owns(string kind, string code, Guid? supplierId = null, Guid? excludeId = null) =>
        _items.Setup(i => i.FindCodeOwnerAsync(new ItemCodeLookup(kind, code, supplierId, excludeId),
                TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemCodeOwner(OwnerId, "Arroz 1 kg"));

    [Theory]
    [InlineData("Sku", " sku-1 ", "SKU-1")]
    [InlineData("Barcode", " 7861234567898 ", "7861234567898")]
    public async Task Existing_code_reports_owner_name_and_rows(string kind, string input, string normalized)
    {
        Owns(kind, normalized);
        var conflicts = await RunAsync(new ItemCodeCandidate(kind, input, "3", "item"));
        conflicts.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Kind = kind, Code = normalized, ItemId = (Guid?)OwnerId, ItemName = "Arroz 1 kg", Rows = new[] { "3" } });
    }

    [Fact]
    public async Task Supplier_code_is_looked_up_only_for_its_supplier()
    {
        var supplierA = Guid.NewGuid();
        Owns("SupplierCode", "07", supplierA);
        var conflicts = await RunAsync(
            new ItemCodeCandidate("SupplierCode", "07", "1", "a", supplierA),
            new ItemCodeCandidate("SupplierCode", "07", "2", "b", Guid.NewGuid()));
        conflicts.Should().ContainSingle().Which.Rows.Should().Equal("1");
    }

    [Fact]
    public async Task Same_code_for_distinct_targets_in_one_batch_is_a_batch_conflict_without_owner()
    {
        var conflicts = await RunAsync(
            new ItemCodeCandidate("Barcode", "ABC", "1", "new-1"),
            new ItemCodeCandidate("Barcode", "ABC", "4", "new-2"),
            new ItemCodeCandidate("Barcode", "SAME", "5", "new-3:1"),
            new ItemCodeCandidate("Barcode", "SAME", "6", "new-3:1"));
        conflicts.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Kind = "Barcode", Code = "ABC", ItemId = (Guid?)null, Rows = new[] { "1", "4" } });
    }

    [Fact]
    public async Task Editing_excludes_the_current_record()
    {
        var conflicts = await RunAsync(new ItemCodeCandidate("Sku", "SKU-1", "1", "item", ExcludeId: OwnerId));
        conflicts.Should().BeEmpty();
        _items.Verify(i => i.FindCodeOwnerAsync(new ItemCodeLookup("Sku", "SKU-1", null, OwnerId),
            TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Variant_sku_is_resolved_with_the_domain_rule_and_unknown_items_are_skipped()
    {
        var item = Item.Create(TenantId, "BASE", "Camisa", "Camisa", Guid.NewGuid(), "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(),
            Guid.NewGuid(), companyId: Guid.NewGuid());
        _items.Setup(i => i.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        Owns("VariantSku", "BASE-RED");
        var attribute = new VariantAttributeInput(Guid.NewGuid(), "red");
        var conflicts = await RunAsync(
            new ItemCodeCandidate("VariantSku", "", "1", "variant", Variant: new(item.Id, [attribute])),
            new ItemCodeCandidate("VariantSku", "", "2", "foreign", Variant: new(Guid.NewGuid(), [attribute])));
        conflicts.Should().ContainSingle().Which.Code.Should().Be("BASE-RED");
    }
}
