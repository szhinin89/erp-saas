namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Proveedores — SSOT
/// compartida entre <c>SupplierImportProcessor</c> (Application, mapeo columna→DTO) y
/// <c>ClosedXmlSupplierImportSheetReader</c> (Infrastructure, lectura/escritura del .xlsx).
/// Mismo patrón que <c>CustomerImportColumns</c> (INITIAL-LOAD-ARCH-01).
///
/// Alcance reducido a propósito (INITIAL-LOAD-SUPPLIERS-01): de <c>SupplierRoleConfig</c> solo
/// se importa la condición de pago (único campo obligatorio de esa VO); los campos SRI
/// operativos avanzados (sustento tributario, retención, método de pago SRI) quedan fuera de
/// esta plantilla — se completan luego desde la ficha del proveedor, igual que Clientes dejó
/// fuera CreditRating/LoyaltyTier/InvoiceFormat/Classification.
/// </summary>
public static class SupplierImportColumns
{
    public const string IdentificationType = "Tipo Identificación";
    public const string IdentificationNumber = "Número Identificación";
    public const string LegalName = "Razón Social";
    public const string TradeName = "Nombre Comercial";
    public const string CountryCode = "País";
    public const string Email = "Email";
    public const string Phone = "Teléfono";
    public const string PaymentTermCode = "Condición de Pago";
    public const string SupplierCategory = "Categoría";
    public const string SupplierType = "Tipo";
    public const string PrimaryGoodType = "Bien Principal";
    public const string SupplierSegment = "Segmento";

    public static readonly IReadOnlyList<string> All =
    [
        IdentificationType,
        IdentificationNumber,
        LegalName,
        TradeName,
        CountryCode,
        Email,
        Phone,
        PaymentTermCode,
        SupplierCategory,
        SupplierType,
        PrimaryGoodType,
        SupplierSegment,
    ];
}
