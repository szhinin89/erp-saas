namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Configuración específica del rol Supplier. Solo existe cuando RoleType = Supplier.
///
/// Los códigos SRI son sugerencias de pre-llenado para documentos de compra.
/// Cada documento puede sobrescribirlos en el momento de emisión.
///
/// CAMPOS SRI OPERATIVOS (S3-A):
///   DefaultTaxSupportCode       — sustento tributario por defecto (01-19)
///   PaymentTerms                — [LEGACY] texto libre, será eliminado
///   DefaultPaymentMethodCode    — método de pago SRI por defecto (01-21)
///   RefundProviderTypeCode      — Tipo Proveedor de Reembolso (global.sri_supplier_type: 01/02)
///   IsRetentionExempt           — exento de retención (RISE, microempresa, etc.)
///
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: los antiguos DefaultRetentionVatCode/
/// DefaultRetentionIncomeCode (un único código fijo por impuesto, tenant-wide) se eliminaron de
/// este VO. La lista dinámica de retenciones predeterminadas por proveedor+empresa vive ahora en
/// <see cref="ERP.Domain.MasterData.Entities.SupplierRetentionDefault"/> (N filas, FK real a
/// SriRetentionCode, scoped por Company) — ver ADR-033 para el precedente de este mismo patrón
/// aplicado a PaymentTermId.
///
/// Almacenado en tabla master_bp_supplier_configs (1:1 con master_bp_roles).
///
/// Este VO solo valida invariantes estructurales (trim, longitud máxima) — nunca contra
/// catálogos SRI (Domain no depende de EF Core / repositorios). La validación de pertenencia a
/// catálogo real (sri_tax_support, sri_retention_code, sri_payment_method, sri_supplier_type)
/// vive en Application: <c>UpdateSupplierRoleConfigValidator</c> (FluentValidation MustAsync).
/// </summary>
public sealed record SupplierRoleConfig
{
    // ── Longitudes máximas ────────────────────────────────────────────────────
    public const int SriCodeMaxLen = 5;
    public const int PaymentTermsMaxLen = 200;
    public const int PaymentMethodCodeMaxLen = 5;

    public string? DefaultTaxSupportCode { get; }

    /// <summary>[LEGACY] Texto libre — será eliminado.</summary>
    public string? PaymentTerms { get; }

    /// <summary>
    /// Método de pago SRI por defecto (código global.sri_payment_method).
    /// Pre-llenado en documentos de compra. NULL = sin preferencia.
    /// </summary>
    public string? DefaultPaymentMethodCode { get; }

    /// <summary>
    /// Tipo Proveedor de Reembolso (Tabla 26 Ficha Técnica SRI: 01=Persona Natural, 02=Sociedad),
    /// código de global.sri_supplier_type. NULL = sin clasificar.
    /// </summary>
    public string? RefundProviderTypeCode { get; }

    /// <summary>
    /// Proveedor exento de retención en la fuente (RISE, microempresa calificada, sector público).
    /// Cuando true el proceso de generación de retención debe alertar al operador.
    /// Default: false.
    /// </summary>
    public bool IsRetentionExempt { get; }

    /// <summary>
    /// Obligado a llevar contabilidad según SRI Ecuador.
    /// Afecta porcentajes de retención (IVA 30% vs 70%, Renta 1% vs 2%).
    /// Requerido para ATS y cálculo de retenciones.
    /// </summary>
    public bool IsRequiredToKeepAccounting { get; }

    private SupplierRoleConfig(
        string? defaultTaxSupportCode,
        string? paymentTerms,
        string? defaultPaymentMethodCode,
        string? refundProviderTypeCode,
        bool isRetentionExempt,
        bool isRequiredToKeepAccounting
    )
    {
        DefaultTaxSupportCode = defaultTaxSupportCode;
        PaymentTerms = paymentTerms;
        DefaultPaymentMethodCode = defaultPaymentMethodCode;
        RefundProviderTypeCode = refundProviderTypeCode;
        IsRetentionExempt = isRetentionExempt;
        IsRequiredToKeepAccounting = isRequiredToKeepAccounting;
    }

    public static SupplierRoleConfig Create(
        string? defaultTaxSupportCode = null,
        string? paymentTerms = null,
        string? defaultPaymentMethodCode = null,
        string? refundProviderTypeCode = null,
        bool isRetentionExempt = false,
        bool isRequiredToKeepAccounting = false
    )
    {
        return new SupplierRoleConfig(
            NormalizeSriCode(defaultTaxSupportCode, nameof(defaultTaxSupportCode)),
            NormalizeText(paymentTerms, PaymentTermsMaxLen, nameof(paymentTerms)),
            NormalizeSriCode(defaultPaymentMethodCode, nameof(defaultPaymentMethodCode)),
            NormalizeSriCode(refundProviderTypeCode, nameof(refundProviderTypeCode)),
            isRetentionExempt,
            isRequiredToKeepAccounting
        );
    }

    private static string? NormalizeSriCode(string? code, string paramName)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c))
            return null;
        if (c.Length > SriCodeMaxLen)
            throw new ArgumentException(
                $"{paramName} no puede superar {SriCodeMaxLen} caracteres.",
                paramName
            );
        return c;
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
