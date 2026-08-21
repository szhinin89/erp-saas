using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.UpdateCompanyEmailSettings;

public sealed class UpdateCompanyEmailSettingsCommandHandler
    : IRequestHandler<UpdateCompanyEmailSettingsCommand, Result<CommunicationEmailSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretProtector _secretProtector;

    public UpdateCompanyEmailSettingsCommandHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ISecretProtector secretProtector
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _secretProtector = secretProtector;
    }

    public async Task<Result<CommunicationEmailSettingsDto>> Handle(
        UpdateCompanyEmailSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        var existingPassword = await _repo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Communications.SmtpPassword,
            cancellationToken
        );
        var hasNewPassword = !string.IsNullOrWhiteSpace(command.SmtpPassword);
        var passwordConfigured = hasNewPassword || existingPassword?.Value is not null;

        if (command.Enabled && !passwordConfigured)
        {
            return Result<CommunicationEmailSettingsDto>.ValidationFailure(
                "La contraseña SMTP es obligatoria para activar el envío de correos."
            );
        }

        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.EmailEnabled,
            command.Enabled.ToString(),
            SettingDataType.Bool,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.SmtpHost,
            command.SmtpHost,
            SettingDataType.String,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.SmtpPort,
            command.SmtpPort?.ToString(),
            SettingDataType.Int,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.SmtpUsername,
            command.SmtpUsername,
            SettingDataType.String,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.SenderEmail,
            command.SenderEmail,
            SettingDataType.String,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.SenderName,
            command.SenderName,
            SettingDataType.String,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.UseSsl,
            command.UseSsl.ToString(),
            SettingDataType.Bool,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.ReplyToEmail,
            command.ReplyToEmail,
            SettingDataType.String,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.MaxRetries,
            (command.MaxRetries ?? 3).ToString(),
            SettingDataType.Int,
            userId,
            cancellationToken
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Communications.DefaultLanguage,
            string.IsNullOrWhiteSpace(command.DefaultLanguage) ? "es" : command.DefaultLanguage,
            SettingDataType.String,
            userId,
            cancellationToken
        );

        // Nunca se sobrescribe con un valor vacío: si no se envía password nueva, la fila
        // existente (o su ausencia) queda intacta — ver nota en UpdateCompanyEmailSettingsCommand.
        if (hasNewPassword)
        {
            await UpsertAsync(
                tenantId,
                companyId,
                OrgSettingKeys.Communications.SmtpPassword,
                _secretProtector.Protect(command.SmtpPassword!),
                SettingDataType.String,
                userId,
                cancellationToken
            );
        }

        await _repo.SaveChangesAsync(cancellationToken);

        return Result<CommunicationEmailSettingsDto>.Success(
            new CommunicationEmailSettingsDto(
                Enabled: command.Enabled,
                SmtpHost: command.SmtpHost,
                SmtpPort: command.SmtpPort,
                SmtpUsername: command.SmtpUsername,
                PasswordConfigured: passwordConfigured,
                SenderEmail: command.SenderEmail,
                SenderName: command.SenderName,
                UseSsl: command.UseSsl,
                ReplyToEmail: command.ReplyToEmail,
                MaxRetries: command.MaxRetries ?? 3,
                DefaultLanguage: string.IsNullOrWhiteSpace(command.DefaultLanguage)
                    ? "es"
                    : command.DefaultLanguage,
                Source: "OrgSettings"
            )
        );
    }

    private async Task UpsertAsync(
        Guid tenantId,
        Guid companyId,
        string key,
        string? value,
        SettingDataType dataType,
        Guid userId,
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
            dataType,
            userId
        );

        await _repo.UpsertAsync(setting, ct);
    }
}
