namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Punto único para invalidar todo el acceso vivo de un <c>IdentityUser</c> (refresh tokens +
/// UserSession activas). Compone <see cref="IRefreshTokenService.RevokeAllForUserAsync"/> (ya
/// existente) y el cierre de sesiones activas — antes de esta interfaz ambas piezas se invocaban
/// por separado y ningún caller cerraba sesiones en bloque (ver comentario histórico en
/// CloseUserSessionAdminHandler: "no existe flujo aprobado de revocación por Id"). Cualquier caso
/// de uso administrativo futuro que necesite forzar un logout global (asignar contraseña
/// temporal, desactivar usuario, etc.) debe reutilizar este servicio en vez de reimplementar el
/// loop de cierre de sesiones.
/// </summary>
public interface IUserAccessRevocationService
{
    /// <summary>
    /// Revoca todos los refresh tokens activos del usuario y cierra (CloseManually) todas sus
    /// UserSession activas en el tenant. Idempotente: si no hay tokens/sesiones activas, no hace
    /// nada.
    /// </summary>
    /// <param name="userId">Usuario cuyo acceso se revoca.</param>
    /// <param name="tenantId">Tenant sobre el que se revoca (cruza todas las empresas del tenant).</param>
    /// <param name="actorId">
    /// Quien ejecuta la revocación (el propio usuario en self-service, o el administrador en un
    /// flujo administrativo) — queda registrado como <c>UpdatedBy</c> de cada UserSession cerrada,
    /// igual criterio que <c>CloseUserSessionAdminHandler</c>.
    /// </param>
    /// <param name="reason">Motivo persistido en cada RefreshToken revocado.</param>
    /// <param name="cancellationToken"></param>
    Task RevokeAllAccessAsync(
        Guid userId,
        Guid tenantId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default
    );
}
