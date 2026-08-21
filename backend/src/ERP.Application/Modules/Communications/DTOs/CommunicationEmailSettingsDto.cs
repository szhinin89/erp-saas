namespace ERP.Application.Modules.Communications.DTOs;

/// <summary>
/// Nunca incluye el password en texto plano — solo <see cref="PasswordConfigured"/>.
/// </summary>
public sealed record CommunicationEmailSettingsDto(
    bool Enabled,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    bool PasswordConfigured,
    string? SenderEmail,
    string? SenderName,
    bool UseSsl,
    string? ReplyToEmail,
    int MaxRetries,
    string DefaultLanguage,
    /// <summary>"OrgSettings" si la empresa ya guardó su propia configuración; "EnvironmentFallback" si depende de variables de entorno.</summary>
    string Source
);

public sealed record SendTestEmailResultDto(bool Sent, string Message);
