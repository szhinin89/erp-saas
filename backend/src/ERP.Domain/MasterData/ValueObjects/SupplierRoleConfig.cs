namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Configuración específica del rol Supplier. Solo existe cuando RoleType = Supplier.
///
/// Los códigos SRI son sugerencias de pre-llenado para documentos de compra.
/// Cada documento puede sobrescribirlos en el momento de emisión.
/// Almacenado en tabla master_bp_supplier_configs (1:1 con master_bp_roles).
/// </summary>
public sealed record SupplierRoleConfig
{
    public const int SriCodeMaxLen    = 5;
    public const int PaymentTermsMaxLen = 200;

    public string? DefaultTaxSupportCode       { get; }
    public string? DefaultRetentionVatCode     { get; }
    public string? DefaultRetentionIncomeCode  { get; }
    public string? PaymentTerms                { get; }

    private SupplierRoleConfig(
        string? defaultTaxSupportCode,
        string? defaultRetentionVatCode,
        string? defaultRetentionIncomeCode,
        string? paymentTerms)
    {
        DefaultTaxSupportCode      = defaultTaxSupportCode;
        DefaultRetentionVatCode    = defaultRetentionVatCode;
        DefaultRetentionIncomeCode = defaultRetentionIncomeCode;
        PaymentTerms               = paymentTerms;
    }

    public static SupplierRoleConfig Create(
        string? defaultTaxSupportCode      = null,
        string? defaultRetentionVatCode    = null,
        string? defaultRetentionIncomeCode = null,
        string? paymentTerms               = null)
    {
        return new SupplierRoleConfig(
            NormalizeSriCode(defaultTaxSupportCode,      nameof(defaultTaxSupportCode)),
            NormalizeSriCode(defaultRetentionVatCode,    nameof(defaultRetentionVatCode)),
            NormalizeSriCode(defaultRetentionIncomeCode, nameof(defaultRetentionIncomeCode)),
            NormalizeText(paymentTerms, PaymentTermsMaxLen, nameof(paymentTerms)));
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
