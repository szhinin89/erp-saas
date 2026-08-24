namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Catálogo de Productos — SSOT
/// compartida entre <c>ItemImportProcessor</c> y <c>ClosedXmlItemImportSheetReader</c>. Rediseño
/// "importación inteligente" (segunda vuelta de INITIAL-LOAD-ITEMS-01): una sola hoja plana con
/// columnas anchas — nunca varias hojas relacionadas — donde cada fila representa un producto
/// principal y el backend resuelve/crea internamente Categoría, Marca, códigos de barras,
/// vínculo a proveedor y PVP.
/// </summary>
public static class ItemImportColumns
{
    public const string Sku = "SKU";
    public const string Name = "Nombre";
    public const string ItemTypeCode = "Tipo de Ítem";
    public const string UomCode = "Unidad Base";
    public const string VatCode = "IVA";
    public const string CategoryName = "Categoría";
    public const string BrandName = "Marca";
    public const string Barcode1 = "Código Barra 1";
    public const string Barcode2 = "Código Barra 2";
    public const string Barcode3 = "Código Barra 3";
    public const string Pvp = "PVP";
    public const string AvailableOnPos = "Disponible POS";
    public const string SupplierQuery = "Proveedor";
    public const string SupplierItemCode = "Código Proveedor";
    public const string Cost = "Costo";
    public const string Observations = "Observaciones";

    public static readonly IReadOnlyList<string> All =
    [
        Sku,
        Name,
        ItemTypeCode,
        UomCode,
        VatCode,
        CategoryName,
        BrandName,
        Barcode1,
        Barcode2,
        Barcode3,
        Pvp,
        AvailableOnPos,
        SupplierQuery,
        SupplierItemCode,
        Cost,
        Observations,
    ];
}
