using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;

/// <summary>
/// COMPRAS-METODO-ZH-01B — Item nuevo a crear dentro del lote. Mismos campos obligatorios que
/// <c>CreateItemCommand</c> (se crea a través de él, nunca con otro motor). <see cref="Key"/> es la
/// referencia del cliente que usan las líneas (<see cref="ResolveReceptionLineInput.NewItemKey"/>).
/// </summary>
public sealed record ResolveReceptionNewItemInput(
    string Key,
    string Sku,
    string ShortName,
    string Description,
    Guid ItemTypeId,
    Guid CategoryNodeId,
    Guid BrandId,
    string DefaultUomCode,
    string Barcode,
    string BarcodeType,
    string? SaleVatCode,
    string? PurchaseVatCode,
    string? ExciseTaxCode,
    decimal BaseSalePrice
);

/// <summary>
/// Resolución de una línea XML: vincular un Item existente (<see cref="ItemId"/>, opcionalmente con
/// una presentación existente <see cref="PackagingLevelId"/>) o un Item nuevo del lote
/// (<see cref="NewItemKey"/>). Para un Item nuevo, la presentación que entrega el proveedor con ESTE
/// código se describe con <see cref="PresentationFactor"/> (1 = unidad base) y, si es mayor a 1,
/// <see cref="PresentationName"/>/<see cref="PresentationUomCode"/> — varios códigos del mismo
/// producto (unidad, caja x12…) apuntan al mismo <see cref="NewItemKey"/>.
/// </summary>
public sealed record ResolveReceptionLineInput(
    Guid PurchaseReceptionLineId,
    Guid? ItemId = null,
    string? NewItemKey = null,
    Guid? PackagingLevelId = null,
    decimal PresentationFactor = 1m,
    string? PresentationName = null,
    string? PresentationUomCode = null
);

/// <summary>
/// Resolución masiva de las líneas pendientes de UNA recepción XML (el documento se deduce de las
/// líneas; todas deben pertenecer al mismo): crea los Items nuevos (con sus
/// presentaciones), vincula cada línea y aprende la equivalencia Proveedor + Código → Item +
/// Presentación. Todo o nada: si cualquier fila es inválida no se persiste nada y se devuelven
/// todos los errores por fila (<see cref="ResolvePurchaseReceptionLinesResultDto.Applied"/> = false).
/// </summary>
public sealed record ResolvePurchaseReceptionLinesCommand(
    IReadOnlyList<ResolveReceptionNewItemInput> NewItems,
    IReadOnlyList<ResolveReceptionLineInput> Lines
) : IRequest<Result<ResolvePurchaseReceptionLinesResultDto>>, IBranchScopedRequest;

/// <summary>Error de una fila: línea XML y/o Item nuevo al que corresponde.</summary>
public sealed record ResolveReceptionRowError(
    Guid? PurchaseReceptionLineId,
    string? NewItemKey,
    string Message
);

/// <summary>Estado final de una línea resuelta — lo que el formulario de compra necesita aplicar.</summary>
public sealed record ResolvedReceptionLineDto(
    Guid PurchaseReceptionLineId,
    Guid ItemId,
    string ItemSku,
    string ItemName,
    string MatchStatus,
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor,
    bool EquivalenceLearned
);

public sealed record ResolvePurchaseReceptionLinesResultDto(
    bool Applied,
    IReadOnlyList<ResolveReceptionRowError> Errors,
    IReadOnlyList<ResolvedReceptionLineDto> Lines,
    int ItemsCreated,
    int LinesLinked,
    int EquivalencesLearned,
    int LinesAutoMatched
);
