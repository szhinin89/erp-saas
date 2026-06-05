namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Configuración específica del rol Supplier. Solo existe cuando RoleType = Supplier.
///
/// Los códigos SRI son sugerencias de pre-llenado para documentos de compra.
/// Cada documento puede sobrescribirlos en el momento de emisión.
///
/// CAMPOS SRI OPERATIVOS (S3-A):
///   DefaultTaxSupportCode       — sustento tributario por defecto (01-19)
///   DefaultRetentionVatCode     — código retención IVA (ej: 725)
///   DefaultRetentionIncomeCode  — código retención Renta (ej: 303)
///   PaymentTerms                — plazo de pago (texto)
///   DefaultPaymentMethodCode    — método de pago SRI por defecto (01-21)
///   IsRetentionExempt           — exento de retención (RISE, microempresa, etc.)
///
/// Almacenado en tabla master_bp_supplier_configs (1:1 con master_bp_roles).
/// </summary>
public sealed record SupplierRoleConfig
{
    // ── Longitudes máximas ────────────────────────────────────────────────────
    public const int SriCodeMaxLen           = 5;
    public const int PaymentTermsMaxLen      = 200;
    public const int PaymentMethodCodeMaxLen = 5;

    /// <summary>
    /// Códigos de método de pago SRI válidos (global.sri_payment_method).
    /// 01=Sin sistema financiero, 15=Compensación, 16=Tarjeta débito,
    /// 17=Dinero electrónico, 18=Tarjeta prepago, 19=Tarjeta crédito,
    /// 20=Otros con sistema financiero, 21=Endoso de títulos.
    /// </summary>
    public static readonly IReadOnlySet<string> ValidPaymentMethodCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "01", "15", "16", "17", "18", "19", "20", "21" };

    public string? DefaultTaxSupportCode       { get; }
    public string? DefaultRetentionVatCode     { get; }
    public string? DefaultRetentionIncomeCode  { get; }
    public string? PaymentTerms                { get; }

    /// <summary>
    /// Método de pago SRI por defecto (código global.sri_payment_method).
    /// Pre-llenado en documentos de compra. NULL = sin preferencia.
    /// </summary>
    public string? DefaultPaymentMethodCode    { get; }

    /// <summary>
    /// Proveedor exento de retención en la fuente (RISE, microempresa calificada, sector público).
    /// Cuando true el proceso de generación de retención debe alertar al operador.
    /// Default: false.
    /// </summary>
    public bool    IsRetentionExempt           { get; }

    private SupplierRoleConfig(
        string? defaultTaxSupportCode,
        string? defaultRetentionVatCode,
        string? defaultRetentionIncomeCode,
        string? paymentTerms,
        string? defaultPaymentMethodCode,
        bool    isRetentionExempt)
    {
        DefaultTaxSupportCode      = defaultTaxSupportCode;
        DefaultRetentionVatCode    = defaultRetentionVatCode;
        DefaultRetentionIncomeCode = defaultRetentionIncomeCode;
        PaymentTerms               = paymentTerms;
        DefaultPaymentMethodCode   = defaultPaymentMethodCode;
        IsRetentionExempt          = isRetentionExempt;
    }

    public static SupplierRoleConfig Create(
        string? defaultTaxSupportCode      = null,
        string? defaultRetentionVatCode    = null,
        string? defaultRetentionIncomeCode = null,
        string? paymentTerms               = null,
        string? defaultPaymentMethodCode   = null,
        bool    isRetentionExempt          = false)
    {
        var paymentMethodCode = defaultPaymentMethodCode?.Trim();
        if (!string.IsNullOrEmpty(paymentMethodCode)
            && !ValidPaymentMethodCodes.Contains(paymentMethodCode))
            throw new ArgumentException(
                $"DefaultPaymentMethodCode '{paymentMethodCode}' no es un código SRI válido. " +
                $"Valores permitidos: {string.Join(", ", ValidPaymentMethodCodes)}.",
                nameof(defaultPaymentMethodCode));

        return new SupplierRoleConfig(
            NormalizeSriCode(defaultTaxSupportCode,      nameof(defaultTaxSupportCode)),
            NormalizeSriCode(defaultRetentionVatCode,    nameof(defaultRetentionVatCode)),
            NormalizeSriCode(defaultRetentionIncomeCode, nameof(defaultRetentionIncomeCode)),
            NormalizeText(paymentTerms, PaymentTermsMaxLen, nameof(paymentTerms)),
            string.IsNullOrEmpty(paymentMethodCode) ? null : paymentMethodCode,
            isRetentionExempt);
    }

    private static string? NormalizeSriCode(string? code, string paramName)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c)) return null;
        if (c.Length > SriCodeMaxLen)
            throw new ArgumentException($"{paramName} no puede superar {SriCodeMaxLen} caracteres.", paramName);
        return c;
    }

    private static string? NormalizeText(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }
}
