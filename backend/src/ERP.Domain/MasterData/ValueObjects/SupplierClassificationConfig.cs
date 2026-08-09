namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Clasificación estratégica del proveedor. Solo existe cuando RoleType = Supplier.
///
/// SEPARACIÓN DE RESPONSABILIDADES:
///   SupplierRoleConfig            → datos SRI operativos (sustento, retención, método de pago)
///   SupplierClassificationConfig  → clasificación interna para gestión de proveedores
///
/// Los campos de esta VO son tenant-scoped y no afectan directamente la generación
/// de documentos SRI. Son para análisis, segmentación y gestión estratégica.
///
/// Almacenado en tabla master_bp_supplier_classification_configs (1:1 con master_bp_roles).
/// </summary>
public sealed record SupplierClassificationConfig
{
    // ── Longitudes máximas ────────────────────────────────────────────────────
    public const int CategoryMaxLen = 50;
    public const int TypeMaxLen = 30;
    public const int RiskMaxLen = 20;
    public const int RatingMaxLen = 10;
    public const int GoodTypeMaxLen = 30;
    public const int SegmentMaxLen = 30;
    public const int PaymentPrefMaxLen = 100;

    // ── Valores válidos: CLASS-BP-CATALOGS-01 — ya no viven como HashSet fijo en Domain, ver
    // catálogos persistidos tenant+company-scoped en ERP.Domain.MasterData.Entities
    // (SupplierCategory, SupplierType, SupplierRisk, SupplierRating, PrimaryGoodType,
    // SupplierSegment) y su validación async en UpdateSupplierClassificationConfigValidator
    // (Application layer, FluentValidation MustAsync). Un VO factory debe seguir siendo
    // síncrono/libre de efectos secundarios — Domain no puede hacer lookup async a BD.

    // ── Propiedades ───────────────────────────────────────────────────────────

    /// <summary>Tipo estratégico: Manufacturer | Distributor | ServiceProvider | Agent | Retailer | Other</summary>
    public string? SupplierCategory { get; }

    /// <summary>Origen: National | International | Both</summary>
    public string? SupplierType { get; }

    /// <summary>Riesgo operativo: Low | Medium | High | Critical</summary>
    public string? SupplierRisk { get; }

    /// <summary>Calificación de calidad/confiabilidad: AAA | AA | A | BBB | B | C | D | NR</summary>
    public string? SupplierRating { get; }

    /// <summary>Tipo de bien principal: Goods | Services | Both | Digital</summary>
    public string? PrimaryGoodType { get; }

    /// <summary>Segmento estratégico: Strategic | Preferred | Approved | Transactional</summary>
    public string? SupplierSegment { get; }

    /// <summary>Método de pago preferido operativo interno (texto libre, no código SRI).</summary>
    public string? PaymentMethodPreference { get; }

    private SupplierClassificationConfig(
        string? supplierCategory,
        string? supplierType,
        string? supplierRisk,
        string? supplierRating,
        string? primaryGoodType,
        string? supplierSegment,
        string? paymentMethodPreference
    )
    {
        SupplierCategory = supplierCategory;
        SupplierType = supplierType;
        SupplierRisk = supplierRisk;
        SupplierRating = supplierRating;
        PrimaryGoodType = primaryGoodType;
        SupplierSegment = supplierSegment;
        PaymentMethodPreference = paymentMethodPreference;
    }

    public static SupplierClassificationConfig Create(
        string? supplierCategory = null,
        string? supplierType = null,
        string? supplierRisk = null,
        string? supplierRating = null,
        string? primaryGoodType = null,
        string? supplierSegment = null,
        string? paymentMethodPreference = null
    )
    {
        return new SupplierClassificationConfig(
            NormalizeText(supplierCategory, CategoryMaxLen, nameof(supplierCategory)),
            NormalizeText(supplierType, TypeMaxLen, nameof(supplierType)),
            NormalizeText(supplierRisk, RiskMaxLen, nameof(supplierRisk)),
            NormalizeText(supplierRating, RatingMaxLen, nameof(supplierRating)),
            NormalizeText(primaryGoodType, GoodTypeMaxLen, nameof(primaryGoodType)),
            NormalizeText(supplierSegment, SegmentMaxLen, nameof(supplierSegment)),
            NormalizeText(
                paymentMethodPreference,
                PaymentPrefMaxLen,
                nameof(paymentMethodPreference)
            )
        );
    }

    private static string? NormalizeText(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v))
            return null;
        if (v.Length > maxLen)
            throw new ArgumentException(
                $"{paramName} no puede superar {maxLen} caracteres.",
                paramName
            );
        return v;
    }
}
