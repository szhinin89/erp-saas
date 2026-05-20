using ERP.Domain.Common;

namespace ERP.Domain.Tenants.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public PasswordResetMode PasswordResetMode { get; private set; } = PasswordResetMode.Disabled;

    /// <summary>Código comercial del plan (p. ej. starter). Entitlements en <c>TenantSaasSubscription</c>.</summary>
    public string? PlanCode { get; private set; }

    public string? Ruc { get; private set; }
    public string? ShortName { get; private set; }
    public string? TradeName { get; private set; }
    public string? Dinardap { get; private set; }
    public string? LogoUrl { get; private set; }

    public int DisplayOrder { get; private set; }
    public int Priority { get; private set; }

    public bool ElectronicBillingTrialEnabled { get; private set; }

    public string Currency { get; private set; } = "USD";
    public string Language { get; private set; } = "es";
    public string Timezone { get; private set; } = "America/Guayaquil";
    public string? InvoicePrefix { get; private set; }
    public int DefaultCreditDays { get; private set; } = 30;

    private Tenant() { }

    public static Tenant Create(
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
        var tenant = new Tenant
        {
            Id       = Guid.NewGuid(),
            TenantId = Guid.Empty,
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

    public void UpdateCompanyData(
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
