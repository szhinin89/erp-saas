using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4) — responsabilidad de la empresa de aplicar un impuesto
/// especial (ICE, IRBPNR) en sus propias ventas. Excepción acotada y documentada a la infraestructura
/// FROZEN "Configuración Tributaria" (docs/architecture/frozen-infrastructure.md § Excepción acotada
/// — CompanySpecialTaxResponsibility): NO es un catálogo tributario por empresa — no tiene tarifa, no
/// tiene código de catálogo, no reemplaza <c>SriIceRate</c>/<c>SriIrbpnrRate</c>. Es exclusivamente un
/// booleano que responde "¿esta empresa es sujeto pasivo de este impuesto especial al vender?" — una
/// realidad fiscal real del SRI (fabricante/importador vs. revendedor). Nunca participa en Compras.
/// Sin fila (o <see cref="IsResponsibleOnSales"/> = false) para un <c>SriTaxCategoryCode</c> dado = la
/// empresa NO aplica ese impuesto en ventas (comportamiento por defecto).
/// </summary>
public sealed class CompanySpecialTaxResponsibility : IMustHaveTenant
{
    public const int SriTaxCategoryCodeMaxLen = 10;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "3" ICE, "5" IRBPNR (ERP.Domain.Modules.Purchases.SriTaxCategoryCodes).</summary>
    public string SriTaxCategoryCode { get; private set; } = null!;
    public bool IsResponsibleOnSales { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private CompanySpecialTaxResponsibility() { }

    public static CompanySpecialTaxResponsibility Create(
        Guid companyId,
        Guid tenantId,
        string sriTaxCategoryCode,
        bool isResponsibleOnSales,
        Guid? updatedBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(sriTaxCategoryCode))
            throw new ArgumentException(
                "El código de categoría SRI es obligatorio.",
                nameof(sriTaxCategoryCode)
            );

        return new CompanySpecialTaxResponsibility
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            SriTaxCategoryCode = sriTaxCategoryCode.Trim(),
            IsResponsibleOnSales = isResponsibleOnSales,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy,
        };
    }

    public void SetResponsibility(bool isResponsibleOnSales, Guid? updatedBy)
    {
        IsResponsibleOnSales = isResponsibleOnSales;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Disable(Guid? updatedBy)
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
