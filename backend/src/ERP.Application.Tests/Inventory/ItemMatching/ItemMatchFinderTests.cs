using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

public sealed class ItemMatchFinderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static Item CreateItem(string sku, string shortName, string description) => Item.Create(
        TenantId, sku, shortName, description, ItemTypeId, "UNIT",
        ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), UserId);

    [Fact]
    public async Task Exact_supplier_code_wins_even_when_the_same_item_would_also_match_by_similarity()
    {
        var item = CreateItem("SKU-001", "Coca Cola 500ML", "Coca Cola botella 500ML");
        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.FindItemIdBySupplierCodeAsync(SupplierId, "PROV-001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item.Id);
        itemRepo.Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        itemRepo.Setup(r => r.SearchBySimilarityAsync(It.IsAny<string>(), TenantId, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ItemSimilarityMatch(item.Id, item.Code.SKU, item.Code.ShortName, item.Code.Description, 1.0)]);

        var finder = new ItemMatchFinder(itemRepo.Object);
        var candidates = await finder.FindCandidatesAsync(
            TenantId, SupplierId, "PROV-001", null, "COCA COLA BOTELLA 500ML", maxResults: 5);

        candidates.Should().ContainSingle();
        candidates[0].ItemId.Should().Be(item.Id);
        candidates[0].MatchScore.Should().Be(100m);
        candidates[0].MatchReason.Should().Be(ItemMatchFinder.ReasonSupplierCodeExact);
    }

    [Fact]
    public async Task Auxiliary_code_is_used_when_there_is_no_exact_supplier_code_match()
    {
        var item = CreateItem("SKU-002", "Arroz 5kg", "Arroz superior 5kg");
        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.FindItemIdBySupplierCodeAsync(SupplierId, "PROV-002", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        itemRepo.Setup(r => r.FindItemIdBySupplierCodeAsync(SupplierId, "AUX-002", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item.Id);
        itemRepo.Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        itemRepo.Setup(r => r.SearchBySimilarityAsync(It.IsAny<string>(), TenantId, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var finder = new ItemMatchFinder(itemRepo.Object);
        var candidates = await finder.FindCandidatesAsync(
            TenantId, SupplierId, "PROV-002", "AUX-002", "Arroz superior 5kg", maxResults: 5);

        candidates.Should().ContainSingle();
        candidates[0].ItemId.Should().Be(item.Id);
        candidates[0].MatchReason.Should().Be(ItemMatchFinder.ReasonSupplierAuxCodeExact);
    }

    [Fact]
    public async Task Normalized_description_equality_upgrades_the_similarity_score()
    {
        var item = CreateItem("SKU-003", "Coca-Cola 500 ML", "Coca-Cola 500 ML");
        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.FindItemIdBySupplierCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        itemRepo.Setup(r => r.SearchBySimilarityAsync(It.IsAny<string>(), TenantId, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ItemSimilarityMatch(item.Id, item.Code.SKU, item.Code.ShortName, item.Code.Description, 0.5)]);

        var finder = new ItemMatchFinder(itemRepo.Object);
        var candidates = await finder.FindCandidatesAsync(
            TenantId, supplierId: null, supplierCode: null, supplierAuxCode: null,
            description: "coca cola 500 ml", maxResults: 5);

        candidates.Should().ContainSingle();
        candidates[0].MatchScore.Should().Be(95m);
        candidates[0].MatchReason.Should().Be(ItemMatchFinder.ReasonDescriptionNormalized);
    }

    [Fact]
    public async Task Plain_similarity_candidates_use_the_pg_trgm_score()
    {
        var item = CreateItem("SKU-004", "Leche entera", "Leche entera 1L");
        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.FindItemIdBySupplierCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        itemRepo.Setup(r => r.SearchBySimilarityAsync(It.IsAny<string>(), TenantId, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ItemSimilarityMatch(item.Id, item.Code.SKU, item.Code.ShortName, item.Code.Description, 0.42)]);

        var finder = new ItemMatchFinder(itemRepo.Object);
        var candidates = await finder.FindCandidatesAsync(
            TenantId, supplierId: null, supplierCode: null, supplierAuxCode: null,
            description: "Leche deslactosada 1L", maxResults: 5);

        candidates.Should().ContainSingle();
        candidates[0].MatchScore.Should().Be(42m);
        candidates[0].MatchReason.Should().Be(ItemMatchFinder.ReasonDescriptionSimilarity);
    }
}
