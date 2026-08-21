using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Communications;

public sealed class CommunicationSettingsResolver : ICommunicationSettingsResolver
{
    private readonly IOrgConfigResolver _orgConfig;
    private readonly IConfiguration _configuration;

    public CommunicationSettingsResolver(IOrgConfigResolver orgConfig, IConfiguration configuration)
    {
        _orgConfig = orgConfig;
        _configuration = configuration;
    }

    public async Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default)
    {
        var companyScopeId = Guid.Empty;

        var enabled = await GetBoolAsync(OrgSettingKeys.Communications.EmailEnabled, ct)
            ?? _configuration.GetValue("Communications:Email:Enabled", false);
        var host = await GetStringAsync(OrgSettingKeys.Communications.SmtpHost, ct)
            ?? _configuration["Communications:Email:SmtpHost"];
        var port = await GetIntAsync(OrgSettingKeys.Communications.SmtpPort, ct)
            ?? _configuration.GetValue("Communications:Email:SmtpPort", 587);
        var username = await GetStringAsync(OrgSettingKeys.Communications.SmtpUsername, ct)
            ?? _configuration["Communications:Email:SmtpUsername"];
        var password = await GetStringAsync(OrgSettingKeys.Communications.SmtpPassword, ct)
            ?? _configuration["Communications:Email:SmtpPassword"];
        var senderEmail = await GetStringAsync(OrgSettingKeys.Communications.SenderEmail, ct)
            ?? _configuration["Communications:Email:SenderEmail"];
        var senderName = await GetStringAsync(OrgSettingKeys.Communications.SenderName, ct)
            ?? _configuration["Communications:Email:SenderName"];
        var useSsl = await GetBoolAsync(OrgSettingKeys.Communications.UseSsl, ct)
            ?? _configuration.GetValue("Communications:Email:UseSsl", true);
        var replyTo = await GetStringAsync(OrgSettingKeys.Communications.ReplyToEmail, ct)
            ?? _configuration["Communications:Email:ReplyToEmail"];
        var maxRetries = await GetIntAsync(OrgSettingKeys.Communications.MaxRetries, ct)
            ?? _configuration.GetValue("Communications:Email:MaxRetries", 3);
        var language = await GetStringAsync(OrgSettingKeys.Communications.DefaultLanguage, ct)
            ?? _configuration["Communications:Email:DefaultLanguage"]
            ?? "es";

        _ = companyScopeId;
        return new CommunicationEmailSettings(
            enabled,
            host,
            port,
            username,
            password,
            senderEmail,
            senderName,
            useSsl,
            replyTo,
            maxRetries,
            language
        );
    }

    private Task<string?> GetStringAsync(string key, CancellationToken ct) =>
        _orgConfig.GetValueAsync(OrgScope.Company, Guid.Empty, key, ct);

    private async Task<int?> GetIntAsync(string key, CancellationToken ct) =>
        await _orgConfig.GetValueAsync<int>(OrgScope.Company, Guid.Empty, key, ct);

    private async Task<bool?> GetBoolAsync(string key, CancellationToken ct) =>
        await _orgConfig.GetValueAsync<bool>(OrgScope.Company, Guid.Empty, key, ct);
}

