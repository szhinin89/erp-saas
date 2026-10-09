namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Inventario Inicial — SSOT
/// compartida entre <c>InitialStockImportProcessor</c> y <c>ClosedXmlInitialStockImportSheetReader</c>.
/// IL-4A: la bodega se identifica por CÓDIGO dentro de la sucursal activa (el nombre no es único
/// entre sucursales) y la Fecha de corte es obligatoria — es la fecha efectiva del saldo inicial.
/// </summary>
public static class InitialStockImportColumns
{
    public const string Sku = "SKU";
    public const string Barcode = "Código de barras";
    public const string WarehouseCode = "Código Bodega";
    public const string Quantity = "Cantidad";
    public const string UnitCost = "Costo unitario";
    public const string CutoffDate = "Fecha de corte";
    public const string Observation = "Observación";

    public static readonly IReadOnlyList<string> All =
    [
        Sku,
        Barcode,
        WarehouseCode,
        Quantity,
        UnitCost,
        CutoffDate,
        Observation,
    ];

    /// <summary>La plantilla anterior identificaba la bodega por nombre (ambiguo): se rechaza.</summary>
    public static readonly IReadOnlyList<string> Obsolete = ["Bodega"];
}
