using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.UpdateCompanyEmailSettings;

/// <summary>
/// <see cref="SmtpPassword"/> nulo o en blanco significa "conservar la contraseña ya guardada" —
/// este endpoint nunca borra una contraseña existente; solo la reemplaza cuando se envía un
/// valor no vacío. No hay una acción separada de "borrar contraseña" (fuera de alcance).
/// </summary>
public sealed record UpdateCompanyEmailSettingsCommand(
    bool Enabled,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SenderEmail,
    string? SenderName,
    bool UseSsl,
    string? ReplyToEmail,
    int? MaxRetries,
    string? DefaultLanguage
) : IRequest<Result<CommunicationEmailSettingsDto>>;
