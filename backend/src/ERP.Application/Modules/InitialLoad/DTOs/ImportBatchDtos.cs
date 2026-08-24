using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.DTOs;

public sealed record ImportBatchDto(
    Guid Id,
    ImportType ImportType,
    ImportStatus Status,
    string? Label,
    bool AutoCreateCatalogValues,
    int TotalRows,
    int ValidRows,
    int IssueRows,
    int WarningRows,
    int ImportedRows,
    DateTime? ValidatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? FailureReason,
    DateTime CreatedAt
)
{
    public static ImportBatchDto From(ImportBatch batch) =>
        new(
            batch.Id,
            batch.ImportType,
            batch.Status,
            batch.Label,
            batch.AutoCreateCatalogValues,
            batch.TotalRows,
            batch.ValidRows,
            batch.IssueRows,
            batch.WarningRows,
            batch.ImportedRows,
            batch.ValidatedAt,
            batch.ConfirmedAt,
            batch.CancelledAt,
            batch.FailureReason,
            batch.CreatedAt
        );
}

public sealed record ImportBatchIssueDto(
    Guid Id,
    int RowNumber,
    string? FieldName,
    ImportSeverity Severity,
    string Code,
    string Message
)
{
    public static ImportBatchIssueDto From(ImportBatchIssue issue) =>
        new(issue.Id, issue.RowNumber, issue.FieldName, issue.Severity, issue.Code, issue.Message);
}

public sealed record ImportBatchRowPreviewDto(
    Guid Id,
    int RowNumber,
    bool HasBlockingIssue,
    bool IsImported,
    Guid? CreatedBusinessPartnerId,
    IReadOnlyDictionary<string, string?> RawData,
    IReadOnlyList<ImportBatchIssueDto> Issues
);

public sealed record ImportBatchConfirmResultDto(
    Guid ImportBatchId,
    ImportStatus Status,
    int ImportedRows,
    int FailedRows
);

public sealed record ImportTemplateFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>Fila cruda leída del Excel por un <c>IImportProcessor</c>, keyed por encabezado de columna.</summary>
public sealed record ImportReadResult(
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows
);

public sealed record RowValidationResult(
    string ParsedDataJson,
    bool HasBlockingIssue,
    IReadOnlyList<RowIssue> Issues
);

public sealed record RowIssue(ImportSeverity Severity, string Code, string Message, string? FieldName = null);

public sealed record RowConfirmResult(bool IsSuccess, Guid? BusinessPartnerId, string? Error)
{
    public static RowConfirmResult Success(Guid businessPartnerId) => new(true, businessPartnerId, null);

    public static RowConfirmResult Failed(string error) => new(false, null, error);
}

/// <summary>Fila de Clientes ya tipada — resultado del mapeo de <c>CustomerImportProcessor</c>.</summary>
public sealed record ParsedCustomerRow(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? CountryCode,
    string? Email,
    string? Phone,
    string? CustomerCategory,
    string? CustomerSegment,
    string? SalesZone,
    decimal? CreditLimit,
    int? PaymentDays
);

/// <summary>
/// Fila de Proveedores ya tipada — resultado del mapeo de <c>SupplierImportProcessor</c>.
/// <see cref="PaymentTermId"/> ya viene resuelto (código de la plantilla → FK) desde
/// ValidateRowAsync — Confirm nunca vuelve a resolverlo, evita una segunda consulta y mantiene
/// el mismo comportamiento de "lo que se validó es lo que se confirma" que usa Clientes.
/// </summary>
public sealed record ParsedSupplierRow(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? CountryCode,
    string? Email,
    string? Phone,
    Guid PaymentTermId,
    string? SupplierCategory,
    string? SupplierType,
    string? PrimaryGoodType,
    string? SupplierSegment
);

/// <summary>
/// Fila de Catálogo de Productos ya tipada — resultado del mapeo de <c>ItemImportProcessor</c>
/// (rediseño "importación inteligente" sobre INITIAL-LOAD-ITEMS-01).
///
/// A diferencia de Clientes/Proveedores, <see cref="CategoryName"/>/<see cref="BrandName"/> NO
/// vienen resueltos a Guid desde Validate — la resolución (y la posible creación automática si
/// <c>ImportBatch.AutoCreateCatalogValues</c> está activo) se hace en <c>ConfirmRowAsync</c>, para
/// no escribir catálogo real (Categoría/Marca) hasta que el usuario confirme el lote. ItemTypeId
/// sí viene resuelto porque los tipos de ítem son un catálogo cerrado que Validate solo puede
/// leer, nunca crear.
///
/// <see cref="IsAvailableOnPOS"/> ya incorpora la regla "sin precio (o precio inválido) → no
/// disponible en POS": el processor la fuerza a false en Validate cuando
/// <see cref="BaseSalePrice"/> es null, independientemente de lo que diga la columna de la
/// plantilla. <see cref="SupplierId"/> ya viene resuelto (o null si no se pudo vincular de forma
/// inequívoca) porque resolver un proveedor es una lectura pura, sin efectos secundarios.
/// </summary>
public sealed record ParsedItemRow(
    string SKU,
    string ShortName,
    string Description,
    Guid ItemTypeId,
    string DefaultUomCode,
    string CategoryName,
    string BrandName,
    IReadOnlyList<string> BarcodeCodes,
    string? SaleVatCode,
    decimal? BaseSalePrice,
    bool IsAvailableOnPOS,
    Guid? SupplierId,
    string? SupplierItemCode,
    string? Observations
);

/// <summary>
/// Fila de Stock Inicial ya tipada — resultado del mapeo de <c>InitialStockImportProcessor</c>
/// (INITIAL-LOAD-INITIAL-STOCK-01). ItemId/WarehouseId ya vienen resueltos desde Validate — a
/// diferencia de Categoría/Marca en Catálogo de Productos, ítems y bodegas NUNCA se crean desde
/// este importador, así que no hay nada que diferir a Confirm: si no existen, la fila ya está
/// bloqueada antes de llegar aquí. <see cref="BaseUomCode"/> viene del ítem resuelto — el stock
/// inicial siempre se carga en la unidad base, sin presentaciones/empaques.
/// </summary>
public sealed record ParsedInitialStockRow(
    Guid ItemId,
    string ItemName,
    string BaseUomCode,
    Guid WarehouseId,
    string WarehouseName,
    decimal Quantity,
    decimal UnitCost,
    string? Observation
);
