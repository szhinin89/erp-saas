using ERP.Domain.Common;

namespace ERP.Domain.Subscribers.Entities;

public class Subscriber : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public PasswordResetMode PasswordResetMode { get; private set; } = PasswordResetMode.Disabled;

    /// <summary>Código comercial del plan (p. ej. starter). Entitlements en <c>SubscriberSubscription</c>.</summary>
    public string? PlanCode { get; private set; }

    // FIXME(phase5-db): Ruc pertenece a Company (entidad legal). Mover en migración DB futura.
    public string? Ruc { get; private set; }
    public string? ShortName { get; private set; }
    // FIXME(phase5-db): TradeName pertenece a Company. Mover en migración DB futura.
    public string? TradeName { get; private set; }
    // FIXME(phase5-db): Dinardap pertenece a Company (registro fiscal). Mover en migración DB futura.
    public string? Dinardap { get; private set; }
    // FIXME(phase5-db): LogoUrl pertenece a Company (branding). Mover en migración DB futura.
    public string? LogoUrl { get; private set; }

    public int DisplayOrder { get; private set; }
    public int Priority { get; private set; }

    public bool ElectronicBillingTrialEnabled { get; private set; }

    // FIXME(phase5-db): Currency duplicado con Company.CurrencyCode. Consolidar en Company.
    public string Currency { get; private set; } = "USD";
    public string Language { get; private set; } = "es";
    // FIXME(phase5-db): Timezone duplicado con Company.Timezone. Consolidar en Company.
    public string Timezone { get; private set; } = "America/Guayaquil";
    // FIXME(phase5-db): InvoicePrefix pertenece a Company (prefijo de comprobantes SRI).
    public string? InvoicePrefix { get; private set; }
    // FIXME(phase5-db): DefaultCreditDays es parámetro operativo de Company.
    public int DefaultCreditDays { get; private set; } = 30;

    private Subscriber() { }

    public static Subscriber Create(
        string name,
        string slug,
        Guid createdBy,
        PasswordResetMode passwordResetMode = PasswordResetMode.Disabled,
        string? ruc = null,
        string? shortName = null,
        string? tradeName = null,
        string? dinardap = null,
        string? logoUrl = null,
        int displayOrder = 0,
        int priority = 0,
        string? planCode = null)
    {
        var tenant = new Subscriber
        {
            Id       = Guid.NewGuid(),
            SubscriberId = Guid.Empty,
            Name     = name,
            Slug     = slug.ToLowerInvariant(),
            IsActive = true,
            PasswordResetMode = passwordResetMode,
            Ruc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim(),
            ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            Dinardap = string.IsNullOrWhiteSpace(dinardap) ? null : dinardap.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            DisplayOrder = displayOrder,
            Priority = priority,
            ElectronicBillingTrialEnabled = false,
            PlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim(),
        };
        tenant.SetCreated(createdBy);
        return tenant;
    }

    public void SetPlanCode(string? planCode, Guid updatedBy)
    {
        PlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Actualiza el perfil SaaS del suscriptor (nombre de plataforma, slug, ordering y campos
    /// fiscales heredados pendientes de migración a <c>Company</c>).
    /// </summary>
    public void UpdateSaasProfile(
        string name,
        string slug,
        string? ruc,
        string? shortName,
        string? tradeName,
        string? dinardap,
        string? logoUrl,
        int displayOrder,
        int priority,
        Guid updatedBy)
    {
        Name = name;
        Slug = slug.ToLowerInvariant();
        Ruc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        Dinardap = string.IsNullOrWhiteSpace(dinardap) ? null : dinardap.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        DisplayOrder = displayOrder;
        Priority = priority;
        SetUpdated(updatedBy);
    }

    /// <summary>Alias backward-compat. Usar <see cref="UpdateSaasProfile"/> en código nuevo.</summary>
    [Obsolete("Use UpdateSaasProfile(). UpdateCompanyData is a misnomer — this method updates SaaS account profile, not the Company entity. Will be removed in phase5-db cleanup.")]
    public void UpdateCompanyData(
        string name, string slug, string? ruc, string? shortName, string? tradeName,
        string? dinardap, string? logoUrl, int displayOrder, int priority, Guid updatedBy)
        => UpdateSaasProfile(name, slug, ruc, shortName, tradeName, dinardap, logoUrl, displayOrder, priority, updatedBy);

    public void UpdateGlobalParameters(bool electronicBillingTrialEnabled, Guid updatedBy)
    {
        ElectronicBillingTrialEnabled = electronicBillingTrialEnabled;
        SetUpdated(updatedBy);
    }

    public void UpdateOperationalSettings(
        string currency,
        string language,
        string timezone,
        string? invoicePrefix,
        int defaultCreditDays,
        Guid updatedBy)
    {
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        Language = string.IsNullOrWhiteSpace(language) ? "es" : language.Trim().ToLowerInvariant();
        Timezone = string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone.Trim();
        InvoicePrefix = string.IsNullOrWhiteSpace(invoicePrefix) ? null : invoicePrefix.Trim();
        DefaultCreditDays = defaultCreditDays < 0 ? 0 : defaultCreditDays;
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void SetPasswordResetMode(PasswordResetMode mode, Guid updatedBy)
    {
        PasswordResetMode = mode;
        SetUpdated(updatedBy);
    }
}

public enum PasswordResetMode
{
    Disabled = 0,
    Direct = 1,
    Email = 2,
    Phone = 3,
}
