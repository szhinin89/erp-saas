using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Rol "proveedor" de un BusinessPartner dentro de un Subscriber.
///
/// SCOPE: ISubscriberScopedEntity.
/// El perfil de proveedor es COMPARTIDO entre todas las Companies del Subscriber.
///
/// DECISIÓN ARQUITECTÓNICA FINAL:
///   SupplierProfile NO es ICompanyOperationalEntity.
///   La entidad legacy Supplier permanece sin cambio en Purchasing.
///   Ver docs/arch/BUSINESSPARTNER-ADR.md — Decisión D3, Opción A.
///
/// Campos específicos del rol proveedor:
///   TaxSupportCode — código SRI de sustento tributario (ej. "01", "04")
///   PaymentTerms   — condición de pago estándar del proveedor
///
/// Las condiciones comerciales por empresa van en CompanyBusinessPartnerSettings.
/// </summary>
public sealed class SupplierProfile : AuditableEntity, ISubscriberScopedEntity
{
    public const int TaxSupportCodeMaxLen        = 10;
    public const int RetentionVatCodeMaxLen      = 10;
    public const int RetentionIncomeCodeMaxLen   = 10;
    public const int PaymentTermsMaxLen          = 30;

    public Guid    SubscriberId      { get; private set; }
    public Guid    BusinessPartnerId { get; private set; }
    public bool    IsActive          { get; private set; } = true;

    /// <summary>Default SRI: código de sustento tributario (overrideable por transacción).</summary>
    public string? DefaultTaxSupportCode { get; private set; }

    /// <summary>Default SRI: código retención IVA (overrideable por transacción).</summary>
    public string? DefaultRetentionVatCode { get; private set; }

    /// <summary>Default SRI: código retención renta (overrideable por transacción).</summary>
    public string? DefaultRetentionIncomeCode { get; private set; }

    /// <summary>Condición de pago acordada con el proveedor (ej. "30D", "COD").</summary>
    public string? PaymentTerms { get; private set; }

    // Navegación
    public BusinessPartner? BusinessPartner { get; private set; }

    private SupplierProfile() { }

    public static SupplierProfile Create(
        Guid    subscriberId,
        Guid    businessPartnerId,
        Guid    createdBy,
        string? defaultTaxSupportCode = null,
        string? defaultRetentionVatCode = null,
        string? defaultRetentionIncomeCode = null,
        string? paymentTerms = null)
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var p = new SupplierProfile
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            BusinessPartnerId = businessPartnerId,
            IsActive          = true,
            DefaultTaxSupportCode      = Trim(defaultTaxSupportCode, TaxSupportCodeMaxLen),
            DefaultRetentionVatCode    = Trim(defaultRetentionVatCode, RetentionVatCodeMaxLen),
            DefaultRetentionIncomeCode = Trim(defaultRetentionIncomeCode, RetentionIncomeCodeMaxLen),
            PaymentTerms               = Trim(paymentTerms, PaymentTermsMaxLen),
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Update(
        string? defaultTaxSupportCode,
        string? defaultRetentionVatCode,
        string? defaultRetentionIncomeCode,
        string? paymentTerms,
        Guid updatedBy)
    {
        DefaultTaxSupportCode      = Trim(defaultTaxSupportCode, TaxSupportCodeMaxLen);
        DefaultRetentionVatCode    = Trim(defaultRetentionVatCode, RetentionVatCodeMaxLen);
        DefaultRetentionIncomeCode = Trim(defaultRetentionIncomeCode, RetentionIncomeCodeMaxLen);
        PaymentTerms               = Trim(paymentTerms, PaymentTermsMaxLen);
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El SupplierProfile ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El SupplierProfile ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? value, int maxLen)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t)) return null;
        if (t.Length > maxLen)
            throw new ArgumentException($"El valor no puede superar {maxLen} caracteres.");
        return t;
    }
}
