namespace ERP.Domain.Modules.Company.Interfaces;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/Subfase 5B) — lectura de
/// <c>CompanySpecialTaxResponsibility</c>. Distinta de <see cref="ICompanyRepository"/> (que
/// administra el agregado <c>Company</c>): esta es una tabla satélite de responsabilidad tributaria,
/// nunca un catálogo ni un dato tributario en sí (ver excepción documentada en
/// docs/architecture/frozen-infrastructure.md § Configuración Tributaria).
/// </summary>
public interface ICompanySpecialTaxResponsibilityRepository
{
    /// <summary>
    /// Códigos SRI de categoría de impuesto especial (ICE="3", IRBPNR="5") para los que la empresa
    /// está activa y marcada como responsable de aplicar en ventas. Vacío = la empresa no aplica
    /// ningún impuesto especial en ventas (comportamiento por defecto).
    /// </summary>
    Task<IReadOnlyCollection<string>> GetResponsibleSriTaxCategoryCodesAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
}
