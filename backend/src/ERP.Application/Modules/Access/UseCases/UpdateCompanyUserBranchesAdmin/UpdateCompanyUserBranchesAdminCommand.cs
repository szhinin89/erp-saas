using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;

/// <summary>
/// Fase I-B — reemplazo todo-o-nada de las sucursales autorizadas de una CompanyUserMembership.
/// CompanyUserMembershipId nunca viaja fuera de la ruta del controller — se recibe aquí ya
/// resuelto, no hay TenantId/CompanyId en el contrato. Lista vacía es un valor válido (ver
/// UpdateCompanyUserBranchesAdminHandler): revoca todas las autorizaciones sin desactivar la
/// membresía — decisión documentada en docs/STATUS.md.
/// Fase S1 (hardening): agrega <see cref="IRequiresCompanyContext"/> — mismo motivo que
/// GetCompanyUserPreferencesAdminQuery: el chequeo manual contra <c>ICurrentCompany.CompanyId</c>
/// (header <c>X-Company-Id</c>) no pasaba por ICompanyAccessGuard. El marker fuerza esa
/// revalidación antes del handler.
/// </summary>
public sealed record UpdateCompanyUserBranchesAdminCommand(
    Guid CompanyUserMembershipId,
    IReadOnlyList<Guid> AuthorizedBranchIds
) : IRequest<Result<CompanyUserBranchesAdminDto>>, IRequiresCompanyContext;
