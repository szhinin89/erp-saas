using ERP.Domain.Auth.Entities;

namespace ERP.Application.Common.Interfaces;

/// <summary>Constantes de tipo de usuario para refresh tokens. Evita dependencia circular con el namespace de handlers.</summary>
public static class RefreshUserType
{
    public const string Identity = "Identity";
    public const string Legacy = "Legacy";
}

public interface IRefreshTokenService
{
    /// <summary>
    /// Genera un token aleatorio (64 bytes, base64), lo hashea con SHA-256 y lo persiste en BD.
    /// Devuelve el token en texto plano (para enviar al cliente) y su fecha de expiración.
    /// </summary>
    Task<(string RawToken, DateTime Expiry)> CreateAsync(
        Guid userId, Guid tenantId, Guid? companyId, string userType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="CreateAsync"/> (misma generación/hash, mismo <c>RefreshToken.Create</c>),
    /// pero NO llama a SaveChangesAsync — deja la entidad agregada al ChangeTracker para que el
    /// caller la combine en la misma unidad de trabajo que otra escritura relacionada del mismo
    /// ErpDbContext (p. ej. UserSession.RefreshTokenId). El caller es responsable de llamar a
    /// SaveChangesAsync exactamente una vez, vía cualquier repositorio que comparta el contexto.
    /// </summary>
    Task<(RefreshToken Entity, string RawToken)> CreateWithoutSaveAsync(
        Guid userId, Guid tenantId, Guid? companyId, string userType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida el token recibido del cliente, lo rota (revoca el actual, emite uno nuevo)
    /// y devuelve los datos del usuario para regenerar el access token.
    /// </summary>
    Task<RefreshTokenValidationResult> ValidateAndRotateAsync(
        string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revoca todos los refresh tokens activos del usuario (logout global / cambio contraseña).</summary>
    Task RevokeAllForUserAsync(
        Guid userId, Guid tenantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Revoca la cadena de rotación comprometida sin afectar otras sesiones/dispositivos.</summary>
    Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca un token específico por su valor en texto plano (logout de dispositivo). Devuelve el
    /// Id del RefreshToken encontrado (o null si no existe) para que el caller pueda correlacionar
    /// y cerrar la UserSession asociada (UserSession.RefreshTokenId) sin que este servicio conozca
    /// nada de UserSession.
    /// </summary>
    Task<Guid?> RevokeAsync(
        string rawToken, string reason, CancellationToken cancellationToken = default);
}

public static class RefreshTokenAuditEvents
{
    public const string RefreshSuccess = "refresh_success";
    public const string RefreshReuseBenign = "refresh_reuse_benign";
    public const string RefreshReuseSuspicious = "refresh_reuse_suspicious";
    public const string RefreshFamilyRevoked = "refresh_family_revoked";
    public const string RefreshMultitabRetry = "refresh_multitab_retry";
    public const string RefreshRotationFailed = "refresh_rotation_failed";
    public const string RefreshRateLimited = "refresh_rate_limited";
}

public sealed class RefreshTokenValidationResult
{
    public bool IsValid { get; init; }
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? CompanyId { get; init; }
    public string? UserType { get; init; }
    public string? NewToken { get; init; }
    public DateTime? NewExpiry { get; init; }
    public string? Error { get; init; }
    public bool IsRateLimited { get; init; }

    public static RefreshTokenValidationResult Fail(string error)
        => new() { IsValid = false, Error = error };

    public static RefreshTokenValidationResult RateLimited(string error)
        => new() { IsValid = false, IsRateLimited = true, Error = error };

    public static RefreshTokenValidationResult Ok(
        Guid userId, Guid tenantId, Guid? companyId, string userType,
        string newToken, DateTime newExpiry)
        => new()
        {
            IsValid = true,
            UserId = userId,
            TenantId = tenantId,
            CompanyId = companyId,
            UserType = userType,
            NewToken = newToken,
            NewExpiry = newExpiry,
        };
}
