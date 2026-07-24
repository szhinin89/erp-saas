using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Auth.DTOs;

// ── Request DTOs (endpoints /auth/refresh y /auth/logout) ─────────────────

/// <param name="RefreshToken">Opcional si el cliente envía la cookie httpOnly <c>erp_refresh_token</c>.</param>
public record RefreshRequest(string? RefreshToken = null);

/// <param name="RefreshToken">Token opaco a revocar. Obligatorio.</param>
/// <param name="AllDevices">Si true, revoca todos los tokens del usuario (logout global).</param>
public record LogoutRequest(string? RefreshToken, bool AllDevices = false);

public record LoginDto(
    string Username,
    string Password
);

public record AuthResponseDto(
    Guid    UserId,
    string  FullName,
    string  Username,
    string? Email,
    string  Role,
    Guid    TenantId,
    string  Token)
{
    /// <summary>Empresa operativa activa. Null si RequiresCompanySelection=true.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// True cuando el usuario tiene múltiples empresas y debe seleccionar una antes de acceder al ERP.
    /// El frontend redirige a /select-company.
    /// </summary>
    public bool RequiresCompanySelection { get; init; }

    public string?   RefreshToken       { get; init; }
    public DateTime? RefreshTokenExpiry { get; init; }

    public bool OnboardingCompleted { get; init; }

    public CompanyOperationalStatus? OperationalStatus { get; init; }

    /// <summary>
    /// Fase H — true cuando IdentityUser.RequirePasswordReset está activo: Token viene vacío (no
    /// se emite JWT), el cliente debe llamar POST /auth/complete-password-reset con
    /// PasswordResetToken antes de obtener una sesión real.
    /// </summary>
    public bool RequiresPasswordReset { get; init; }

    /// <summary>Token opaco de un solo uso, solo presente cuando RequiresPasswordReset=true.</summary>
    public string? PasswordResetToken { get; init; }

    /// <summary>Segundos de vigencia de PasswordResetToken desde esta respuesta.</summary>
    public int? PasswordResetTokenExpiresIn { get; init; }
}
