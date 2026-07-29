using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Media;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Media.Enums;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UploadCompanyLogoAlt;

public sealed class UploadCompanyLogoAltHandler
    : IRequestHandler<UploadCompanyLogoAltCommand, Result<CompanyProfileDto>>
{
    private const string LogoRole = "logo";
    private const string AlternateLogoRole = "logo-alt";

    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly IMediaService _media;
    private readonly ICurrentUser _currentUser;

    public UploadCompanyLogoAltHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        IMediaService media,
        ICurrentUser currentUser
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _media = media;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyProfileDto>> Handle(
        UploadCompanyLogoAltCommand request,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyProfileDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(access.Value!.CompanyId, cancellationToken);
        if (company is null)
            return Result<CompanyProfileDto>.Failure("Empresa no encontrada.");

        var uploadResult = await _media.ReplacePrimaryImageAsync(
            new ReplacePrimaryMediaRequest(
                TenantId: access.Value!.TenantId,
                CompanyId: access.Value!.CompanyId,
                OwnerType: MediaOwnerType.Company,
                OwnerId: access.Value!.CompanyId,
                Role: AlternateLogoRole,
                MediaType: MediaType.Image,
                Visibility: MediaVisibility.TenantOnly,
                Content: request.File,
                UserId: _currentUser.UserId
            ),
            cancellationToken
        );

        if (!uploadResult.IsSuccess)
            return Result<CompanyProfileDto>.Failure(uploadResult.Error!, uploadResult.Code);

        var logo = await _media.GetActivePrimaryAsync(
            access.Value!.TenantId,
            access.Value!.CompanyId,
            MediaOwnerType.Company,
            access.Value!.CompanyId,
            LogoRole,
            cancellationToken
        );

        return Result<CompanyProfileDto>.Success(
            CompanyProfileDto.FromEntity(company, logo, uploadResult.Value!)
        );
    }
}
