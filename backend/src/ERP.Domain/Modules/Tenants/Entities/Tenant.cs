using System.Linq;
using System.Text.Json;
using ERP.Domain.Common;

namespace ERP.Domain.Tenants.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public PasswordResetMode PasswordResetMode { get; private set; } = PasswordResetMode.Disabled;

    /// <summary>Código comercial del plan (p. ej. starter, professional). Opcional.</summary>
    public string? PlanCode { get; private set; }

    /// <summary>
    /// Caché legacy de módulos (SuperAdmin). No es autoridad de entitlements; ver <c>TenantSaasSubscription</c>.
    /// Null/vacío ya no implica todos los módulos en runtime (fail-closed en sesión).
    /// </summary>
    [Obsolete("Legacy module cache. Authoritative entitlements: TenantSaasSubscription + SaasPlanFeature.")]
    public string? EnabledModulesJson { get; private set; }

    // ── Datos legales/comerciales (opcionales) ─────────────────────
    public string? Ruc { get; private set; }
    public string? ShortName { get; private set; }          // abreviado
    public string? TradeName { get; private set; }          // nombre comercial
    public string? Dinardap { get; private set; }           // identificador/flag (según necesidad)
    public string? LogoUrl { get; private set; }            // url/path logo

    // ── Orden / prioridad (para listados) ─────────────────────────
    public int DisplayOrder { get; private set; }
    public int Priority { get; private set; }

    // ── Parámetros globales por empresa (dependen de plan comercial) ──
    public bool ElectronicBillingTrialEnabled { get; private set; }

    // ── Parámetros operativos configurables por la empresa ─────────
    /// <summary>Código ISO 4217 de la moneda base (p. ej. "USD").</summary>
    public string Currency { get; private set; } = "USD";
    /// <summary>Código de idioma (p. ej. "es", "en", "qu").</summary>
    public string Language { get; private set; } = "es";
    /// <summary>Zona horaria IANA (p. ej. "America/Guayaquil").</summary>
    public string Timezone { get; private set; } = "America/Guayaquil";
    /// <summary>Prefijo de factura (p. ej. "FAC-"). Null = sin prefijo.</summary>
    public string? InvoicePrefix { get; private set; }
    /// <summary>Días de crédito por defecto al crear facturas.</summary>
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
        string? planCode = null,
        IReadOnlyList<string>? enabledModuleKeys = null)
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
        };
        tenant.ApplySubscription(planCode, enabledModuleKeys);
        tenant.SetCreated(createdBy);
        return tenant;
    }

    /// <summary>Actualiza plan y módulos contratados (JSON interno).</summary>
    public void SetSubscription(string? planCode, IReadOnlyList<string>? enabledModuleKeys, Guid updatedBy)
    {
        ApplySubscription(planCode, enabledModuleKeys);
        SetUpdated(updatedBy);
    }

    private void ApplySubscription(string? planCode, IReadOnlyList<string>? enabledModuleKeys)
    {
        PlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        if (enabledModuleKeys is null || enabledModuleKeys.Count == 0)
        {
            EnabledModulesJson = null;
            return;
        }

        var normalized = enabledModuleKeys
            .Select(k => (k ?? string.Empty).Trim().ToLowerInvariant())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        EnabledModulesJson = normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
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

    public void UpdateGlobalParameters(
        bool electronicBillingTrialEnabled,
        Guid updatedBy)
    {
        ElectronicBillingTrialEnabled = electronicBillingTrialEnabled;
        SetUpdated(updatedBy);
    }

    /// <summary>Actualiza los parámetros operativos configurables por el administrador de la empresa.</summary>
    public void UpdateOperationalSettings(
        string currency,
        string language,
        string timezone,
        string? invoicePrefix,
        int defaultCreditDays,
        Guid updatedBy)
    {
        Currency           = string.IsNullOrWhiteSpace(currency)  ? "USD"                 : currency.Trim().ToUpperInvariant();
        Language           = string.IsNullOrWhiteSpace(language)  ? "es"                  : language.Trim().ToLowerInvariant();
        Timezone           = string.IsNullOrWhiteSpace(timezone)  ? "America/Guayaquil"   : timezone.Trim();
        InvoicePrefix      = string.IsNullOrWhiteSpace(invoicePrefix) ? null              : invoicePrefix.Trim();
        DefaultCreditDays  = defaultCreditDays < 0 ? 0 : defaultCreditDays;
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
