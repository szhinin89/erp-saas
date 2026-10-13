using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.Communications.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Ride.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — Ride entrega el PDF del RIDE al ENVIAR el correo (fuera de la
/// transacción fiscal): obtiene o genera el RIDE por su superficie pública
/// (<see cref="IRideBackgroundDocumentService"/>, con el par <c>SourceModule</c>/<c>SourceId</c> de la
/// comunicación y el alcance que abrió el processor) y lee el PDF del almacenamiento oficial.
/// <para>
/// Adjunto opcional (mismo criterio que el correo de factura previo): <c>PendingSource</c> es
/// reintentable (el XML autorizado todavía no es legible); cualquier otro desenlace sin PDF omite el
/// RIDE y el correo sale con el XML autorizado, que es el comprobante legal.
/// </para>
/// </summary>
public sealed partial class RidePdfCommunicationAttachmentProvider : ICommunicationAttachmentContentProvider
{
    private readonly IRideBackgroundDocumentService _documents;
    private readonly ILogger<RidePdfCommunicationAttachmentProvider> _logger;
    private readonly IFileStorage _fileStorage;

    public RidePdfCommunicationAttachmentProvider(IRideBackgroundDocumentService documents,
        IFileStorage fileStorage, ILogger<RidePdfCommunicationAttachmentProvider> logger)
    {
        _documents = documents;
        _logger = logger;
        _fileStorage = fileStorage;
    }

    public CommunicationAttachmentType AttachmentType => CommunicationAttachmentType.RidePdf;

    public async Task<CommunicationAttachmentResolution> ResolveAsync(
        CommunicationAttachmentReference reference,
        CancellationToken ct = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(reference.SourceModule)
            || reference.SourceId is not { } sourceId
        )
            return CommunicationAttachmentResolution.Skipped("RideSourceUnknown");

        ERP.Application.Common.Result<RideGenerationResultDto> result;
        try
        {
            result = await _documents.GetOrGenerateAsync(
                reference.TenantId, reference.CompanyId, reference.SourceModule, sourceId,
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Exception text/stack can include XML, paths, credentials or PII. Log only a safe type and stage.
            LogGenerationFailure("GetOrGenerate", ex.GetType().Name);
            // Un fallo del motor RIDE no bloquea el correo con el comprobante legal (XML autorizado).
            return CommunicationAttachmentResolution.Skipped("RideGenerationThrew");
        }

        if (!result.IsSuccess)
            return CommunicationAttachmentResolution.Skipped("RideUnavailable");

        var ride = result.Value!;
        if (ride.Outcome == RideOutcome.PendingSource)
            throw new CommunicationAttachmentException(
                "El RIDE aún no puede generarse: XML autorizado pendiente."
            );

        if (
            ride.Outcome is not (RideOutcome.Generated or RideOutcome.Cached)
            || string.IsNullOrWhiteSpace(ride.StoragePath)
        )
            return CommunicationAttachmentResolution.Skipped($"Ride{ride.Outcome}");

        await using var stream = await _fileStorage.GetAsync(ride.StoragePath, ct);
        if (stream is null)
            return CommunicationAttachmentResolution.Skipped("RideFileMissing");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return CommunicationAttachmentResolution.Resolved(buffer.ToArray());
    }

    [LoggerMessage(EventId = 4240, Level = LogLevel.Warning,
        Message = "RIDE attachment failed at {Stage}: {ExceptionType}")]
    private partial void LogGenerationFailure(string stage, string exceptionType);
}
