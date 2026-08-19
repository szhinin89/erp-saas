using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: escribe directamente en org_settings (scope Company, namespace
/// <see cref="OrgSettingKeys.CompanyBranding"/>) — ya no toca la entidad Company (el campo
/// BrandingConfiguration fue eliminado). Único punto de escritura de la marca de empresa.
/// </summary>
public sealed class UpdateCompanyBrandingHandler
    : IRequestHandler<UpdateCompanyBrandingCommand, Result<CompanyBrandingDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly IOrgSettingsRepository _orgSettingsRepository;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyBrandingHandler(
        ICompanyAccessGuard accessGuard,
        IOrgSettingsRepository orgSettingsRepository,
        ICurrentUser currentUser
    )
    {
        _accessGuard = accessGuard;
        _orgSettingsRepository = orgSettingsRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyBrandingDto>> Handle(
        UpdateCompanyBrandingCommand command,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyBrandingDto>.Failure(access.Error!);

        var tenantId = access.Value!.TenantId;
        var companyId = access.Value!.CompanyId;
        var userId = _currentUser.UserId;

        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.CompanyBranding.PrimaryColor,
            command.PrimaryColor,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.CompanyBranding.SecondaryColor,
            command.SecondaryColor,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.CompanyBranding.Slogan,
            command.Slogan,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.CompanyBranding.DocumentFooterText,
            command.DocumentFooterText,
            userId,
            cancellationToken
        );

        await _orgSettingsRepository.SaveChangesAsync(cancellationToken);

        return Result<CompanyBrandingDto>.Success(
            new CompanyBrandingDto(
                command.PrimaryColor,
                command.SecondaryColor,
                command.Slogan,
                command.DocumentFooterText
            )
        );
    }

    private async Task UpsertAsync(
        Guid tenantId,
        Guid companyId,
        string key,
        string? value,
        Guid updatedBy,
        CancellationToken ct
    )
    {
        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            key,
            value,
            SettingDataType.String,
            updatedBy
        );
        await _orgSettingsRepository.UpsertAsync(setting, ct);
    }
}
