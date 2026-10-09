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
    public const string IdentificationType = PartnerImportColumns.IdentificationType;
    public const string IdentificationNumber = PartnerImportColumns.IdentificationNumber;
    public const string LegalEntityTypeCode = PartnerImportColumns.LegalEntityTypeCode;
    public const string LegalName = PartnerImportColumns.LegalName;
    public const string TradeName = PartnerImportColumns.TradeName;
    public const string CountryCode = PartnerImportColumns.CountryCode;
    public const string Email = PartnerImportColumns.Email;
    public const string Phone = PartnerImportColumns.Phone;
    public const string PaymentTermCode = PartnerImportColumns.PaymentTermCode;

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
