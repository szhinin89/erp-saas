using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration;

/// <summary>
/// Diagnostica la configuración SRI de la empresa activa — solo lectura, nunca persiste
/// nada. Reutiliza <see cref="ISriSettingsRepository"/> (misma fuente de verdad que
/// Get/UpsertSriConfiguration), <see cref="ISecretProtector"/> y
/// <see cref="ISriCertificateInspector"/>/<see cref="SriSoapClient"/> ya existentes.
/// </summary>
public sealed record ValidateSriConfigurationQuery : IRequest<Result<SriConfigurationValidationDto>>;
