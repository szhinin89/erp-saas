namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Token opaco de larga duración (30 días) almacenado en BD como hash SHA-256.
/// Se rota en cada uso para detectar reutilización maliciosa.
/// No implementa ISubscriberScopedEntity porque se consulta por hash, no por tenant.
/// </summary>
public sealed class RefreshToken
{
    public const int ExpiryDays = 30;

    /// <summary>"SuperAdmin" | "Identity" | "Legacy" — determina qué servicio regenera el access token.</summary>
    public const string TypeSuperAdmin = "SuperAdmin";
    public const string TypeIdentity   = "Identity";
    public const string TypeLegacy     = "Legacy";

    public Guid     Id             { get; private set; }
    public Guid     UserId         { get; private set; }
    public Guid     SubscriberId       { get; private set; }
    public Guid?    CompanyId        { get; private set; }
    public string   UserType       { get; private set; } = null!;
    public string   TokenHash      { get; private set; } = null!;
    public DateTime ExpiresAt      { get; private set; }
    public bool     IsRevoked      { get; private set; }
    public DateTime? RevokedAt     { get; private set; }
    public string?  ReplacedByHash { get; private set; }
    public string?  ReasonRevoked  { get; private set; }
    public DateTime CreatedAt      { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid   userId,
        Guid   subscriberId,
        Guid?  companyId,
        string userType,
        string tokenHash)
    {
        return new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            SubscriberId  = subscriberId,
            CompanyId = companyId == Guid.Empty ? null : companyId,
            UserType  = userType,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(ExpiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    public void Revoke(string reason, string? replacedByHash = null)
    {
        IsRevoked      = true;
        RevokedAt      = DateTime.UtcNow;
        ReasonRevoked  = reason;
        ReplacedByHash = replacedByHash;
    }
}
