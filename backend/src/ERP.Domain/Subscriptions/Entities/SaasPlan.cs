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
    public const int MenuSidebarLayoutMaxLen = 32;

    public const string MenuSidebarLayoutHorizontal = "horizontal";
    public const string MenuSidebarLayoutVertical = "vertical";

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

    /// <summary>JSON del menú lateral (mismo shape que <c>SessionMenuGroupDto[]</c>). Null/vacío = usar menú global <c>ui_nav_*</c>.</summary>
    public string? MenuConfigJson { get; private set; }

    /// <summary>Preferencia de barra lateral para previsualización y SPA (<c>horizontal</c> | <c>vertical</c>).</summary>
    public string MenuSidebarLayout { get; private set; } = MenuSidebarLayoutHorizontal;

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
            throw new ArgumentException("Currency inválida.", nameof(currency));

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
            MenuConfigJson = null,
            MenuSidebarLayout = MenuSidebarLayoutHorizontal,
        };
    }

    /// <summary>Asigna o limpia el menú comercial (JSON). <paramref name="menuConfigJson"/> null o blanco borra la personalización del plan.</summary>
    public void SetMenuConfigJson(string? menuConfigJson)
    {
        var t = menuConfigJson?.Trim();
        MenuConfigJson = string.IsNullOrEmpty(t) ? null : t;
    }

    /// <summary>Layout de barra lateral; solo <see cref="MenuSidebarLayoutHorizontal"/> o <see cref="MenuSidebarLayoutVertical"/>.</summary>
    public void SetMenuSidebarLayout(string? layout)
    {
        var v = string.IsNullOrWhiteSpace(layout)
            ? MenuSidebarLayoutHorizontal
            : layout.Trim().ToLowerInvariant();
        if (v.Length > MenuSidebarLayoutMaxLen)
            throw new ArgumentException("Layout demasiado largo.", nameof(layout));
        if (v != MenuSidebarLayoutHorizontal && v != MenuSidebarLayoutVertical)
            throw new ArgumentException("Layout debe ser horizontal o vertical.", nameof(layout));
        MenuSidebarLayout = v;
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
            throw new ArgumentException("Currency inválida.", nameof(currency));

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
