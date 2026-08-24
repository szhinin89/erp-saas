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
    public const string LegalName = "Razón Social";
    public const string TradeName = "Nombre Comercial";
    public const string CountryCode = "País";
    public const string Email = "Email";
    public const string Phone = "Teléfono";
    public const string CustomerCategory = "Categoría";
    public const string CustomerSegment = "Segmento";
    public const string SalesZone = "Zona de Ventas";
    public const string CreditLimit = "Límite de Crédito";
    public const string PaymentDays = "Días de Pago";

    public static readonly IReadOnlyList<string> All =
    [
        IdentificationType,
        IdentificationNumber,
        LegalName,
        TradeName,
        CountryCode,
        Email,
        Phone,
        CustomerCategory,
        CustomerSegment,
        SalesZone,
        CreditLimit,
        PaymentDays,
    ];
}
