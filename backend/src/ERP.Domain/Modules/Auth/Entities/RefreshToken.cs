namespace ERP.Domain.Auth.Entities;

/// <summary>
/// Token opaco almacenado en BD como hash SHA-256. Se rota en cada uso para detectar
/// reutilización maliciosa. No implementa ITenantScopedEntity porque se consulta por hash,
/// no por tenant.
///
/// Vencimiento individual (<see cref="ExpiresAt"/>) vs. ventana absoluta de sesión
/// (<see cref="AbsoluteExpiresAt"/>): la rotación en cada refresh renueva
/// <see cref="ExpiresAt"/>, pero <see cref="AbsoluteExpiresAt"/> se fija en la emisión inicial
/// de la familia y se hereda sin cambios en cada sucesor — evita que una sesión se extienda
/// indefinidamente solo por seguir usándose. El caller (<c>RefreshTokenService</c>) siempre
/// calcula <see cref="ExpiresAt"/> como el mínimo entre la duración individual configurada y
/// <see cref="AbsoluteExpiresAt"/>, así que ninguna consulta existente por <c>ExpiresAt</c>
/// (limpieza, revocación masiva, <c>IsActive</c>) necesita conocer la ventana absoluta aparte.
/// </summary>
public sealed class RefreshToken
{
    public const string TypeIdentity = "Identity";
    public const string TypeLegacy = "Legacy";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string UserType { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Límite absoluto de la sesión/familia de rotación, fijado en la emisión inicial y
    /// heredado sin cambios por cada sucesor rotado — nunca se extiende.
    /// </summary>
    public DateTime AbsoluteExpiresAt { get; private set; }
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

    /// <summary>
    /// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: true cuando este token se originó en
    /// <c>POST /auth/global/operate-company</c> (admin global operando una empresa sin
    /// CompanyUserMembership). Se hereda sin cambios en cada sucesor rotado — es el único
    /// registro persistido de "esta sesión viene de operar empresa", porque el access token
    /// (que sí lleva <c>operator_mode</c>/<c>global_admin_user_id</c> como claims) nunca se
    /// persiste: el navegador solo conserva la cookie httpOnly del refresh token. Sin este flag,
    /// <c>RefreshTokenHandler</c> no tenía forma de saber que debía reemitir esos claims al
    /// rotar, y la sesión se degradaba silenciosamente a admin de empresa normal en cada
    /// refresh/F5 — bug real encontrado en revisión manual.
    /// </summary>
    public bool IsOperatorSession { get; private set; }

    /// <summary>
    /// UserId del admin global cuando <see cref="IsOperatorSession"/> es true — mismo valor que
    /// el claim <c>global_admin_user_id</c> del access token emitido por operate-company. Se
    /// revalida contra <c>GlobalUserRole</c> en cada refresh (nunca se confía en el flag solo)
    /// antes de reemitir los claims de operador.
    /// </summary>
    public Guid? GlobalAdminUserId { get; private set; }

    private RefreshToken() { }

    /// <summary>
    /// <paramref name="expiresAt"/> y <paramref name="absoluteExpiresAt"/> los calcula el caller
    /// (política de configuración, fuera del Domain). Al rotar, el caller debe pasar el mismo
    /// <paramref name="absoluteExpiresAt"/> del token que se está reemplazando, y el mismo
    /// <paramref name="isOperatorSession"/>/<paramref name="globalAdminUserId"/> del token
    /// predecesor — ver <see cref="IsOperatorSession"/>.
    /// </summary>
    public static RefreshToken Create(
        Guid userId,
        Guid tenantId,
        Guid? companyId,
        string userType,
        string tokenHash,
        DateTime expiresAt,
        DateTime absoluteExpiresAt,
        Guid? familyId = null,
        Guid? parentTokenId = null,
        int rotationDepth = 0,
        bool isOperatorSession = false,
        Guid? globalAdminUserId = null
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
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            FamilyId = familyId ?? id,
            ParentTokenId = parentTokenId,
            RotationDepth = rotationDepth,
            IsOperatorSession = isOperatorSession,
            GlobalAdminUserId = globalAdminUserId,
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
