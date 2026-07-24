using ERP.Domain.Access.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Sesión de sistema de un <see cref="IdentityUser"/> en una empresa y sucursal, iniciada tras
/// completar el login. Independiente de <c>CashSession</c> (Caja) — cerrar una UserSession nunca
/// cierra la caja asociada, y viceversa.
/// </summary>
public sealed class UserSession : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int TerminalIdMaxLen = 200;

    public Guid CompanyId { get; private set; }
    public Guid IdentityUserId { get; private set; }
    public Guid BranchId { get; private set; }
    public string TerminalId { get; private set; } = null!;
    public Guid? RefreshTokenId { get; private set; }

    public UserSessionStatus Status { get; private set; } = UserSessionStatus.Active;

    public DateTime StartedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? ClosedReason { get; private set; }

    public bool IsActive => Status == UserSessionStatus.Active;

    private UserSession() { }

    public static UserSession Create(
        Guid tenantId,
        Guid companyId,
        Guid identityUserId,
        Guid branchId,
        string terminalId,
        Guid? refreshTokenId = null)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (identityUserId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(identityUserId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(terminalId))
            throw new ArgumentException("El terminal es obligatorio.", nameof(terminalId));
        if (terminalId.Length > TerminalIdMaxLen)
            throw new ArgumentException($"El terminal no puede superar {TerminalIdMaxLen} caracteres.", nameof(terminalId));

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            IdentityUserId = identityUserId,
            BranchId = branchId,
            TerminalId = terminalId.Trim(),
            RefreshTokenId = refreshTokenId,
            Status = UserSessionStatus.Active,
            StartedAt = DateTime.UtcNow,
        };
        session.SetCreated(identityUserId);

        return session;
    }

    /// <summary>Cierre explícito por el usuario (logout). Idempotente.</summary>
    public void CloseManually(Guid closedBy)
    {
        Close(UserSessionStatus.ClosedManually, "Cierre manual (logout).", closedBy);
    }

    /// <summary>Cierre automático al detectar un nuevo login del mismo usuario en la misma empresa. Idempotente.</summary>
    public void CloseByNewLogin(Guid closedBy)
    {
        Close(UserSessionStatus.ClosedByNewLogin, "Cerrada automáticamente por un nuevo inicio de sesión.", closedBy);
    }

    /// <summary>
    /// Cierre pasivo por política de expiración (Fase 9 — ExpireUserSessionsCommand). Distinto
    /// semánticamente de CloseManually (acción del usuario) y CloseByNewLogin (nuevo login) —
    /// por eso no se reutiliza ninguno de los dos; UserSessionStatus.Expired ya existía en el
    /// enum desde su diseño original específicamente para este caso, sin usar hasta ahora.
    /// Idempotente. closedBy = Guid.Empty para el actor de sistema/job, mismo convenio ya usado
    /// por ElectronicDocumentRetryJob.
    /// </summary>
    public void MarkExpired(Guid closedBy)
    {
        Close(UserSessionStatus.Expired, "Expirada por política de inactividad.", closedBy);
    }

    private void Close(UserSessionStatus reason, string closedReason, Guid closedBy)
    {
        if (Status != UserSessionStatus.Active)
            return;

        Status = reason;
        ClosedAt = DateTime.UtcNow;
        ClosedReason = closedReason;
        SetUpdated(closedBy);
    }
}
