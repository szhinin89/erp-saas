namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Token opaco de un solo uso para restablecer contraseña (hash en BD).
/// No está ligado a un tenant en consultas públicas; se resuelve por hash del token.
/// </summary>
public sealed class PasswordResetToken
{
    public const string KindSuperAdmin = "SuperAdmin";
    public const string KindIdentity = "Identity";
    public const string KindLegacy = "Legacy";

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public Guid UserId { get; private set; }
    /// <summary>Tipo de cuenta: <see cref="KindSuperAdmin"/>, <see cref="KindIdentity"/>, <see cref="KindLegacy"/>.</summary>
    public string UserKind { get; private set; } = null!;
    /// <summary>Empresa asociada; null solo para SuperAdmin global.</summary>
    public Guid? TenantId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool Used { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        string tokenHash,
        Guid userId,
        string userKind,
        Guid? tenantId,
        DateTime expiresAtUtc)
    {
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            UserId = userId,
            UserKind = userKind,
            TenantId = tenantId,
            ExpiresAt = expiresAtUtc,
            Used = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public bool IsValid => !Used && ExpiresAt > DateTime.UtcNow;

    public void MarkUsed()
    {
        Used = true;
    }
}
