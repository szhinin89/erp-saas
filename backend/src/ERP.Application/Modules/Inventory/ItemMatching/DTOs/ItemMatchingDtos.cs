namespace ERP.Application.Modules.Inventory.ItemMatching.DTOs;

public sealed record ItemMatchCandidateDto(
    Guid ItemId, string Sku, string ShortName, string Description,
    decimal MatchScore, string MatchReason);

public sealed record PurchaseReceptionLineMatchDto(
    Guid LineId,
    Guid? SupplierId,
    string? SupplierCode,
    string? SupplierAuxCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? ItemId,
    string MatchStatus,
    DateTime? MatchedAt,
    IReadOnlyList<ItemMatchCandidateDto> Suggestions);

public sealed record BulkMatchItemEntry(Guid PurchaseReceptionLineId, Guid ItemId);

public sealed record BulkMatchItemResultEntry(Guid PurchaseReceptionLineId, bool Success, string? Error);

public sealed record BulkMatchItemsResultDto(IReadOnlyList<BulkMatchItemResultEntry> Results);
