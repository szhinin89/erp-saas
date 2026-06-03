using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Condiciones comerciales especÃ­ficas de un BusinessPartner en una Company concreta.
///
/// SCOPE: ICompanyScopedEntity + ISubscriberScopedEntity.
/// Query filter: fail-closed en ambas dimensiones (subscriber + company).
/// Sin company context en el JWT â†’ 0 filas retornadas.
///
/// SeparaciÃ³n de responsabilidades:
///   BusinessPartner                    â†’ identidad fiscal (quiÃ©n es)
///   CompanyBusinessPartnerSettings     â†’ condiciones comerciales por empresa (cÃ³mo opera con nosotros)
///
/// Ejemplos de uso:
///   - La misma persona jurÃ­dica puede tener CreditLimit diferente en Company A y Company B
///   - Un BP puede estar bloqueado solo en una de las empresas del Subscriber
///   - El PriceListId puede variar por empresa (lista de precios de exportaciÃ³n vs. local)
///
/// Cardinalidad: 0..N por BusinessPartner (una por cada Company que lo usa)
/// Unicidad: (company_id, business_partner_id) es Ãºnica
/// </summary>
public sealed class CompanyBusinessPartnerSettings : AuditableEntity, ISubscriberScopedEntity, ICompanyScopedEntity
{
    public Guid    SubscriberId      { get; private set; }
    public Guid    CompanyId         { get; private set; }
    public Guid    BusinessPartnerId { get; private set; }

    /// <summary>LÃ­mite de crÃ©dito aprobado para esta empresa. Null = sin lÃ­mite.</summary>
    public decimal? CreditLimit  { get; private set; }

    /// <summary>DÃ­as de crÃ©dito acordados (0 = contado).</summary>
    public short    PaymentDays  { get; private set; }

    /// <summary>Bloqueo operativo: impide crear nuevas transacciones con este BP en esta empresa.</summary>
    public bool     IsBlocked    { get; private set; }

    /// <summary>FK a lista de precios especÃ­fica para este BP en esta empresa (futuro).</summary>
    public Guid?    PriceListId  { get; private set; }

    /// <summary>
    /// Moneda del lÃ­mite de crÃ©dito. ISO-4217 (ej. "USD", "EUR").
    /// Necesario en holdings multimoneda donde Company A opera en USD y Company B en EUR.
    /// Null = moneda de la company activa.
    /// </summary>
    public string?  CreditCurrencyCode { get; private set; }

    // NavegaciÃ³n
    public BusinessPartner? BusinessPartner { get; private set; }

    private CompanyBusinessPartnerSettings() { }

    public static CompanyBusinessPartnerSettings Create(
        Guid     subscriberId,
        Guid     companyId,
        Guid     businessPartnerId,
        Guid     createdBy,
        decimal? creditLimit        = null,
        short    paymentDays        = 0,
        bool     isBlocked          = false,
        Guid?    priceListId        = null,
        string?  creditCurrencyCode = null)
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var s = new CompanyBusinessPartnerSettings
        {
            Id                 = Guid.NewGuid(),
            SubscriberId       = subscriberId,
            CompanyId = companyId,
            BusinessPartnerId  = businessPartnerId,
            CreditLimit        = creditLimit is < 0 ? null : creditLimit,
            PaymentDays        = paymentDays < 0 ? (short)0 : paymentDays,
            IsBlocked          = isBlocked,
            PriceListId        = priceListId,
            CreditCurrencyCode = NormalizeCurrencyCode(creditCurrencyCode),
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void Update(
        decimal? creditLimit,
        short    paymentDays,
        bool     isBlocked,
        Guid?    priceListId,
        Guid     updatedBy,
        string?  creditCurrencyCode = null)
    {
        CreditLimit        = creditLimit is < 0 ? null : creditLimit;
        PaymentDays        = paymentDays < 0 ? (short)0 : paymentDays;
        IsBlocked          = isBlocked;
        PriceListId        = priceListId;
        CreditCurrencyCode = NormalizeCurrencyCode(creditCurrencyCode);
        SetUpdated(updatedBy);
    }

    private static string? NormalizeCurrencyCode(string? code)
    {
        var t = code?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(t) ? null : t[..Math.Min(3, t.Length)];
    }

    public void Block(Guid updatedBy)
    {
        IsBlocked = true;
        SetUpdated(updatedBy);
    }

    public void Unblock(Guid updatedBy)
    {
        IsBlocked = false;
        SetUpdated(updatedBy);
    }
}
