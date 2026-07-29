namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Configuración específica del rol Customer. Solo existe cuando RoleType = Customer.
///
/// Contiene atributos de clasificación, segmentación y scoring del cliente.
/// Todos los campos son tenant-scoped — describen al cliente como tercero,
/// independientemente de qué empresa del tenant lo esté consultando.
///
/// SEPARACIÓN DE RESPONSABILIDADES:
///   CustomerCategory / Segment / Zone   → clasificación para CRM y analítica
///   CreditRating / LoyaltyTier          → scoring y fidelización
///   PreferredInvoiceFormat              → automatización documental
///   CustomerClassification              → clasificación tributaria/comercial Ecuador
///
///   CreditLimit / PaymentDays / IsBlocked → CompanyBpTradingSettings (company-scoped)
///   Email / Phone → BusinessPartnerContact
///   LegalName     → BusinessPartner
///
/// Almacenado en tabla master_bp_customer_configs (1:1 con master_bp_roles).
/// </summary>
public sealed record CustomerRoleConfig
{
    // ── Longitudes máximas ────────────────────────────────────────────────────
    public const int CategoryMaxLen = 50;
    public const int SegmentMaxLen = 50;
    public const int SalesZoneMaxLen = 100;
    public const int CreditRatingMaxLen = 10;
    public const int LoyaltyTierMaxLen = 20;
    public const int InvoiceFormatMaxLen = 20;
    public const int ClassificationMaxLen = 50;

    // ── Valores válidos (validación en Application layer vía FluentValidation) ─
    public static readonly IReadOnlySet<string> ValidCategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Retail", "Wholesale", "Corporate", "Government", "VIP", "Other" };

    public static readonly IReadOnlySet<string> ValidSegments =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Individual", "SMB", "MidMarket", "Enterprise", "StartUp" };

    public static readonly IReadOnlySet<string> ValidCreditRatings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AAA", "AA", "A", "BBB", "BB", "B", "C", "D", "NR" };

    public static readonly IReadOnlySet<string> ValidLoyaltyTiers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "None", "Bronze", "Silver", "Gold", "Platinum" };

    public static readonly IReadOnlySet<string> ValidInvoiceFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "PDF", "XML", "EMAIL", "PORTAL", "PAPER" };

    // ── Propiedades ───────────────────────────────────────────────────────────

    /// <summary>
    /// Clasificación del tipo de cliente. Uso: CRM, segmentación, reportes.
    /// Valores: Retail | Wholesale | Corporate | Government | VIP | Other
    /// </summary>
    public string? CustomerCategory { get; }

    /// <summary>
    /// Segmento de mercado. Uso: funnels, campañas, análisis comercial.
    /// Valores: Individual | SMB | MidMarket | Enterprise | StartUp
    /// </summary>
    public string? CustomerSegment { get; }

    /// <summary>
    /// Territorio de ventas asignado. Uso: rutas, vendedores, cobertura comercial.
    /// Texto libre: "Norte", "Sur", "Centro", "Amazonía", código de zona, etc.
    /// </summary>
    public string? SalesZone { get; }

    /// <summary>
    /// Calificación de riesgo crediticio interno. Uso: scoring, riesgo.
    /// NO reemplaza CompanyBpTradingSettings.CreditLimit — ese es el límite operativo.
    /// Valores: AAA | AA | A | BBB | BB | B | C | D | NR (no rated)
    /// </summary>
    public string? CreditRating { get; }

    /// <summary>
    /// Nivel del programa de fidelización. Uso: beneficios, campañas, retención.
    /// Valores: None | Bronze | Silver | Gold | Platinum
    /// </summary>
    public string? LoyaltyTier { get; }

    /// <summary>
    /// Formato preferido para recepción de documentos. Uso: automatización documental.
    /// Valores: PDF | XML | EMAIL | PORTAL | PAPER
    /// </summary>
    public string? PreferredInvoiceFormat { get; }

    /// <summary>
    /// Clasificación tributaria/comercial. Uso: analítica, tributación, reportes SRI Ecuador.
    /// Valores: Nacional | Exportador | Importador | Exento | Especial
    /// </summary>
    public string? CustomerClassification { get; }

    private CustomerRoleConfig(
        string? customerCategory,
        string? customerSegment,
        string? salesZone,
        string? creditRating,
        string? loyaltyTier,
        string? preferredInvoiceFormat,
        string? customerClassification)
    {
        CustomerCategory = customerCategory;
        CustomerSegment = customerSegment;
        SalesZone = salesZone;
        CreditRating = creditRating;
        LoyaltyTier = loyaltyTier;
        PreferredInvoiceFormat = preferredInvoiceFormat;
        CustomerClassification = customerClassification;
    }

    public static CustomerRoleConfig Create(
        string? customerCategory = null,
        string? customerSegment = null,
        string? salesZone = null,
        string? creditRating = null,
        string? loyaltyTier = null,
        string? preferredInvoiceFormat = null,
        string? customerClassification = null)
    {
        return new CustomerRoleConfig(
            Normalize(customerCategory, CategoryMaxLen, nameof(customerCategory)),
            Normalize(customerSegment, SegmentMaxLen, nameof(customerSegment)),
            Normalize(salesZone, SalesZoneMaxLen, nameof(salesZone)),
            Normalize(creditRating, CreditRatingMaxLen, nameof(creditRating)),
            Normalize(loyaltyTier, LoyaltyTierMaxLen, nameof(loyaltyTier)),
            Normalize(preferredInvoiceFormat, InvoiceFormatMaxLen, nameof(preferredInvoiceFormat)),
            Normalize(customerClassification, ClassificationMaxLen, nameof(customerClassification)));
    }

    private static string? Normalize(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }
}
