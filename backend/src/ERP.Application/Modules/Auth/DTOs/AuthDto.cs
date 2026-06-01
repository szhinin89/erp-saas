using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Auth.DTOs;

// ── Request DTOs (endpoints /auth/refresh y /auth/logout) ─────────────────

/// <param name="RefreshToken">Opcional si el cliente envía la cookie httpOnly <c>erp_refresh_token</c>.</param>
public record RefreshRequest(string? RefreshToken = null);

/// <param name="RefreshToken">Token opaco a revocar. Obligatorio.</param>
/// <param name="AllDevices">Si true, revoca todos los tokens del usuario (logout global).</param>
public record LogoutRequest(string? RefreshToken, bool AllDevices = false);

public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid SubscriberId
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
    Guid                  SubscriberId,
    string                Token,
    string?               PlanCode,
    IReadOnlyList<string> EnabledModules)
{
    /// <summary>Empresa operativa (RUC) activa. Null si el token no incluye contexto company.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// True cuando el usuario tiene múltiples empresas disponibles y debe seleccionar una
    /// antes de acceder al ERP. El frontend debe redirigir a /select-company.
    /// False cuando ya tiene contexto de empresa (un solo empresa o después de SwitchCompany).
    /// </summary>
    public bool RequiresCompanySelection { get; init; }

    /// <summary>Token opaco para renovar el access token sin re-autenticarse.</summary>
    public string?   RefreshToken       { get; init; }
    public DateTime? RefreshTokenExpiry { get; init; }

    /// <summary>IAM unificado: Platform | Company | Subscriber.</summary>
    public string? UserType { get; init; }

    /// <summary>Rol platform cuando <see cref="UserType"/> es Platform.</summary>
    public string? PlatformRole { get; init; }

    /// <summary>
    /// True cuando la empresa ha completado el wizard de onboarding (datos fiscales +
    /// infraestructura ERP). False si el onboarding sigue pendiente.
    /// Solo tiene valor cuando <see cref="CompanyId"/> no es null.
    /// El frontend usa este campo para decidir ERP vs /onboarding/company
    /// sin depender del 403 company_onboarding_required.
    /// </summary>
    public bool OnboardingCompleted { get; init; }

    /// <summary>
    /// Estado operativo de la empresa activa.
    /// Null cuando no hay empresa seleccionada (RequiresCompanySelection=true o platform operator).
    /// El frontend usa el enum directamente — no strings mágicos.
    /// </summary>
    public CompanyOperationalStatus? OperationalStatus { get; init; }
}
