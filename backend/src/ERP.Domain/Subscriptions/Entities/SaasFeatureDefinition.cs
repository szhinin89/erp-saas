using ERP.Domain.Subscriptions;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>
/// Catálogo global de funcionalidades comercializables (no multi-tenant).
/// Cada módulo o formulario expuesto en el producto debe tener aquí su definición antes de asociarse a planes.
/// </summary>
public sealed class SaasFeatureDefinition
{
    public const int CodeMaxLen = 64;
    public const int NameMaxLen = 200;
    public const int ResourceRefMaxLen = 128;

    public Guid Id { get; private set; }
    /// <summary>Código estable (USERS, ACCOUNTING, …).</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Si es true, se controla consumo vía <see cref="TenantSubscriptionUsage"/>.</summary>
    public bool IsMetered { get; private set; }
    public SaasFeatureKind Kind { get; private set; }
    /// <summary>Clave de permiso, código de formulario u otro ancla técnica (opcional).</summary>
    public string? ResourceRef { get; private set; }

    private SaasFeatureDefinition() { }

    public static SaasFeatureDefinition Create(
        string code,
        string name,
        string? description,
        bool isMetered,
        SaasFeatureKind kind = SaasFeatureKind.Module,
        string? resourceRef = null)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length == 0 || c.Length > CodeMaxLen)
            throw new ArgumentException("Código de feature inválido.", nameof(code));

        var rr = string.IsNullOrWhiteSpace(resourceRef) ? null : resourceRef.Trim();
        if (rr is { Length: > ResourceRefMaxLen })
            throw new ArgumentException("ResourceRef demasiado largo.", nameof(resourceRef));

        return new SaasFeatureDefinition
        {
            Id = Guid.NewGuid(),
            Code = c,
            Name = (name ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsMetered = isMetered,
            Kind = kind,
            ResourceRef = rr,
        };
    }

    public void Update(string name, string? description, bool isMetered, SaasFeatureKind kind, string? resourceRef)
    {
        var rr = string.IsNullOrWhiteSpace(resourceRef) ? null : resourceRef.Trim();
        if (rr is { Length: > ResourceRefMaxLen })
            throw new ArgumentException("ResourceRef demasiado largo.", nameof(resourceRef));

        Name = (name ?? string.Empty).Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsMetered = isMetered;
        Kind = kind;
        ResourceRef = rr;
    }
}
