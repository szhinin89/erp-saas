using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Application.Modules.Inventory.ItemMatching.Mapping;

public static class ItemMatchingMapper
{
    public static PurchaseReceptionLineMatchDto ToDto(
        PurchaseReceptionLine line,
        Guid? supplierId,
        IReadOnlyList<ItemMatchCandidateDto>? suggestions = null
    ) =>
        new(
            line.Id,
            supplierId,
            line.SupplierCode,
            line.SupplierAuxCode,
            line.Description,
            line.Quantity,
            line.UnitPrice,
            line.ItemId,
            ToStatusCode(line.MatchStatus),
            line.MatchedAt,
            suggestions ?? []
        );

    public static string ToStatusCode(ItemMatchStatus status) =>
        status switch
        {
            ItemMatchStatus.Pending => "PENDING",
            ItemMatchStatus.NeedsReview => "NEEDS_REVIEW",
            ItemMatchStatus.AutoMatched => "AUTO_MATCHED",
            ItemMatchStatus.ManuallyMatched => "MANUALLY_MATCHED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
}
