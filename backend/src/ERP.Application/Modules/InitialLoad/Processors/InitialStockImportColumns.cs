namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Stock Inicial — SSOT
/// compartida entre <c>InitialStockImportProcessor</c> y
/// <c>ClosedXmlInitialStockImportSheetReader</c>. Archivo separado del Catálogo de Productos a
/// propósito (INITIAL-LOAD-INITIAL-STOCK-01): afecta inventario/Kardex/costo, nunca crea ítems ni
/// bodegas — solo referencia los que ya existen.
/// </summary>
public static class InitialStockImportColumns
{
    public const string Sku = "SKU";
    public const string Barcode = "Código de barras";
    public const string Warehouse = "Bodega";
    public const string Quantity = "Cantidad";
    public const string UnitCost = "Costo unitario";
    public const string CutoffDate = "Fecha de corte";
    public const string Observation = "Observación";

    public static readonly IReadOnlyList<string> All =
    [
        Sku,
        Barcode,
        Warehouse,
        Quantity,
        UnitCost,
        CutoffDate,
        Observation,
    ];
}
