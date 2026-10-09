namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Clientes — SSOT compartida
/// entre <c>CustomerImportProcessor</c> (Application, mapeo columna→DTO) y
/// <c>ClosedXmlCustomerImportSheetReader</c> (Infrastructure, lectura/escritura del .xlsx). Una
/// entidad <c>ImportTemplate</c> persistida es abstracción prematura mientras exista una única
/// plantilla hardcodeada por tipo — revisar cuando un import type necesite columnas configurables
/// por tenant.
/// </summary>
public static class CustomerImportColumns
{
    public const string IdentificationType = "Tipo Identificación";
    public const string IdentificationNumber = "Número Identificación";
    public const string LegalEntityTypeCode = "Tipo Entidad Legal";
    public const string LegalName = "Razón Social";
    public const string TradeName = "Nombre Comercial";
    public const string CountryCode = "País";
    public const string Email = "Email";
    public const string Phone = "Teléfono";
    public const string PaymentTermCode = "Condición de Pago";

    public static readonly IReadOnlyList<string> All =
    [
        IdentificationType,
        IdentificationNumber,
        LegalEntityTypeCode,
        LegalName,
        TradeName,
        CountryCode,
        Email,
        Phone,
        PaymentTermCode,
    ];

    /// <summary>
    /// IL-2A: columnas de la plantilla anterior que escribían clasificación comercial en el rol
    /// Cliente tenant-wide. Las condiciones comerciales son por Company — un archivo que todavía
    /// las trae se rechaza en vez de ignorarlas en silencio.
    /// </summary>
    public static readonly IReadOnlyList<string> Obsolete = ["Categoría", "Segmento", "Zona de Ventas"];
}
