using ERP.Domain.Common;

namespace ERP.Domain.Subscribers.Entities;

public class Subscriber : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public SubscriberLifecycleStatus LifecycleStatus { get; private set; } = SubscriberLifecycleStatus.Active;
    public PasswordResetMode PasswordResetMode { get; private set; } = PasswordResetMode.Disabled;

    /// <summary>Código comercial del plan (p. ej. starter). Entitlements en <c>SubscriberSubscription</c>.</summary>
    public string? PlanCode { get; private set; }

    /// <summary>Motivo de suspensión/desactivación (opcional, para auditoría).</summary>
    public string? SuspendedReason { get; private set; }

    /// <summary>Fecha de última suspensión (audit trail).</summary>
    public DateTime? SuspendedAtUtc { get; private set; }

    public int DisplayOrder { get; private set; }
    public int Priority { get; private set; }

    /// <summary>Idioma preferido de la UI para este tenant SaaS (es/en/qu).</summary>
    public string PreferredLanguage { get; private set; } = "es";

    /// <summary>
    /// True when this Subscriber is the internal platform owner (e.g., ZH Technologies).
    /// Internal owners are hidden from the tenant list in the platform control panel.
    /// Created via POST /api/setup/platform-owner — NOT via ZHTechnologiesBootstrap.
    /// </summary>
    public bool IsInternalPlatformOwner { get; private set; } = false;

    private Subscriber() { }

    /// <summary>Creates the internal platform owner subscriber during first-run provisioning.</summary>
    public static Subscriber CreatePlatformOwner(
        string name,
        string slug,
        Guid createdBy,
        string preferredLanguage = "es")
    {
        var s = Create(name, slug, createdBy,
            planCode: "platform",
            preferredLanguage: preferredLanguage);
        s.IsInternalPlatformOwner = true;
        return s;
    }

    public static Subscriber Create(
        string name,
        string slug,
        Guid createdBy,
        PasswordResetMode passwordResetMode = PasswordResetMode.Disabled,
        int displayOrder = 0,
        int priority = 0,
        string? planCode = null,
        string preferredLanguage = "es")
    {
        var tenant = new Subscriber
        {
            Id               = Guid.NewGuid(),
            SubscriberId     = Guid.Empty,
            Name             = name,
            Slug             = slug.ToLowerInvariant(),
            IsActive         = true,
            PasswordResetMode = passwordResetMode,
            DisplayOrder     = displayOrder,
            Priority         = priority,
            PlanCode         = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim(),
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "es" : preferredLanguage.Trim().ToLowerInvariant(),
        };
        tenant.SetCreated(createdBy);
        return tenant;
    }

    public void SetPlanCode(string? planCode, Guid updatedBy)
    {
        PlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>Actualiza el perfil SaaS: nombre de plataforma, slug, ordering y preferencia de idioma.</summary>
    public void UpdateSaasProfile(
        string name,
        string slug,
        int displayOrder,
        int priority,
        string preferredLanguage,
        Guid updatedBy)
    {
        Name             = name;
        Slug             = slug.ToLowerInvariant();
        DisplayOrder     = displayOrder;
        Priority         = priority;
        PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "es" : preferredLanguage.Trim().ToLowerInvariant();
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        LifecycleStatus = SubscriberLifecycleStatus.Inactive;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        IsActive = true;
        LifecycleStatus = SubscriberLifecycleStatus.Active;
        SuspendedReason = null;
        SuspendedAtUtc  = null;
        SetUpdated(updatedBy);
    }

    public void Suspend(string? reason, Guid updatedBy)
    {
        IsActive        = false;
        LifecycleStatus = SubscriberLifecycleStatus.Suspended;
        SuspendedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        SuspendedAtUtc  = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void SetTrial(Guid updatedBy)
    {
        LifecycleStatus = SubscriberLifecycleStatus.Trial;
        SetUpdated(updatedBy);
    }

    public void EnterGracePeriod(Guid updatedBy)
    {
        LifecycleStatus = SubscriberLifecycleStatus.GracePeriod;
        SetUpdated(updatedBy);
    }

    public void SetPasswordResetMode(PasswordResetMode mode, Guid updatedBy)
    {
        PasswordResetMode = mode;
        SetUpdated(updatedBy);
    }

    public bool IsOperational =>
        LifecycleStatus is SubscriberLifecycleStatus.Active
            or SubscriberLifecycleStatus.Trial
            or SubscriberLifecycleStatus.GracePeriod;
}

public enum PasswordResetMode
{
    Disabled = 0,
    Direct   = 1,
    Email    = 2,
    Phone    = 3,
}

public enum SubscriberLifecycleStatus
{
    Active      = 0,
    Trial       = 1,
    GracePeriod = 2,
    Suspended   = 3,
    Inactive    = 4,
}
