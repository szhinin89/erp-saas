using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateSystemUser;

/// <summary>
/// Único caso de uso autorizado para crear un <see cref="ERP.Domain.Access.Entities.IdentityUser"/>
/// fuera del flujo de First Run (<c>CreateInitialAdminCommand</c>). Crea el usuario y, en el mismo
/// flujo, delega en <see cref="ERP.Application.Access.UseCases.UpsertCompanyUserMembership.UpsertCompanyUserMembershipCommand"/>
/// (ya probado) para dar de alta su membership en la empresa activa — nunca reimplementa esa lógica.
/// </summary>
public sealed record CreateSystemUserCommand(
    Guid TenantId,
    Guid CompanyId,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string Password,
    string Role,
    Guid? ProfileId = null
) : IRequest<Result<CreateSystemUserResultDto>>;
