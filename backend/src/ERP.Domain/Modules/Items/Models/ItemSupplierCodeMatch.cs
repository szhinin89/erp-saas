namespace ERP.Domain.Modules.Items.Models;

public sealed record ItemSupplierCodeMatch(
    Guid ItemId,
    Guid? PackagingLevelId,
    string? PackagingUomCode,
    decimal? PackagingBaseQuantity,
    string BaseUomCode
);
