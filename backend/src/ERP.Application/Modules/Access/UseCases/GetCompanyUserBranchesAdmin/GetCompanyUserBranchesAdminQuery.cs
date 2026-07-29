using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;

/// <summary>
/// Fase I-B — lectura administrativa de sucursales autorizadas de una CompanyUserMembership.
/// CompanyUserId identifica la membresía (mismo nombre de parámetro que
/// GetCompanyUserPreferencesAdminQuery, por consistencia de contrato entre los dos endpoints
/// admin que cuelgan de la misma ruta /admin/iam/memberships/{membershipId}/...).
/// Fase S1 (hardening): agrega <see cref="IRequiresCompanyContext"/> — mismo motivo que
/// GetCompanyUserPreferencesAdminQuery: el chequeo manual contra <c>ICurrentCompany.CompanyId</c>
/// (header <c>X-Company-Id</c>) no pasaba por ICompanyAccessGuard. El marker fuerza esa
/// revalidación antes del handler.
/// </summary>
public sealed record GetCompanyUserBranchesAdminQuery(Guid CompanyUserId)
    : IRequest<Result<CompanyUserBranchesAdminDto?>>,
        IRequiresCompanyContext;
