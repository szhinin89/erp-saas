namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Catálogo global de funcionalidades comercializables (no multi-tenant).</summary>
public sealed class SaasFeatureDefinition
{
    public const int CodeMaxLen = 64;
    public const int NameMaxLen = 200;

    public Guid Id { get; private set; }
    /// <summary>Código estable (USERS, ACCOUNTING, CUSTOMERS, …).</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Si es true, se controla consumo vía <see cref="TenantSubscriptionUsage"/>.</summary>
    public bool IsMetered { get; private set; }

    private SaasFeatureDefinition() { }

    public static SaasFeatureDefinition Create(string code, string name, string? description, bool isMetered)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length == 0 || c.Length > CodeMaxLen)
            throw new ArgumentException("Código de feature inválido.", nameof(code));

        return new SaasFeatureDefinition
        {
            Id = Guid.NewGuid(),
            Code = c,
            Name = (name ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsMetered = isMetered,
        };
    }
}
