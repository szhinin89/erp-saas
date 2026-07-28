using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.Domain.Tests.Purchases.PurchaseReception;

public sealed class PurchaseReceptionLineTests
{
    private static PurchaseReceptionLine CreateLine() =>
        PurchaseReceptionLine.Create(
            documentId: Guid.NewGuid(), tenantId: Guid.NewGuid(),
            description: "COCA COLA 500 ML", quantity: 10m, unitPrice: 0.5m,
            vatCode: "2", taxCode: "2", vatPercentage: 15m, taxValue: 0.75m,
            discountPct: 0m, discount: 0m, lineSubtotal: 5m, totalLine: 5.75m,
            supplierCode: "PROV-001", supplierAuxCode: "AUX-1");

    [Fact]
    public void Create_defaults_to_Pending_without_an_item()
    {
        var line = CreateLine();

        line.MatchStatus.Should().Be(ItemMatchStatus.Pending);
        line.ItemId.Should().BeNull();
        line.MatchedAt.Should().BeNull();
        line.MatchedBy.Should().BeNull();
    }

    [Fact]
    public void AutoMatch_resolves_the_item_without_matched_audit_fields()
    {
        var line = CreateLine();
        var itemId = Guid.NewGuid();

        line.AutoMatch(itemId);

        line.ItemId.Should().Be(itemId);
        line.MatchStatus.Should().Be(ItemMatchStatus.AutoMatched);
        line.MatchedAt.Should().BeNull();
        line.MatchedBy.Should().BeNull();
    }

    [Fact]
    public void MarkNeedsReview_moves_a_pending_line_without_resolving_the_item()
    {
        var line = CreateLine();

        line.MarkNeedsReview();

        line.MatchStatus.Should().Be(ItemMatchStatus.NeedsReview);
        line.ItemId.Should().BeNull();
    }

    [Fact]
    public void MarkNeedsReview_does_not_override_an_already_resolved_line()
    {
        var line = CreateLine();
        var itemId = Guid.NewGuid();
        line.AutoMatch(itemId);

        line.MarkNeedsReview();

        line.MatchStatus.Should().Be(ItemMatchStatus.AutoMatched);
    }

    [Fact]
    public void ManualMatch_sets_item_and_audit_fields()
    {
        var line = CreateLine();
        var itemId = Guid.NewGuid();
        var matchedBy = Guid.NewGuid();
        var matchedAt = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

        line.ManualMatch(itemId, matchedBy, matchedAt);

        line.ItemId.Should().Be(itemId);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
        line.MatchedAt.Should().Be(matchedAt);
        line.MatchedBy.Should().Be(matchedBy);
    }

    [Fact]
    public void ManualMatch_can_correct_a_previous_auto_match()
    {
        var line = CreateLine();
        line.AutoMatch(Guid.NewGuid());

        var correctedItemId = Guid.NewGuid();
        var matchedBy = Guid.NewGuid();
        line.ManualMatch(correctedItemId, matchedBy, DateTime.UtcNow);

        line.ItemId.Should().Be(correctedItemId);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
    }

    [Fact]
    public void Create_throws_when_a_resolved_status_has_no_item()
    {
        var act = () => PurchaseReceptionLine.Create(
            documentId: Guid.NewGuid(), tenantId: Guid.NewGuid(),
            description: "Línea test", quantity: 1m, unitPrice: 1m,
            vatCode: "2", taxCode: "2", vatPercentage: 15m, taxValue: 0.15m,
            discountPct: 0m, discount: 0m, lineSubtotal: 1m, totalLine: 1.15m,
            matchStatus: ItemMatchStatus.AutoMatched);

        act.Should().Throw<ArgumentException>();
    }
}
