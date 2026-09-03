using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.ReturnToGlobal;

/// <summary>
/// AdminGlobalCore: vuelve de una sesión operativa (emitida por operate-company) a la sesión
/// global. Solo funciona si la sesión actual trae los claims <c>operator_mode</c>/
/// <c>global_admin_user_id</c> — ver <see cref="ReturnToGlobalHandler"/>.
/// </summary>
public sealed record ReturnToGlobalCommand : IRequest<Result<AuthResponseDto>>;
