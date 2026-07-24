using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;

/// <summary>
/// Fase I-A — envoltorio administrativo de
/// <see cref="ERP.Application.Access.UseCases.UpsertCompanyUserMembership.UpsertCompanyUserMembershipCommand"/>
/// (Fase D, ya probado). Nunca acepta TenantId/CompanyId del cliente: <see cref="UpsertCompanyUserMembershipAdminHandler"/>
/// los resuelve del contexto autenticado (ICurrentTenant/ICurrentCompany) antes de delegar — mismo
/// principio que GetCompanyUserPreferencesAdmin/UpdateCompanyUserPreferencesAdmin (Fase F), que
/// resuelven la empresa activa antes de reenviar al UseCase real sin reimplementar su validación.
/// <see cref="IRequiresCompanyContext"/>: exige empresa operativa activa vía CompanyScopeBehavior
/// antes de que el handler se ejecute (mismo mecanismo que CloseUserSessionAdminCommand).
/// </summary>
public sealed record UpsertCompanyUserMembershipAdminCommand(
    string Username,
    string Role,
    Guid? ProfileId = null,
    IReadOnlyList<Guid>? AuthorizedBranchIds = null,
    Guid? DefaultBranchId = null,
    string? LoginMode = null
) : IRequest<Result<object>>, IRequiresCompanyContext;
