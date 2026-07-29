namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Token opaco de larga duración (30 días) almacenado en BD como hash SHA-256.
/// Se rota en cada uso para detectar reutilización maliciosa.
/// No implementa ITenantScopedEntity porque se consulta por hash, no por tenant.
/// </summary>
public sealed class RefreshToken
{
    public const int ExpiryDays = 30;

    public const string TypeIdentity = "Identity";
    public const string TypeLegacy = "Legacy";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string UserType { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }
    public string? ReasonRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Cadena de rotación (sesión/dispositivo). Todos los sucesores comparten el mismo FamilyId.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Token anterior en la cadena de rotación, si aplica.</summary>
    public Guid? ParentTokenId { get; private set; }

    /// <summary>Profundidad de rotación dentro de la familia (0 = emisión inicial).</summary>
    public int RotationDepth { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        Guid tenantId,
        Guid? companyId,
        string userType,
        string tokenHash,
        Guid? familyId = null,
        Guid? parentTokenId = null,
        int rotationDepth = 0
    )
    {
        var id = Guid.NewGuid();
        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            CompanyId = companyId == Guid.Empty ? null : companyId,
            UserType = userType,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(ExpiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            FamilyId = familyId ?? id,
            ParentTokenId = parentTokenId,
            RotationDepth = rotationDepth,
        };
    }

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    public void Revoke(string reason, string? replacedByHash = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        ReasonRevoked = reason;
        ReplacedByHash = replacedByHash;
    }
}
