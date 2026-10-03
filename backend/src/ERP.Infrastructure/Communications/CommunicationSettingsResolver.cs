using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Communications;

public sealed class CommunicationSettingsResolver : ICommunicationSettingsResolver
{
    private readonly IOrgConfigResolver _orgConfig;
    private readonly IConfiguration _configuration;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISecretProtector _secretProtector;

    public CommunicationSettingsResolver(
        IOrgConfigResolver orgConfig,
        IConfiguration configuration,
        ICurrentCompany currentCompany,
        ISecretProtector secretProtector
    )
    {
        _orgConfig = orgConfig;
        _configuration = configuration;
        _currentCompany = currentCompany;
        _secretProtector = secretProtector;
    }

    public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) =>
        ResolveAsync(_currentCompany.CompanyId, ct);

    /// <summary>
    /// ZH-COMMUNICATIONS-CONTRACT-01 — alcance explícito: Company lee OrgSettings de ESA empresa (más
    /// el fallback de instancia vigente); System lee solo la configuración de instancia
    /// (<c>Communications:Email:*</c>) y nunca la de una empresa — no hay fallback cruzado.
    /// </summary>
    public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) =>
        scope.Kind switch
        {
            CommunicationScopeKind.Company => ResolveAsync(scope.CompanyId!.Value, ct),
            CommunicationScopeKind.System => ResolveAsync(companyScopeId: null, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Alcance de comunicación desconocido."),
        };

    /// <param name="companyScopeId">Empresa cuyo OrgSettings se lee; null = solo instancia.</param>
    private async Task<CommunicationEmailSettings> ResolveAsync(Guid? companyScopeId, CancellationToken ct)
    {
        // Bug corregido (CONFIG-AUDIT-01 / COMMUNICATIONS-SETTINGS-UI-01): antes las tres
        // funciones GetStringAsync/GetIntAsync/GetBoolAsync consultaban OrgSettings con
        // scopeId=Guid.Empty fijo, que nunca coincide con el companyId real con el que
        // UpdateCompanyEmailSettingsCommandHandler guarda las filas (scope=Company,
        // scopeId=companyId) — la capa OrgSettings jamás se leía en la práctica y todo
        // dependía silenciosamente del fallback por variable de entorno.

        var enabled = await GetBoolAsync(OrgSettingKeys.Communications.EmailEnabled, companyScopeId, ct)
            ?? _configuration.GetValue("Communications:Email:Enabled", false);
        var host = await GetStringAsync(OrgSettingKeys.Communications.SmtpHost, companyScopeId, ct)
            ?? _configuration["Communications:Email:SmtpHost"];
        var port = await GetIntAsync(OrgSettingKeys.Communications.SmtpPort, companyScopeId, ct)
            ?? _configuration.GetValue("Communications:Email:SmtpPort", 587);
        var username = await GetStringAsync(OrgSettingKeys.Communications.SmtpUsername, companyScopeId, ct)
            ?? _configuration["Communications:Email:SmtpUsername"];
        var storedPassword = await GetStringAsync(OrgSettingKeys.Communications.SmtpPassword, companyScopeId, ct);
        var password = storedPassword is not null
            ? _secretProtector.UnprotectOrPlaintext(storedPassword)
            : _configuration["Communications:Email:SmtpPassword"];
        var senderEmail = await GetStringAsync(OrgSettingKeys.Communications.SenderEmail, companyScopeId, ct)
            ?? _configuration["Communications:Email:SenderEmail"];
        var senderName = await GetStringAsync(OrgSettingKeys.Communications.SenderName, companyScopeId, ct)
            ?? _configuration["Communications:Email:SenderName"];
        var useSsl = await GetBoolAsync(OrgSettingKeys.Communications.UseSsl, companyScopeId, ct)
            ?? _configuration.GetValue("Communications:Email:UseSsl", true);
        var replyTo = await GetStringAsync(OrgSettingKeys.Communications.ReplyToEmail, companyScopeId, ct)
            ?? _configuration["Communications:Email:ReplyToEmail"];
        var maxRetries = await GetIntAsync(OrgSettingKeys.Communications.MaxRetries, companyScopeId, ct)
            ?? _configuration.GetValue("Communications:Email:MaxRetries", 3);
        var language = await GetStringAsync(OrgSettingKeys.Communications.DefaultLanguage, companyScopeId, ct)
            ?? _configuration["Communications:Email:DefaultLanguage"]
            ?? "es";

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
            Math.Clamp(maxRetries, 0, 20),
            language
        )
        {
            // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — solo instancia (no OrgSettings): el lease del
            // claim depende de este valor, acotado a [5, 120] s por CommunicationDeliveryTiming.
            SmtpTimeout = CommunicationDeliveryTiming.ResolveSmtpTimeout(
                _configuration.GetValue<int?>("Communications:Email:SmtpTimeoutSeconds")
            ),
        };
    }

    private Task<string?> GetStringAsync(string key, Guid? companyScopeId, CancellationToken ct) =>
        companyScopeId is { } companyId
            ? _orgConfig.GetValueAsync(OrgScope.Company, companyId, key, ct)
            : Task.FromResult<string?>(null);

    // GetValueAsync<T> (no usada aquí a propósito): IOrgConfigResolver la declara sin
    // `where T : struct`, así que T? no se materializa como Nullable<T> para tipos valor —
    // "no configurado" y "configurado como false/0" son indistinguibles ahí, lo que rompería el
    // fallback a variable de entorno para Enabled/UseSsl/SmtpPort/MaxRetries. Se lee el string
    // crudo y se parsea aquí, en vez de tocar el resolver genérico compartido (fuera de alcance).
    private async Task<int?> GetIntAsync(string key, Guid? companyScopeId, CancellationToken ct)
    {
        var raw = await GetStringAsync(key, companyScopeId, ct);
        return int.TryParse(raw, out var value) ? value : null;
    }

    private async Task<bool?> GetBoolAsync(string key, Guid? companyScopeId, CancellationToken ct)
    {
        var raw = await GetStringAsync(key, companyScopeId, ct);
        return bool.TryParse(raw, out var value) ? value : null;
    }
}

