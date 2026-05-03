using ERP.Domain.Subscriptions;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Plan comercial global (catálogo). Precios y visibilidad persistidos en BD (sin hardcode en aplicación).</summary>
public sealed class SaasPlan
{
    public const int CodeMaxLen = 64;
    public const int NameMaxLen = 200;
    public const int ShortLabelMaxLen = 32;
    public const int CurrencyMaxLen = 8;
    public const int BillingCycleMaxLen = 32;
    public const int ExternalBillingRefMaxLen = 128;

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    /// <summary>Etiqueta corta para UI (p. ej. STARTER, BUSINESS).</summary>
    public string? ShortLabel { get; private set; }
    public bool IsActive { get; private set; }
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string BillingCycle { get; private set; } = SaasBillingCycle.Monthly;
    public bool IsPubliclyVisible { get; private set; } = true;
    public bool IsRecommended { get; private set; }
    public int SortOrder { get; private set; }
    /// <summary>Referencia externa futura (Stripe Price, etc.).</summary>
    public string? ExternalBillingRef { get; private set; }

    private SaasPlan() { }

    public static SaasPlan Create(
        string code,
        string name,
        string? shortLabel,
        bool isActive,
        decimal priceAmount,
        string currency,
        string billingCycle,
        bool isPubliclyVisible,
        bool isRecommended,
        int sortOrder,
        string? externalBillingRef = null)
    {
        var c = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (c.Length == 0 || c.Length > CodeMaxLen)
            throw new ArgumentException("Código de plan inválido.", nameof(code));

        if (!SaasBillingCycle.IsValid(billingCycle))
            throw new ArgumentException("Ciclo de facturación inválido.", nameof(billingCycle));

        var cur = (currency ?? "USD").Trim().ToUpperInvariant();
        if (cur.Length == 0 || cur.Length > CurrencyMaxLen)
            throw new ArgumentException("Moneda inválida.", nameof(currency));

        if (priceAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(priceAmount));

        var sl = string.IsNullOrWhiteSpace(shortLabel)
            ? null
            : shortLabel.Trim().ToUpperInvariant();
        if (sl is { Length: > ShortLabelMaxLen })
            throw new ArgumentException("ShortLabel demasiado largo.", nameof(shortLabel));

        var ext = string.IsNullOrWhiteSpace(externalBillingRef)
            ? null
            : externalBillingRef.Trim();
        if (ext is { Length: > ExternalBillingRefMaxLen })
            throw new ArgumentException("ExternalBillingRef demasiado largo.", nameof(externalBillingRef));

        return new SaasPlan
        {
            Id = Guid.NewGuid(),
            Code = c,
            Name = (name ?? string.Empty).Trim(),
            ShortLabel = sl,
            IsActive = isActive,
            PriceAmount = priceAmount,
            Currency = cur,
            BillingCycle = billingCycle.Trim().ToLowerInvariant(),
            IsPubliclyVisible = isPubliclyVisible,
            IsRecommended = isRecommended,
            SortOrder = sortOrder,
            ExternalBillingRef = ext,
        };
    }

    public void UpdateCatalog(
        string name,
        string? shortLabel,
        decimal priceAmount,
        string currency,
        string billingCycle,
        bool isPubliclyVisible,
        bool isActive,
        string? externalBillingRef)
    {
        if (!SaasBillingCycle.IsValid(billingCycle))
            throw new ArgumentException("Ciclo de facturación inválido.", nameof(billingCycle));

        var cur = (currency ?? "USD").Trim().ToUpperInvariant();
        if (cur.Length == 0 || cur.Length > CurrencyMaxLen)
            throw new ArgumentException("Moneda inválida.", nameof(currency));

        if (priceAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(priceAmount));

        var sl = string.IsNullOrWhiteSpace(shortLabel)
            ? null
            : shortLabel.Trim().ToUpperInvariant();
        if (sl is { Length: > ShortLabelMaxLen })
            throw new ArgumentException("ShortLabel demasiado largo.", nameof(shortLabel));

        var ext = string.IsNullOrWhiteSpace(externalBillingRef)
            ? null
            : externalBillingRef.Trim();
        if (ext is { Length: > ExternalBillingRefMaxLen })
            throw new ArgumentException("ExternalBillingRef demasiado largo.", nameof(externalBillingRef));

        Name = (name ?? string.Empty).Trim();
        ShortLabel = sl;
        PriceAmount = priceAmount;
        Currency = cur;
        BillingCycle = billingCycle.Trim().ToLowerInvariant();
        IsPubliclyVisible = isPubliclyVisible;
        IsActive = isActive;
        ExternalBillingRef = ext;
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void SetRecommended(bool value) => IsRecommended = value;
}
