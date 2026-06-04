namespace ERP.Application.SupplierCatalog.DTOs;

public record SupplierItemDto(
    Guid                            Id,
    Guid                            SupplierId,
    Guid                            ItemId,
    Guid?                           VariantId,
    string                          SupplierCode,
    string?                         SupplierName,
    string                          DefaultUomCode,
    int                             LeadTimeDays,
    decimal                         MinOrderQty,
    bool                            IsDefault,
    bool                            IsActive,
    IReadOnlyList<SupplierPriceDto> Prices
);

public record SupplierPriceDto(
    Guid     Id,
    decimal  MinQty,
    decimal  UnitPrice,
    string   CurrencyCode,
    DateOnly? ValidFrom,
    DateOnly? ValidTo
);
