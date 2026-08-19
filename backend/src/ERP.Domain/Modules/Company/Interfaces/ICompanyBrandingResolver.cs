namespace ERP.Domain.Modules.Company.Interfaces;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: marca resuelta de la empresa — colores, eslogan, pie de página de
/// documentos, y ruta de almacenamiento del logo activo (resuelto desde MediaFile, no
/// duplicado aquí como binario/base64). Todos los campos son opcionales: una empresa sin marca
/// configurada es un estado válido, nunca un error.
/// </summary>
public sealed record CompanyBrandingSettings(
    string? LogoStoragePath,
    string? PrimaryColor,
    string? SecondaryColor,
    string? Slogan,
    string? DocumentFooterText
)
{
    /// <summary>Ausencia total de marca configurada — estado válido, no un error.</summary>
    public static CompanyBrandingSettings Empty() => new(null, null, null, null, null);
}

/// <summary>
/// Único punto de acceso a la marca resuelta de una empresa (logo + colores + eslogan + pie de
/// página de documentos). Compone <c>MediaFile</c> (logo) y <c>org_settings</c>
/// (<see cref="ERP.Domain.Configuration.Constants.OrgSettingKeys.CompanyBranding"/>) — ningún
/// otro módulo debe leer esas dos fuentes directamente para resolver la marca de la empresa.
/// Consumidores: Configuración → Empresa → Marca (vía los use cases de Companies) y RIDE/PDF
/// (vía <c>CompanyBrandingRideProvider</c>, que traduce este objeto a <c>RideBranding</c> — RIDE
/// nunca lee <c>org_settings</c> ni <c>MediaFile</c> directamente).
///
/// Fail-safe: un color corrupto/no-hex en <c>org_settings</c> se descarta (null) con warning en
/// log — nunca lanza, nunca produce un PDF con un color inválido. Un logo ausente es un estado
/// válido — RIDE/PDF debe renderizar sin logo, no fallar.
/// </summary>
public interface ICompanyBrandingResolver
{
    Task<CompanyBrandingSettings> GetAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );
}
