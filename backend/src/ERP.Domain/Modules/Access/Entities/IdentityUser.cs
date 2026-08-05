using System.Text.RegularExpressions;
using ERP.Domain.Auth.ValueObjects;
using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Usuario global del sistema. Acceso operativo ERP vía <see cref="CompanyUserMembership"/>.
/// </summary>
public class IdentityUser : SystemAuditableEntity
{
    public string Username { get; private set; } = null!;
    public string UsernameNormalized { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;

    /// <summary>
    /// Dato de contacto — opcional (Fase G). No es identidad de login; algunas cuentas operativas
    /// (bodega, caja01, ventas01) no tienen buzón individual. Solo se usa hoy para el flujo
    /// ForgotPassword; nunca como clave de autenticación.
    /// </summary>
    public Email? Email { get; private set; }
    public string? EmailNormalized { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; }

    /// <summary>
    /// Legado: asociación directa usuario→subscriber para usuarios single-tenant.
    /// En arquitectura multi-empresa la relación canónica es:
    /// IdentityUser → CompanyUserMembership → Company → Subscriber.
    /// No usar en guards, policies ni handlers de autorización nuevos.
    /// </summary>
    [Obsolete(
        "Use CompanyUserMembership chain instead. Kept for backward compat with single-tenant user records."
    )]
    public Guid? TenantId { get; private set; }
    public string SecurityStamp { get; private set; } = null!;
    public bool RequirePasswordReset { get; private set; }

    private static readonly Regex UsernamePattern = new(
        @"^[a-z0-9](?:[a-z0-9._-]{1,48}[a-z0-9])?$",
        RegexOptions.Compiled
    );

    private IdentityUser() { }

    /// <summary>
    /// Username es la identidad de login (Fase G) — único, estable, independiente del email.
    /// Email es opcional: dato de contacto, nunca clave de autenticación.
    /// </summary>
    public static IdentityUser Create(
        string username,
        string firstName,
        string lastName,
        string? email,
        string passwordHash,
        Guid createdBy
    )
    {
        var normalizedUsername = NormalizeUsername(username);
        if (!UsernamePattern.IsMatch(normalizedUsername))
            throw new ArgumentException(
                "El username debe tener 3-50 caracteres, minúsculas/números/./_/-,  sin espacios ni '@'.",
                nameof(username)
            );

        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            Username = normalizedUsername,
            UsernameNormalized = normalizedUsername,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : new Email(NormalizeEmail(email)),
            EmailNormalized = string.IsNullOrWhiteSpace(email) ? null : NormalizeEmail(email),
            PasswordHash = passwordHash,
            IsActive = true,
#pragma warning disable CS0618 // backward compat: TenantId is kept for single-tenant legacy records
            TenantId = null,
#pragma warning restore CS0618
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        user.SetCreated(createdBy);
        return user;
    }

    public string FullName => $"{FirstName} {LastName}";

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        BumpSecurityStamp(updatedBy);
    }

    /// <summary>
    /// Cambia únicamente el hash de la contraseña. No decide el estado de
    /// <see cref="RequirePasswordReset"/> — esa es una decisión de negocio del caso de uso
    /// (ver <see cref="ClearRequirePasswordReset"/>/<see cref="MarkRequirePasswordReset"/>).
    /// </summary>
    public void SetPasswordHash(string passwordHash, Guid updatedBy)
    {
        PasswordHash = passwordHash;
        BumpSecurityStamp(updatedBy);
        SetUpdated(updatedBy);
    }

    public void MarkRequirePasswordReset(Guid updatedBy)
    {
        RequirePasswordReset = true;
        BumpSecurityStamp(updatedBy);
    }

    /// <summary>
    /// Usado exclusivamente por los flujos donde el propio usuario completa correctamente un
    /// cambio de contraseña (CompletePasswordReset, ResetPasswordWithToken, ChangeMyPassword) —
    /// nunca por un flujo administrativo que asigna una contraseña temporal en nombre de otro
    /// usuario (ese caso debe dejar el flag en true vía <see cref="MarkRequirePasswordReset"/>).
    /// </summary>
    public void ClearRequirePasswordReset(Guid updatedBy)
    {
        RequirePasswordReset = false;
        SetUpdated(updatedBy);
    }

    private void BumpSecurityStamp(Guid updatedBy)
    {
        SecurityStamp = Guid.NewGuid().ToString("N");
        SetUpdated(updatedBy);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
