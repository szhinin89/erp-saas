using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;

/// <summary>
/// Fase F — punto de entrada administrativo de solo lectura. Delega íntegramente en
/// <see cref="ERP.Application.Access.UseCases.GetCompanyUserPreferences.GetCompanyUserPreferencesQuery"/>
/// (Fase C); el único agregado propio de este caso de uso es verificar que la membresía
/// consultada pertenezca a la empresa operativa actual del administrador (aislamiento
/// multi-tenant que la Query de Fase C no puede aplicar porque también la usa el login, antes de
/// que exista un contexto de empresa confiable — ver CompanyUserPreferencesLoginResolver).
/// Fase S1 (hardening): agrega <see cref="IRequiresCompanyContext"/> — el chequeo manual de
/// arriba comparaba contra <c>ICurrentCompany.CompanyId</c>, que viene del header
/// <c>X-Company-Id</c> (no de un claim firmado), y un caller con rol Admin obtiene el permiso
/// sin que nada revalide que esa empresa pertenezca a su tenant ni que tenga membership ahí. El
/// marker fuerza CompanyScopeBehavior → ICompanyAccessGuard.RequireCurrentCompanyAsync antes del
/// handler, cerrando esa vía — mismo mecanismo que UpsertCompanyUserMembershipAdminCommand.
/// </summary>
public sealed record GetCompanyUserPreferencesAdminQuery(Guid CompanyUserId)
    : IRequest<Result<CompanyUserPreferencesAdminDto?>>,
        IRequiresCompanyContext;
