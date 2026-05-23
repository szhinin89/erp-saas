using ERP.Domain.Common;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate root del bounded context MasterData.
///
/// Representa la identidad comercial/fiscal única de una persona natural o jurídica
/// dentro de un Subscriber. Un mismo RUC/CI existe una SOLA vez por Subscriber,
/// independientemente de cuántas Companies tenga.
///
/// SCOPE: ISubscriberScopedEntity — NO tiene CompanyId.
///
/// Relación con Companies:
///   Las condiciones comerciales específicas por empresa (CreditLimit, PaymentDays, etc.)
///   se modelan en CompanyBusinessPartnerSettings, que sí es ICompanyScopedEntity.
///
/// Migración futura:
///   Customer.IdentificationNumber → BusinessPartner.Identification.Number
///   Supplier.Ruc                 → BusinessPartner.Identification.Number
///   Customer/Supplier conservan solo datos operativos de transacción.
///
/// IMPORTANTE: No agregar CompanyId a esta entidad. Es una decisión de dominio FINAL.
/// Ver docs/arch/BUSINESSPARTNER-ADR.md.
/// </summary>
public sealed class BusinessPartner : AuditableEntity, ISubscriberScopedEntity
{
    public const int LegalNameMaxLen  = 200;
    public const int TradeNameMaxLen  = 200;
    public const int EmailMaxLen      = 120;
    public const int PhoneMaxLen      = 40;
    public const int CountryCodeMaxLen = 3;

    /// <summary>FK al Subscriber SaaS propietario. No confundir con Company.</summary>
    public Guid SubscriberId { get; private set; }

    /// <summary>Identificación fiscal/personal unificada (RUC, CI, PASSPORT, OTHER).</summary>
    public TaxIdentification Identification { get; private set; } = null!;

    /// <summary>Razón social o nombre completo legal.</summary>
    public string LegalName { get; private set; } = null!;

    /// <summary>Nombre comercial (opcional).</summary>
    public string? TradeName { get; private set; }

    /// <summary>Email de contacto principal (opcional).</summary>
    public string? Email { get; private set; }

    /// <summary>Teléfono de contacto principal (opcional).</summary>
    public string? Phone { get; private set; }

    /// <summary>ISO-3 del país (ej. ECU, USA). FK → sri_country.code.</summary>
    public string? CountryCode { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ── Roles del BusinessPartner (1:1 opcionales) ─────────────
    // Navegación lazy — no cargar por defecto. Usar proyección explícita.
    public CustomerProfile? CustomerProfile { get; private set; }
    public SupplierProfile? SupplierProfile { get; private set; }

    private BusinessPartner() { }

    public static BusinessPartner Create(
        Guid   subscriberId,
        string identificationType,
        string identificationNumber,
        string legalName,
        Guid   createdBy,
        string? tradeName    = null,
        string? email        = null,
        string? phone        = null,
        string? countryCode  = null)
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));

        var bp = new BusinessPartner
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            Identification = TaxIdentification.Create(identificationType, identificationNumber),
            LegalName      = Normalize(legalName, nameof(legalName), LegalNameMaxLen),
            TradeName      = NormalizeOpt(tradeName, TradeNameMaxLen),
            Email          = NormalizeOpt(email, EmailMaxLen),
            Phone          = NormalizeOpt(phone, PhoneMaxLen),
            CountryCode    = string.IsNullOrWhiteSpace(countryCode) ? null
                             : countryCode.Trim().ToUpperInvariant()[..Math.Min(3, countryCode.Trim().Length)],
            IsActive       = true,
        };
        bp.SetCreated(createdBy);
        return bp;
    }

    public void UpdateProfile(
        string  legalName,
        string? tradeName,
        string? email,
        string? phone,
        string? countryCode,
        Guid    updatedBy)
    {
        LegalName   = Normalize(legalName, nameof(legalName), LegalNameMaxLen);
        TradeName   = NormalizeOpt(tradeName, TradeNameMaxLen);
        Email       = NormalizeOpt(email, EmailMaxLen);
        Phone       = NormalizeOpt(phone, PhoneMaxLen);
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null
                      : countryCode.Trim().ToUpperInvariant()[..Math.Min(3, countryCode.Trim().Length)];
        SetUpdated(updatedBy);
    }

    public void UpdateIdentification(string type, string number, Guid updatedBy)
    {
        Identification = TaxIdentification.Create(type, number);
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    private static string Normalize(string value, string paramName, int maxLen)
    {
        var t = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(t))
            throw new ArgumentException($"{paramName} es obligatorio.", paramName);
        if (t.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return t;
    }

    private static string? NormalizeOpt(string? value, int maxLen)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t)) return null;
        if (t.Length > maxLen)
            throw new ArgumentException($"El valor no puede superar {maxLen} caracteres.");
        return t;
    }
}
