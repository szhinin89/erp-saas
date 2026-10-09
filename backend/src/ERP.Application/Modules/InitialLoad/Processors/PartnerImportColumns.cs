namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Encabezados comunes de las plantillas de terceros (Clientes/Proveedores) — SSOT de los nombres
/// que leen <see cref="BusinessPartnerImportRules"/> y los readers de Infrastructure.
/// </summary>
public static class PartnerImportColumns
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
}
