using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.CloseUserSessionAdmin;

/// <summary>
/// Cierre administrativo de una sesión específica (Fase 10): el SessionId es explícito y puede
/// pertenecer a otro usuario, por eso requiere permiso dedicado (access.sessions.close) en vez
/// de operar solo sobre el contexto ambiente del actor. (El equivalente self-service de Fase 8,
/// CloseUserSessionCommand, se eliminó en la Fase 12 por no tener consumidores reales — ver
/// docs/IDENTITY.md#usersession-contexto-operativo-del-usuario.)
/// ICompanyScopedRequest: el actor solo puede alcanzar sesiones de su empresa activa — el
/// filtro automático de ICompanyOperationalEntity en GetByIdAsync ya lo garantiza sin
/// validación manual adicional.
/// </summary>
public sealed record CloseUserSessionAdminCommand(Guid SessionId)
    : IRequest<Result<string>>, ICompanyScopedRequest;
