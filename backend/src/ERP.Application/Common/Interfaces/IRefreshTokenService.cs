namespace ERP.Application.Common.Interfaces;

/// <summary>Constantes de tipo de usuario para refresh tokens. Evita dependencia circular con el namespace de handlers.</summary>
public static class RefreshUserType
{
    public const string Platform   = "Platform";
    public const string SuperAdmin = "SuperAdmin";
    public const string Identity   = "Identity";
    public const string Legacy     = "Legacy";
}

public interface IRefreshTokenService
{
    /// <summary>
    /// Genera un token aleatorio (64 bytes, base64), lo hashea con SHA-256 y lo persiste en BD.
    /// Devuelve el token en texto plano (para enviar al cliente) y su fecha de expiración.
    /// </summary>
    Task<(string RawToken, DateTime Expiry)> CreateAsync(
        Guid userId, Guid subscriberId, Guid? companyId, string userType, CancellationToken ct = default);

    /// <summary>
    /// Valida el token recibido del cliente, lo rota (revoca el actual, emite uno nuevo)
    /// y devuelve los datos del usuario para regenerar el access token.
    /// </summary>
    Task<RefreshTokenValidationResult> ValidateAndRotateAsync(
        string rawToken, CancellationToken ct = default);

    /// <summary>Revoca todos los refresh tokens activos del usuario (logout).</summary>
    Task RevokeAllForUserAsync(
        Guid userId, Guid subscriberId, string reason, CancellationToken ct = default);

    /// <summary>Revoca un token específico por su valor en texto plano (logout de dispositivo).</summary>
    Task RevokeAsync(
        string rawToken, string reason, CancellationToken ct = default);
}

public sealed class RefreshTokenValidationResult
{
    public bool     IsValid   { get; init; }
    public Guid     UserId    { get; init; }
    public Guid     SubscriberId  { get; init; }
    public Guid?    CompanyId   { get; init; }
    public string?  UserType  { get; init; }
    public string?  NewToken  { get; init; }
    public DateTime? NewExpiry { get; init; }
    public string?  Error     { get; init; }

    public static RefreshTokenValidationResult Fail(string error)
        => new() { IsValid = false, Error = error };

    public static RefreshTokenValidationResult Ok(
        Guid userId, Guid subscriberId, Guid? companyId, string userType,
        string newToken, DateTime newExpiry)
        => new()
        {
            IsValid  = true,
            UserId   = userId,
            SubscriberId = subscriberId,
            CompanyId = companyId,
            UserType = userType,
            NewToken = newToken,
            NewExpiry = newExpiry,
        };
}
