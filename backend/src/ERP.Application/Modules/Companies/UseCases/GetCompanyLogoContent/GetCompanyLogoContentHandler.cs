using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Media;
using ERP.Domain.Modules.Media.Enums;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;

public sealed class GetCompanyLogoContentHandler : IRequestHandler<GetCompanyLogoContentQuery, Result<CompanyLogoContent>>
{
    private const string LogoRole = "logo";

    private readonly ICompanyAccessGuard _accessGuard;
    private readonly IMediaService _media;

    public GetCompanyLogoContentHandler(ICompanyAccessGuard accessGuard, IMediaService media)
    {
        _accessGuard = accessGuard;
        _media = media;
    }

    public async Task<Result<CompanyLogoContent>> Handle(GetCompanyLogoContentQuery request, CancellationToken cancellationToken)
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyLogoContent>.Failure(access.Error!);

        var logo = await _media.GetActivePrimaryAsync(
            access.Value!.TenantId, access.Value!.CompanyId, MediaOwnerType.Company, access.Value!.CompanyId, LogoRole, cancellationToken);

        if (logo is null)
            return Result<CompanyLogoContent>.NotFound("La empresa no tiene un logo configurado.");

        var stream = await _media.OpenContentAsync(logo, cancellationToken);
        if (stream is null)
            return Result<CompanyLogoContent>.NotFound("El archivo del logo no está disponible.");

        return Result<CompanyLogoContent>.Success(new CompanyLogoContent(stream, logo.ContentType, logo.FileName));
    }
}
