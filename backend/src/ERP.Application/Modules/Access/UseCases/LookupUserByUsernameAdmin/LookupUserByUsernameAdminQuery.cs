using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;

/// <summary>
/// Fase F (renombrado en Fase G) — único endpoint de solo lectura que responde "¿este username ya
/// es un IdentityUser? ¿ya tiene membership en la empresa activa? ¿está activa?", para que el
/// frontend decida automáticamente entre UpsertCompanyUserMembershipCommand (usuario existente) y
/// CreateSystemUserCommand (usuario nuevo) sin que el administrador tenga que saberlo de antemano.
/// No reimplementa ninguna regla: compone IAccessRepository.GetUserByUsernameAsync +
/// GetCompanyUserMembershipAsync, ambos ya usados por otros UseCases.
/// </summary>
public sealed record LookupUserByUsernameAdminQuery(string Username)
    : IRequest<Result<UsernameLookupDto>>,
        IRequiresCompanyContext;
