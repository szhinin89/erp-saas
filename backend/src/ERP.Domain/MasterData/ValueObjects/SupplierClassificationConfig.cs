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

    // ── Valores válidos ───────────────────────────────────────────────────────

    /// <summary>Tipo estratégico del proveedor.</summary>
    public static readonly IReadOnlySet<string> ValidCategories = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Manufacturer",
        "Distributor",
        "ServiceProvider",
        "Agent",
        "Retailer",
        "Other",
    };

    /// <summary>Origen geográfico del proveedor.</summary>
    public static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "National",
        "International",
        "Both",
    };

    /// <summary>Nivel de riesgo operativo y comercial.</summary>
    public static readonly IReadOnlySet<string> ValidRisks = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Low",
        "Medium",
        "High",
        "Critical",
    };

    /// <summary>Calificación de calidad y confiabilidad del proveedor.</summary>
    public static readonly IReadOnlySet<string> ValidRatings = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "AAA",
        "AA",
        "A",
        "BBB",
        "BB",
        "B",
        "C",
        "D",
        "NR",
    };

    /// <summary>Tipo de bien o servicio principal que provee.</summary>
    public static readonly IReadOnlySet<string> ValidGoodTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Goods",
        "Services",
        "Both",
        "Digital",
    };

    /// <summary>Segmento estratégico para gestión de proveedores.</summary>
    public static readonly IReadOnlySet<string> ValidSegments = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Strategic",
        "Preferred",
        "Approved",
        "Transactional",
    };

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
            ValidateEnum(
                supplierCategory,
                ValidCategories,
                CategoryMaxLen,
                nameof(supplierCategory)
            ),
            ValidateEnum(supplierType, ValidTypes, TypeMaxLen, nameof(supplierType)),
            ValidateEnum(supplierRisk, ValidRisks, RiskMaxLen, nameof(supplierRisk)),
            ValidateEnum(supplierRating, ValidRatings, RatingMaxLen, nameof(supplierRating)),
            ValidateEnum(primaryGoodType, ValidGoodTypes, GoodTypeMaxLen, nameof(primaryGoodType)),
            ValidateEnum(supplierSegment, ValidSegments, SegmentMaxLen, nameof(supplierSegment)),
            NormalizeText(
                paymentMethodPreference,
                PaymentPrefMaxLen,
                nameof(paymentMethodPreference)
            )
        );
    }

    private static string? ValidateEnum(
        string? value,
        IReadOnlySet<string> validValues,
        int maxLen,
        string paramName
    )
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v))
            return null;
        if (v.Length > maxLen)
            throw new ArgumentException(
                $"{paramName} no puede superar {maxLen} caracteres.",
                paramName
            );
        if (!validValues.Contains(v))
            throw new ArgumentException(
                $"{paramName} '{v}' no es un valor válido. Permitidos: {string.Join(", ", validValues)}.",
                paramName
            );
        return v;
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
