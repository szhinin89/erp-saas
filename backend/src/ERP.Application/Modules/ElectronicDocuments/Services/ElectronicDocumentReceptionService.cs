using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed partial class ElectronicDocumentReceptionService : IElectronicDocumentReceptionService
{
    private static readonly string[] TransportFailureStatuses =
        ["ERROR_CONEXION", "ERROR_RESPUESTA_INVALIDA", "ERROR_SOAP_FAULT"];

    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly ISriReceptionClient _client;
    private readonly ILogger<ElectronicDocumentReceptionService> _logger;

    public ElectronicDocumentReceptionService(
        ISriSettingsRepository sriSettingsRepository,
        ISriReceptionClient client,
        ILogger<ElectronicDocumentReceptionService> logger)
    {
        _sriSettingsRepository = sriSettingsRepository;
        _client = client;
        _logger = logger;
    }

    public async Task<Result<SriReceptionResult>> SendAsync(
        Guid companyId, byte[] signedXmlBytes, CancellationToken ct = default)
    {
        var sriSettings = await _sriSettingsRepository.GetByCompanyIdAsync(companyId, ct);
        if (sriSettings is null)
        {
            LogReceptionPrerequisiteMissing(companyId, "sin configuración SRI");
            return Result<SriReceptionResult>.ValidationFailure(
                "La empresa no tiene configuración SRI registrada — no se puede enviar el documento electrónico al SRI.");
        }

        if (string.IsNullOrWhiteSpace(sriSettings.WsdlUrl))
        {
            LogReceptionPrerequisiteMissing(companyId, "sin WsdlUrl configurada");
            return Result<SriReceptionResult>.ValidationFailure(
                "La empresa no tiene una URL de servicio SRI (WsdlUrl) configurada.");
        }

        var result = await _client.SendAsync(signedXmlBytes, sriSettings.WsdlUrl, ct);
        LogReceptionResult(companyId, result.Status);

        if (TransportFailureStatuses.Contains(result.Status, StringComparer.OrdinalIgnoreCase))
            return Result<SriReceptionResult>.Failure(
                result.Errors.Count > 0
                    ? string.Join(" ", result.Errors)
                    : "No se pudo enviar el documento electrónico al SRI.");

        return Result<SriReceptionResult>.Success(result);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Recepción SRI no ejecutada para empresa {CompanyId}: {Reason}")]
    private partial void LogReceptionPrerequisiteMissing(Guid companyId, string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Recepción SRI respondió estado '{Status}' para empresa {CompanyId}")]
    private partial void LogReceptionResult(Guid companyId, string status);
}
