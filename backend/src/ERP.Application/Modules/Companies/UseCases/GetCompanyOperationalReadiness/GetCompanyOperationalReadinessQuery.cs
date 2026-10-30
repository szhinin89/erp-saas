using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: consulta de solo lectura del checklist de preparación operativa de
/// la empresa activa en la sucursal activa. Tenant/Company/Branch se resuelven del contexto
/// autenticado; <see cref="IBranchScopedRequest"/> hace que BranchScopeBehavior exija y valide la
/// sucursal con IBranchAccessGuard (sin sucursal seleccionada se rechaza, nunca se elige otra).
/// </summary>
public sealed record GetCompanyOperationalReadinessQuery
    : IRequest<Result<CompanyOperationalReadinessDto>>,
        IBranchScopedRequest;
