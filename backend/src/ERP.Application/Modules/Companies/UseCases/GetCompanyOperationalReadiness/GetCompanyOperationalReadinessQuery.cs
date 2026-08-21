using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: consulta de solo lectura del checklist de preparación operativa de
/// la empresa activa. Tenant/Company se resuelven del contexto autenticado.
/// </summary>
public sealed record GetCompanyOperationalReadinessQuery
    : IRequest<Result<CompanyOperationalReadinessDto>>,
        ICompanyScopedRequest;
