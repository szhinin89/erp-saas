namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Token opaco de larga duraciÃ³n (30 dÃ­as) almacenado en BD como hash SHA-256.
/// Se rota en cada uso para detectar reutilizaciÃ³n maliciosa.
/// No implementa ISubscriberScopedEntity porque se consulta por hash, no por tenant.
/// </summary>
public sealed class RefreshToken
{
    public const int ExpiryDays = 30;

    /// <summary>"Platform" | "PlatformOperator" | "Identity" | "Legacy" (acepta legacy wire role).</summary>
    public const string TypePlatform   = "Platform";
    public const string TypePlatformOperator = "PlatformOperator";
    public const string TypeIdentity   = "Identity";
    public const string TypeLegacy     = "Legacy";

    public Guid     Id             { get; private set; }
    public Guid     UserId         { get; private set; }
    public Guid     SubscriberId       { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string   UserType       { get; private set; } = null!;
    public string   TokenHash      { get; private set; } = null!;
    public DateTime ExpiresAt      { get; private set; }
    public bool     IsRevoked      { get; private set; }
    public DateTime? RevokedAt     { get; private set; }
    public string?  ReplacedByHash { get; private set; }
    public string?  ReasonRevoked  { get; private set; }
    public DateTime CreatedAt      { get; private set; }

    /// <summary>Cadena de rotaciÃ³n (sesiÃ³n/dispositivo). Todos los sucesores comparten el mismo FamilyId.</summary>
    public Guid     FamilyId       { get; private set; }

    /// <summary>Token anterior en la cadena de rotaciÃ³n, si aplica.</summary>
    public Guid?    ParentTokenId  { get; private set; }

    /// <summary>Profundidad de rotaciÃ³n dentro de la familia (0 = emisiÃ³n inicial).</summary>
    public int      RotationDepth  { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid   userId,
        Guid   subscriberId,
        Guid?  companyId,
        string userType,
        string tokenHash,
        Guid?  familyId = null,
        Guid?  parentTokenId = null,
        int    rotationDepth = 0)
    {
        var id = Guid.NewGuid();
        return new RefreshToken
        {
            Id             = id,
            UserId         = userId,
            SubscriberId   = subscriberId,
            CompanyId      = companyId == Guid.Empty ? null : companyId,
            UserType       = userType,
            TokenHash      = tokenHash,
            ExpiresAt      = DateTime.UtcNow.AddDays(ExpiryDays),
            IsRevoked      = false,
            CreatedAt      = DateTime.UtcNow,
            FamilyId       = familyId ?? id,
            ParentTokenId  = parentTokenId,
            RotationDepth  = rotationDepth,
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
