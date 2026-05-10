namespace ERP.Application.Auth.DTOs;

// ── Request DTOs (endpoints /auth/refresh y /auth/logout) ─────────────────

public record RefreshRequest(string RefreshToken);

/// <param name="RefreshToken">Token opaco a revocar. Obligatorio.</param>
/// <param name="AllDevices">Si true, revoca todos los tokens del usuario (logout global).</param>
public record LogoutRequest(string? RefreshToken, bool AllDevices = false);

public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid TenantId
);

public record LoginDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    Guid                  UserId,
    string                FullName,
    string                Email,
    string                Role,
    Guid                  TenantId,
    string                Token,
    string?               PlanCode,
    IReadOnlyList<string> EnabledModules)
{
    /// <summary>Token opaco para renovar el access token sin re-autenticarse. Null para SuperAdmin.</summary>
    public string?   RefreshToken       { get; init; }
    public DateTime? RefreshTokenExpiry { get; init; }
}
