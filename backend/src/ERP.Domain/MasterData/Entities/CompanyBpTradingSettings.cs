using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: configuración operativa de un BusinessPartner en una Company específica.
///
/// SCOPE: ISubscriberScopedEntity + ICompanyScopedEntity.
/// Query filter fail-closed en ambas dimensiones. Sin company context → 0 filas.
///
/// CONTIENE: condiciones comerciales negociadas por empresa (crédito, plazos, bloqueos).
/// NO CONTIENE: identidad fiscal (→ BusinessPartner), roles (→ BusinessPartnerRole),
///              listas de precios (→ módulo futuro).
///
/// Única entrada por (subscriber_id, company_id, business_partner_id). Upsert semántico.
/// </summary>
public sealed class CompanyBpTradingSettings : AuditableEntity, ISubscriberScopedEntity, ICompanyScopedEntity
{
    public const int BlockedReasonMaxLen  = 500;
    public const int CurrencyCodeLen      = 3;

    public Guid     CompanyId         { get; private set; }
    public Guid     BusinessPartnerId  { get; private set; }
    public decimal  CreditLimit        { get; private set; }
    public string   CreditCurrencyCode { get; private set; } = "USD";
    public int      PaymentDays        { get; private set; }
    public bool     IsBlocked          { get; private set; }
    public string?  BlockedReason      { get; private set; }
    public DateTime? BlockedAt         { get; private set; }
    public Guid?    BlockedBy          { get; private set; }

    private CompanyBpTradingSettings() { }

    public static CompanyBpTradingSettings Create(
        Guid    subscriberId,
        Guid    companyId,
        Guid    businessPartnerId,
        decimal creditLimit,
        int     paymentDays,
        Guid    createdBy,
        string  creditCurrencyCode = "USD")
    {
        if (subscriberId == Guid.Empty)
            throw new ArgumentException("SubscriberId es obligatorio.", nameof(subscriberId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException("BusinessPartnerId es obligatorio.", nameof(businessPartnerId));

        var settings = new CompanyBpTradingSettings
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            CompanyId         = companyId,
            BusinessPartnerId = businessPartnerId,
            IsBlocked         = false,
        };
        settings.ApplyTradingTerms(creditLimit, paymentDays, creditCurrencyCode);
        settings.SetCreated(createdBy);
        return settings;
    }

    /// <summary>
    /// Actualiza las condiciones comerciales. Usado en el flujo Upsert del handler.
    /// </summary>
    public void Update(decimal creditLimit, int paymentDays, Guid updatedBy, string creditCurrencyCode = "USD")
    {
        ApplyTradingTerms(creditLimit, paymentDays, creditCurrencyCode);
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Bloquea operativamente al BP en esta empresa.
    /// INVARIANTE: reason, blockedAt y blockedBy son obligatorios cuando IsBlocked = true.
    /// Este constraint también está en BD (chk_cbts_block_consistency).
    /// </summary>
    public void Block(string reason, Guid blockedBy)
    {
        if (IsBlocked)
            throw new InvalidOperationException("El BusinessPartner ya está bloqueado en esta empresa.");

        var r = reason?.Trim();
        if (string.IsNullOrEmpty(r))
            throw new ArgumentException("El motivo de bloqueo es obligatorio.", nameof(reason));
        if (r.Length > BlockedReasonMaxLen)
            throw new ArgumentException($"El motivo de bloqueo no puede superar {BlockedReasonMaxLen} caracteres.", nameof(reason));
        if (blockedBy == Guid.Empty)
            throw new ArgumentException("BlockedBy es obligatorio.", nameof(blockedBy));

        IsBlocked     = true;
        BlockedReason = r;
        BlockedAt     = DateTime.UtcNow;
        BlockedBy     = blockedBy;
        SetUpdated(blockedBy);
    }

    public void Unblock(Guid unblockedBy)
    {
        if (!IsBlocked)
            throw new InvalidOperationException("El BusinessPartner no está bloqueado en esta empresa.");
        if (unblockedBy == Guid.Empty)
            throw new ArgumentException("UnblockedBy es obligatorio.", nameof(unblockedBy));

        IsBlocked     = false;
        BlockedReason = null;
        BlockedAt     = null;
        BlockedBy     = null;
        SetUpdated(unblockedBy);
    }

    private void ApplyTradingTerms(decimal creditLimit, int paymentDays, string currencyCode)
    {
        if (creditLimit < 0)
            throw new ArgumentException("El límite de crédito no puede ser negativo.", nameof(creditLimit));
        if (paymentDays < 0)
            throw new ArgumentException("El plazo de pago no puede ser negativo.", nameof(paymentDays));

        var code = (currencyCode ?? "USD").Trim().ToUpperInvariant();
        if (code.Length != CurrencyCodeLen)
            throw new ArgumentException($"CreditCurrencyCode debe ser un código ISO 4217 de {CurrencyCodeLen} letras.", nameof(currencyCode));

        CreditLimit        = creditLimit;
        PaymentDays        = paymentDays;
        CreditCurrencyCode = code;
    }
}
