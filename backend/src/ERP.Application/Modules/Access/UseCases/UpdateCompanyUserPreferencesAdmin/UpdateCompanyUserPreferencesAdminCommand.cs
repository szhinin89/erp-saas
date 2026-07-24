using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;

/// <summary>
/// Fase F — punto de entrada administrativo de escritura. Delega íntegramente en
/// <see cref="ERP.Application.Access.UseCases.UpdateCompanyUserPreferences.UpdateCompanyUserPreferencesCommand"/>
/// (Fase C) — nunca reimplementa la validación de LoginMode/DefaultBranchId ni la autorización
/// de sucursal (CompanyUserBranch sigue siendo su única fuente). El único agregado propio es la
/// verificación de que la membresía pertenezca a la empresa operativa actual del administrador.
/// Fase S1 (hardening): agrega <see cref="IRequiresCompanyContext"/> — mismo motivo que
/// GetCompanyUserPreferencesAdminQuery: el chequeo manual por sí solo confiaba en
/// <c>ICurrentCompany.CompanyId</c> (header <c>X-Company-Id</c>, no un claim firmado) sin pasar
/// por ICompanyAccessGuard. El marker fuerza esa revalidación antes del handler.
/// </summary>
public sealed record UpdateCompanyUserPreferencesAdminCommand(
    Guid CompanyUserId,
    string LoginMode,
    Guid? DefaultBranchId
) : IRequest<Result<CompanyUserPreferencesAdminDto>>, IRequiresCompanyContext;
