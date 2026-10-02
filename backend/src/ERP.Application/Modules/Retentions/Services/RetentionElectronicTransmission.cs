using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036 §14, transmisión inmediata) — ÚNICA entrada para
/// iniciar la transmisión electrónica de una retención emitida. La usan, sin variantes:
/// (1) la confirmación de Compra/Gasto, DESPUÉS del commit del negocio (nunca dentro de su
/// transacción: SOAP no puede sostener locks de Compra/Gasto/CxP/Contabilidad/Inventario);
/// (2) el job de recuperación (retenciones Issued sin ElectronicDocument);
/// (3) la acción de recuperación controlada <c>POST /retentions/{id}/electronic/register</c>.
///
/// No es un segundo pipeline: delega íntegramente en <see cref="IElectronicDocumentIssuer.RegisterAsync"/>,
/// que aplica el gate de ciclo de vida, el reclamo Dispatching y es idempotente (un solo
/// ElectronicDocument por origen: chequeo + índice único <c>uq_electronic_document_source</c>).
/// Nunca lanza: un fallo de transmisión no revierte ni invalida el negocio ya confirmado.
/// </summary>
public interface IRetentionElectronicTransmission
{
    Task<Result<ElectronicDocumentDto>> StartAsync(
        Guid tenantId,
        Guid companyId,
        Guid retentionId,
        Guid userId,
        CancellationToken ct = default
    );
}

public sealed partial class RetentionElectronicTransmission : IRetentionElectronicTransmission
{
    private readonly IElectronicDocumentIssuer _issuer;
    private readonly ILogger<RetentionElectronicTransmission> _logger;

    public RetentionElectronicTransmission(
        IElectronicDocumentIssuer issuer,
        ILogger<RetentionElectronicTransmission> logger
    )
    {
        _issuer = issuer;
        _logger = logger;
    }

    public async Task<Result<ElectronicDocumentDto>> StartAsync(
        Guid tenantId,
        Guid companyId,
        Guid retentionId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        try
        {
            var result = await _issuer.RegisterAsync(
                new RegisterElectronicDocumentRequest(
                    tenantId,
                    companyId,
                    ElectronicDocumentType.Retention,
                    RetentionElectronicDocumentSource.SourceModule,
                    retentionId,
                    userId
                ),
                ct
            );
            if (!result.IsSuccess)
                LogStartNotCompleted(retentionId, result.Code, result.Error);
            return result;
        }
        catch (Exception ex)
        {
            LogStartThrew(retentionId, ex);
            return Result<ElectronicDocumentDto>.Failure(
                "No se pudo iniciar la transmisión electrónica de la retención. Se reintentará automáticamente."
            );
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[Retentions] Transmisión electrónica de la retención {RetentionId} no completada ({Code}): {Reason}"
    )]
    private partial void LogStartNotCompleted(Guid retentionId, string? code, string? reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[Retentions] Excepción iniciando la transmisión electrónica de la retención {RetentionId}"
    )]
    private partial void LogStartThrew(Guid retentionId, Exception ex);
}
