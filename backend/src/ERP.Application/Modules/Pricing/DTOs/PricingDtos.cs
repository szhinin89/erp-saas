using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Application.Pricing.DTOs;

public record PriceListDto(
    Guid           Id,
    string         Code,
    string         Name,
    string         CurrencyCode,
    bool           PriceIncludesVat,
    DateOnly       ValidFrom,
    DateOnly?      ValidTo,
    bool           IsDefault,
    string         Target,
    bool           IsActive,
    DateTime       CreatedAt
);

public record PriceListEntryDto(
    Guid     Id,
    Guid     ItemId,
    Guid?    VariantId,
    string   UomCode,
    decimal  Price,
    decimal  MinQty,
    bool     IsActive
);

public record ResolvedPriceDto(
    Guid     ItemId,
    Guid?    VariantId,
    string   UomCode,
    decimal  BasePrice,
    decimal  DiscountAmount,
    decimal  FinalPrice,
    bool     PriceIncludesVat,
    Guid     AppliedPriceListId,
    string   AppliedPriceListName,
    string   ResolutionPath
);
