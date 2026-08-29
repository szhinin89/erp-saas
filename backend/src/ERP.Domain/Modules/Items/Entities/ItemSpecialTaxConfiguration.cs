using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2) — configuración tributaria del ítem para impuestos
/// especiales (ICE, IRBPNR y cualquier impuesto especial SRI futuro), como colección 1:N en vez de
/// una columna fija por impuesto en <see cref="ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig"/>
/// (que se mantiene sin cambios, exclusiva de IVA). Sin fila activa para un
/// <see cref="SriTaxCategoryCode"/> dado = el ítem no está gravado con ese impuesto — nunca se asume
/// ni se inventa (docs/architecture/frozen-infrastructure.md § Configuración Tributaria, Regla 3).
/// </summary>
public sealed class ItemSpecialTaxConfiguration : AuditableEntity
{
    public const int SriTaxCategoryCodeMaxLen = 10;
    public const int TaxCatalogCodeMaxLen = 10;

    public Guid ItemId { get; private set; }

    /// <summary>Código SRI &lt;impuesto&gt;/&lt;codigo&gt; — "3" ICE, "5" IRBPNR (ERP.Domain.Modules.Purchases.SriTaxCategoryCodes).</summary>
    public string SriTaxCategoryCode { get; private set; } = null!;

    /// <summary>Código de tarifa dentro del catálogo de ese impuesto (SriIceRate.Code / SriIrbpnrRate.Code).</summary>
    public string TaxCatalogCode { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private ItemSpecialTaxConfiguration() { }

    public static ItemSpecialTaxConfiguration Create(
        Guid itemId,
        Guid tenantId,
        string sriTaxCategoryCode,
        string taxCatalogCode,
        Guid createdBy
    )
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("El ítem es obligatorio.", nameof(itemId));
        if (string.IsNullOrWhiteSpace(sriTaxCategoryCode))
            throw new ArgumentException(
                "El código de categoría SRI es obligatorio.",
                nameof(sriTaxCategoryCode)
            );
        if (string.IsNullOrWhiteSpace(taxCatalogCode))
            throw new ArgumentException(
                "El código de tarifa del catálogo es obligatorio.",
                nameof(taxCatalogCode)
            );

        var entity = new ItemSpecialTaxConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            SriTaxCategoryCode = sriTaxCategoryCode.Trim(),
            TaxCatalogCode = taxCatalogCode.Trim(),
            IsActive = true,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Disable(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}
