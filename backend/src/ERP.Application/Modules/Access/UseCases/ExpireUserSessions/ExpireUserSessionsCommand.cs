using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.ExpireUserSessions;

/// <summary>
/// Limpieza pasiva de sesiones (Fase 9) — cierra (Expired) las UserSession Active más antiguas
/// que la política configurada (SessionExpirationOptions). Sin parámetros: opera cross-tenant
/// por diseño (job de sistema, no una operación de un usuario/empresa específicos).
/// Devuelve la cantidad de sesiones cerradas, para que el job la registre en el log.
/// </summary>
public sealed record ExpireUserSessionsCommand : IRequest<Result<int>>;
