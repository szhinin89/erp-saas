using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

/// <summary>
/// Fase D: además de la membresía, acepta opcionalmente autorizar sucursales
/// (<see cref="AuthorizedBranchIds"/> — única escritura de producción de CompanyUserBranch) y
/// configurar las preferencias operativas del usuario (<see cref="DefaultBranchId"/>/
/// <see cref="LoginMode"/>), delegando siempre en los UseCases ya existentes de
/// CompanyUserPreferences (nunca reimplementa su validación). Si no se informan, las
/// preferencias se crean con el default AskBranch + sin sucursal — nunca se deja una membresía
/// nueva sin fila de preferencias.
/// </summary>
public record UpsertCompanyUserMembershipCommand(
    Guid TenantId,
    string Username,
    string Role,
    Guid? ProfileId = null,
    IReadOnlyList<Guid>? AuthorizedBranchIds = null,
    Guid? DefaultBranchId = null,
    string? LoginMode = null
) : IRequest<Result<object>>;

