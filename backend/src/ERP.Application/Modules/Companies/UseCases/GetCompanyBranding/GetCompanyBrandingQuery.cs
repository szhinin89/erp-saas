using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyBranding;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: lee la marca de empresa (colores/eslogan/pie de página) desde su
/// fuente única (org_settings vía ICompanyBrandingResolver) — separado de GetCompanyProfileQuery
/// porque ya no es un campo del perfil (JSON), es su propio recurso tipado.
/// </summary>
public sealed record GetCompanyBrandingQuery : IRequest<Result<CompanyBrandingDto>>;
