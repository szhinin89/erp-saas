using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A — envoltorio administrativo de
/// <see cref="ERP.Application.Access.UseCases.RevokeCompanyUserMembership.RevokeCompanyUserMembershipCommand"/>
/// (Fase D, ya probado). Nunca acepta TenantId/CompanyId del cliente — se resuelven del contexto
/// autenticado en <see cref="RevokeCompanyUserMembershipAdminHandler"/>. <see cref="IRequiresCompanyContext"/>
/// exige empresa operativa activa vía CompanyScopeBehavior antes de ejecutar el handler.
/// </summary>
public sealed record RevokeCompanyUserMembershipAdminCommand(
    string Username
) : IRequest<Result<object>>, IRequiresCompanyContext;
