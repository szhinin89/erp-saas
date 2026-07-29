using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Media;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Media.Enums;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;

public sealed class GetCompanyProfileHandler
    : IRequestHandler<GetCompanyProfileQuery, Result<CompanyProfileDto>>
{
    private const string LogoRole = "logo";
    private const string AlternateLogoRole = "logo-alt";

    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly IMediaService _media;

    public GetCompanyProfileHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        IMediaService media
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _media = media;
    }

    public async Task<Result<CompanyProfileDto>> Handle(
        GetCompanyProfileQuery request,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyProfileDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(access.Value!.CompanyId, cancellationToken);
        if (company is null)
            return Result<CompanyProfileDto>.Failure("Empresa no encontrada.");

        var logo = await _media.GetActivePrimaryAsync(
            access.Value!.TenantId,
            access.Value!.CompanyId,
            MediaOwnerType.Company,
            access.Value!.CompanyId,
            LogoRole,
            cancellationToken
        );

        var alternateLogo = await _media.GetActivePrimaryAsync(
            access.Value!.TenantId,
            access.Value!.CompanyId,
            MediaOwnerType.Company,
            access.Value!.CompanyId,
            AlternateLogoRole,
            cancellationToken
        );

        return Result<CompanyProfileDto>.Success(
            CompanyProfileDto.FromEntity(company, logo, alternateLogo)
        );
    }
}
