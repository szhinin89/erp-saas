using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.GetCompanyEmailSettings;

public sealed class GetCompanyEmailSettingsQueryHandler
    : IRequestHandler<GetCompanyEmailSettingsQuery, Result<CommunicationEmailSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICommunicationSettingsResolver _settingsResolver;

    public GetCompanyEmailSettingsQueryHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICommunicationSettingsResolver settingsResolver
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _settingsResolver = settingsResolver;
    }

    public async Task<Result<CommunicationEmailSettingsDto>> Handle(
        GetCompanyEmailSettingsQuery request,
        CancellationToken cancellationToken
    )
    {
        var companyId = _currentCompany.CompanyId;

        var stored = await _repo.GetAllForScopeAsync(
            _currentTenant.TenantId,
            companyId,
            OrgScope.Company,
            companyId,
            cancellationToken
        );
        var storedKeys = stored.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var hasOwnSettings = CommunicationsEmailKeys.All.Any(storedKeys.Contains);

        var effective = await _settingsResolver.ResolveEmailAsync(cancellationToken);

        return Result<CommunicationEmailSettingsDto>.Success(
            new CommunicationEmailSettingsDto(
                Enabled: effective.Enabled,
                SmtpHost: effective.SmtpHost,
                SmtpPort: effective.SmtpPort > 0 ? effective.SmtpPort : null,
                SmtpUsername: effective.SmtpUsername,
                PasswordConfigured: !string.IsNullOrWhiteSpace(effective.SmtpPassword),
                SenderEmail: effective.SenderEmail,
                SenderName: effective.SenderName,
                UseSsl: effective.UseSsl,
                ReplyToEmail: effective.ReplyToEmail,
                MaxRetries: effective.MaxRetries,
                DefaultLanguage: effective.DefaultLanguage,
                Source: hasOwnSettings ? "OrgSettings" : "EnvironmentFallback"
            )
        );
    }
}

/// <summary>Todas las keys de OrgSettings del namespace Communications.Email — usado solo para detectar si la empresa ya tiene configuración propia guardada.</summary>
internal static class CommunicationsEmailKeys
{
    public static readonly IReadOnlyCollection<string> All =
    [
        OrgSettingKeys.Communications.EmailEnabled,
        OrgSettingKeys.Communications.SmtpHost,
        OrgSettingKeys.Communications.SmtpPort,
        OrgSettingKeys.Communications.SmtpUsername,
        OrgSettingKeys.Communications.SmtpPassword,
        OrgSettingKeys.Communications.SenderEmail,
        OrgSettingKeys.Communications.SenderName,
        OrgSettingKeys.Communications.UseSsl,
        OrgSettingKeys.Communications.ReplyToEmail,
        OrgSettingKeys.Communications.MaxRetries,
        OrgSettingKeys.Communications.DefaultLanguage,
    ];
}
