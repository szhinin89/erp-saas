namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de Proveedores — SSOT compartida
/// entre <c>SupplierImportProcessor</c> y <c>ClosedXmlSupplierImportSheetReader</c>. IL-3A: los
/// datos fiscales del rol Proveedor que alimentan retenciones son obligatorios y explícitos (SI/NO),
/// sin default. Solo 04 (RUC) y 08 (Exterior) — lo que el catálogo SRI permite para Proveedor.
/// </summary>
public static class SupplierImportColumns
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
    public const string IsRequiredToKeepAccounting = "Obligado a llevar contabilidad";
    public const string IsRetentionExempt = "Exento de retención";

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
        IsRequiredToKeepAccounting,
        IsRetentionExempt,
    ];
}
