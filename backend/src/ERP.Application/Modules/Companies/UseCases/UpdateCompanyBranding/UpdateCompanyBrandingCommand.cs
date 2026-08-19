using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: campos tipados — ya no un JSON crudo. Cada campo es opcional; un
/// valor vacío/null lo borra (vuelve a "sin configurar"), nunca lo deja en un valor previo
/// invisible al usuario.
/// </summary>
public sealed record UpdateCompanyBrandingCommand(
    string? PrimaryColor,
    string? SecondaryColor,
    string? Slogan,
    string? DocumentFooterText
) : IRequest<Result<CompanyBrandingDto>>;
