using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyBranding;

public sealed class GetCompanyBrandingQueryHandler
    : IRequestHandler<GetCompanyBrandingQuery, Result<CompanyBrandingDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyBrandingResolver _brandingResolver;

    public GetCompanyBrandingQueryHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyBrandingResolver brandingResolver
    )
    {
        _accessGuard = accessGuard;
        _brandingResolver = brandingResolver;
    }

    public async Task<Result<CompanyBrandingDto>> Handle(
        GetCompanyBrandingQuery request,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyBrandingDto>.Failure(access.Error!);

        var settings = await _brandingResolver.GetAsync(
            access.Value!.TenantId,
            access.Value!.CompanyId,
            cancellationToken
        );

        return Result<CompanyBrandingDto>.Success(
            new CompanyBrandingDto(
                settings.PrimaryColor,
                settings.SecondaryColor,
                settings.Slogan,
                settings.DocumentFooterText
            )
        );
    }
}
