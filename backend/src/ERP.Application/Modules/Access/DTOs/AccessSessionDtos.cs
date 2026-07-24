namespace ERP.Application.Access.DTOs;

/// <summary>
/// Proyección de solo lectura de <c>UserSession</c> — nunca se expone la entidad de dominio
/// hacia API. No incluye datos de Branch/Company/CashSession (fuentes de verdad ajenas).
/// </summary>
public sealed record UserSessionDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid IdentityUserId,
    Guid BranchId,
    string TerminalId,
    string Status,
    DateTime StartedAt,
    DateTime? ClosedAt,
    string? ClosedReason);

/// <summary>
/// Resultado de CreateAuthenticatedSessionCommand (Fase 6) — sesión creada + refresh token en
/// texto plano (para que un futuro handler de login lo entregue vía cookie httpOnly, fuera de
/// alcance aquí). No incluye JWT — la emisión de access token pertenece a Fase 7/8.
/// </summary>
public sealed record AuthenticatedSessionDto(
    UserSessionDto Session,
    string RefreshToken,
    DateTime RefreshTokenExpiry);

/// <summary>
/// Proyección administrativa para GetUserSessionsPagedQuery (Fase 10) — expone exactamente los
/// campos autorizados por el diseño (SessionId, IdentityUserId, CompanyId, BranchId, TerminalId,
/// Status, CreatedAt, UpdatedAt). Nunca RefreshTokenId, tokens ni secretos.
/// </summary>
public sealed record UserSessionAdminDto(
    Guid SessionId,
    Guid IdentityUserId,
    Guid CompanyId,
    Guid BranchId,
    string TerminalId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Métricas básicas de GetSessionStatisticsQuery (Fase 10) — solo conteos, sin PII.</summary>
public sealed record SessionStatisticsDto(
    int Active,
    int ClosedManually,
    int ClosedByNewLogin,
    int Expired,
    int Total);
