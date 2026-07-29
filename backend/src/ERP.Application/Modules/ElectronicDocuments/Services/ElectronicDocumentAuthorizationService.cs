using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed partial class ElectronicDocumentAuthorizationService
    : IElectronicDocumentAuthorizationService
{
    // Únicos estados que representan una respuesta terminal que el llamador puede/debe
    // interpretar y transicionar. Lista blanca deliberada (no lista negra): cualquier estado
    // no reconocido se trata como fallo de transporte y no dispara ninguna transición.
    // EST-01v2 (rechazo real del SRI, 2026-07-11): el literal real que devuelve el WS
    // AutorizacionComprobantesOffline es "NO AUTORIZADO" (con espacio) — no "RECHAZADO", que
    // nunca apareció en una respuesta real (ver SriSoapClient.CheckAuthorizationAsync).
    private static readonly string[] TerminalOutcomeStatuses =
    [
        "AUTORIZADO",
        "NO AUTORIZADO",
        "TIMEOUT",
    ];

    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly ISriAuthorizationClient _client;
    private readonly ILogger<ElectronicDocumentAuthorizationService> _logger;

    public ElectronicDocumentAuthorizationService(
        ISriSettingsRepository sriSettingsRepository,
        ISriAuthorizationClient client,
        ILogger<ElectronicDocumentAuthorizationService> logger
    )
    {
        _sriSettingsRepository = sriSettingsRepository;
        _client = client;
        _logger = logger;
    }

    public async Task<Result<SriAuthorizationResult>> CheckAsync(
        Guid companyId,
        string accessKey,
        CancellationToken ct = default
    )
    {
        var sriSettings = await _sriSettingsRepository.GetByCompanyIdAsync(companyId, ct);
        if (sriSettings is null)
        {
            LogAuthorizationPrerequisiteMissing(companyId, "sin configuración SRI");
            return Result<SriAuthorizationResult>.ValidationFailure(
                "La empresa no tiene configuración SRI registrada — no se puede consultar la autorización del documento electrónico."
            );
        }

        if (string.IsNullOrWhiteSpace(sriSettings.WsdlUrl))
        {
            LogAuthorizationPrerequisiteMissing(companyId, "sin WsdlUrl configurada");
            return Result<SriAuthorizationResult>.ValidationFailure(
                "La empresa no tiene una URL de servicio SRI (WsdlUrl) configurada."
            );
        }

        var result = await _client.CheckAsync(accessKey, sriSettings.WsdlUrl, ct);
        LogAuthorizationResult(companyId, accessKey, result.Status);

        if (TerminalOutcomeStatuses.Contains(result.Status, StringComparer.OrdinalIgnoreCase))
            return Result<SriAuthorizationResult>.Success(result);

        return Result<SriAuthorizationResult>.Failure(
            result.ErrorMessage
                ?? $"No se pudo obtener una respuesta definitiva de autorización del SRI (estado: {result.Status})."
        );
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Consulta de autorización SRI no ejecutada para empresa {CompanyId}: {Reason}"
    )]
    private partial void LogAuthorizationPrerequisiteMissing(Guid companyId, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Consulta de autorización SRI respondió estado '{Status}' para empresa {CompanyId}, claveAcceso {AccessKey}"
    )]
    private partial void LogAuthorizationResult(Guid companyId, string accessKey, string status);
}
