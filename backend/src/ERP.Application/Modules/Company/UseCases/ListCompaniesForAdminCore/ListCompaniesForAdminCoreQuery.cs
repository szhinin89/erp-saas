using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.ListCompaniesForAdminCore;

/// <summary>
/// AdminGlobalCore: lista todas las empresas de todos los tenants para el dashboard global.
/// Deliberadamente sin <c>ITenantScopedRequest</c>/<c>ICompanyScopedRequest</c> — es cross-tenant
/// por diseño, igual que <c>GlobalLoginCommand</c>. Solo accesible vía policy "PlatformAdmin".
/// </summary>
public sealed record ListCompaniesForAdminCoreQuery
    : IRequest<Result<IReadOnlyList<AdminCoreCompanyDto>>>;
