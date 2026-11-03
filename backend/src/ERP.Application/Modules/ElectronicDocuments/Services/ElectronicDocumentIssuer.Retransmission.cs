using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// R12 Fase B — corrección y retransmisión de Factura 01, Nota de crédito 04 y Retención 07 desde
/// <c>Rejected</c>. Reutiliza el pipeline de emisión (supplier, XSD, firma, tamaño), el almacenamiento,
/// el lock del origen (guard de Fase A / ADR-036) y el envío/consulta SOAP: no es otro motor fiscal.
/// Orden: elegibilidad → historial SRI (solo consulta) → XML regenerado y firmado → corrección
/// semántica demostrada → versión nueva almacenada → reclamo transaccional → UNA transmisión.
/// </summary>
public sealed partial class ElectronicDocumentIssuer
{
    public async Task<ElectronicDocumentRetransmissionEligibility> EvaluateAsync(
        ElectronicDocument document,
        CancellationToken ct = default
    )
    {
        if (document.CurrentState != ElectronicDocumentState.Rejected)
            return ElectronicDocumentRetransmissionPolicy.Evaluate(document, null, null);

        var guard = _sourceGuards.Resolve(document.SourceModule);
        var source = guard is null
            ? null
            : await guard.EvaluateAsync(
                document.TenantId,
                document.CompanyId,
                document.SourceEntityId,
                lockForUpdate: false,
                ct
            );
        var evidence = await _repository.GetRejectionEvidenceAsync(
            document.TenantId,
            document.Id,
            document.TransmissionAttemptStartedUtc,
            ct
        );
        return ElectronicDocumentRetransmissionPolicy.Evaluate(document, source, evidence);
    }

    public async Task<Result<ElectronicDocumentDto>> RetransmitCorrectedAsync(
        CorrectedRetransmissionRequest request,
        CancellationToken ct = default
    )
    {
        var document = await _repository.GetByIdAsync(
            request.TenantId,
            request.ElectronicDocumentId,
            ct
        );
        if (document is null)
            return Result<ElectronicDocumentDto>.NotFound("El documento electrónico no existe.");

        // SRI-TOGGLE-07 — historial, reclamo, envío y consulta dentro del ámbito SRI de la empresa.
        await using var sriCall = await _emissionSwitch.EnterSriCallAsync(document.TenantId, document.CompanyId, ct);

        // Idempotencia: la misma solicitud ya reclamada devuelve el estado actual, sin otro envío.
        if (await IsReplayAsync(document, request.IdempotencyKey, ct))
            return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));

        var eligibility = await EvaluateAsync(document, ct);
        if (!eligibility.Eligible)
            return Blocked(
                document,
                string.Join(" ", eligibility.Blocks.Select(b => b.Message)),
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );
        // SRI-TOGGLE-02 — una retransmisión es una transmisión nueva: requiere emisión habilitada.
        if (await EmissionBlockedAsync(document, request.UserId, lockForShare: false, ct) is { } emissionBlocked)
            return emissionBlocked;
        var guard = _sourceGuards.Resolve(document.SourceModule)!;
        var stage = eligibility.RejectionStage!.Value;
        var expectedAttempt = document.TransmissionAttempt;

        // Historial del SRI para la clave (solo consulta): una AUTORIZACIÓN previa, un comprobante en
        // proceso o una evidencia que no coincide con la procedencia del rechazo bloquean el reenvío.
        var history = await _authorizationService.QueryDecisionsAsync(
            document.CompanyId,
            document.AccessKey!.Value,
            ct
        );
        if (!history.IsSuccess)
            return Blocked(
                document,
                $"No se pudo confirmar el historial del comprobante en el SRI; no se retransmite. {history.Error}".TrimEnd(),
                ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked
            );
        var historyBlock = EvaluateSriHistory(document, stage, history.Value!, out var watermark);
        if (historyBlock is not null)
            return Blocked(
                document,
                historyBlock,
                ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked
            );

        // Evidencia anterior: el XML firmado rechazado, con su hash verificado contra el registro del
        // intento previo cuando existe.
        var previousBytes = await _xmlStorageService.ReadAsync(document.SignedXmlPath!, ct);
        if (!previousBytes.IsSuccess)
            return Blocked(
                document,
                "No se encontró el XML firmado del intento rechazado; sin esa evidencia no se retransmite.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );
        var previousSha256 = Sha256(previousBytes.Value!);
        var latest = await _repository.GetLatestTransmissionAttemptAsync(
            document.TenantId,
            document.Id,
            ct
        );
        if (
            latest is not null
            && (latest.SignedXmlPath != document.SignedXmlPath || latest.SignedXmlSha256 != previousSha256)
        )
            return Blocked(
                document,
                "El XML firmado del intento anterior no coincide con el hash registrado; la evidencia no es verificable.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );

        // XML regenerado desde el origen confirmado, validado y firmado por el mismo pipeline.
        var supplier = _xmlSupplierResolver.Resolve(document.DocumentType);
        if (supplier is null)
            return Blocked(
                document,
                $"No hay un generador de XML registrado para el tipo de documento '{document.DocumentType}'.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );
        PreparedSignedXml prepared;
        try
        {
            prepared = await PrepareSignedXmlAsync(
                supplier,
                document.DocumentType,
                new ElectronicDocumentSourceReference(
                    document.TenantId,
                    document.CompanyId,
                    document.SourceEntityId
                ),
                ct
            );
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "RetransmissionPreSignature", ex);
            return Blocked(
                document,
                $"Error inesperado al regenerar el comprobante: {ex.Message}",
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );
        }
        if (prepared.Failure is { } failure)
            return Blocked(
                document,
                $"El XML corregido no superó la etapa '{failure.Stage ?? "Pipeline"}': {failure.Reason}",
                ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection
            );

        var xml = prepared.Xml!;
        var signed = prepared.Signed!;
        if (!string.Equals(signed.AccessKey, document.AccessKey.Value, StringComparison.Ordinal))
            return Blocked(
                document,
                "El XML regenerado tiene una clave de acceso distinta a la del comprobante rechazado; la corrección no es compatible con la misma clave.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection
            );
        if (document.Environment is not null && document.Environment != xml.Environment)
            return Blocked(
                document,
                "El ambiente SRI del XML regenerado no coincide con el del comprobante rechazado.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection
            );

        var correction = ElectronicDocumentXmlCorrectionComparer.Compare(
            document.DocumentType,
            Encoding.UTF8.GetString(previousBytes.Value!),
            signed.SignedXml
        );
        if (correction.Outcome != ElectronicDocumentXmlCorrectionOutcome.Corrected)
            return Blocked(
                document,
                DescribeCorrectionBlock(correction),
                ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection
            );

        // Versión nueva e independiente: la evidencia del intento rechazado nunca se sobrescribe.
        var attemptId = Guid.NewGuid();
        var stored = await _xmlStorageService.StoreAttemptAsync(
            document.TenantId,
            document.DocumentType,
            document.Id,
            attemptId,
            xml,
            signed,
            ct
        );
        if (!stored.IsSuccess)
            return Blocked(
                document,
                stored.Error ?? "No se pudo almacenar el XML corregido.",
                ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed
            );

        var claim = await ClaimRetransmissionAsync(
            document,
            guard,
            request,
            expectedAttempt,
            stage,
            eligibility.RejectionCodes,
            watermark,
            stored.Value!,
            xml,
            prepared.SchemaVersion ?? string.Empty,
            signed,
            previousSha256,
            correction,
            ct
        );
        if (claim is not null)
        {
            await _xmlStorageService.DiscardAttemptAsync(stored.Value!, ct);
            return claim;
        }

        // SOAP solo después del commit del reclamo: exactamente UNA transmisión por reclamo. Desde
        // aquí todo resultado incierto queda en Signed/Dispatching/Received y solo se consulta.
        try
        {
            var (reachedReceived, _) = await TrySendToReceptionAsync(
                document,
                document.CompanyId,
                request.UserId,
                Encoding.UTF8.GetBytes(signed.SignedXml),
                ct
            );
            if (reachedReceived)
                await AuthorizeAsync(
                    document,
                    document.TenantId,
                    document.CompanyId,
                    request.UserId,
                    ct
                );
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "RetransmissionReceptionOrAuthorization", ex);
        }

        return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
    }

    private async Task<bool> IsReplayAsync(
        ElectronicDocument document,
        Guid idempotencyKey,
        CancellationToken ct
    )
    {
        var replay = await _repository.GetTransmissionAttemptByIdempotencyKeyAsync(
            document.TenantId,
            document.Id,
            idempotencyKey,
            ct
        );
        if (replay is null)
            return false;
        LogRetransmissionReplayed(document.Id, replay.AttemptNumber);
        return true;
    }

    /// <summary>
    /// Decide con las decisiones que el SRI ya tiene de la clave. Devuelve el motivo de bloqueo o null, y
    /// en <paramref name="watermark"/> la fecha (reloj del SRI) de la última NO AUTORIZADO conocida: toda
    /// respuesta igual o anterior será histórica para el intento nuevo.
    /// </summary>
    private static string? EvaluateSriHistory(
        ElectronicDocument document,
        ElectronicDocumentRejectionStage stage,
        IReadOnlyList<SriAuthorizationDecision> decisions,
        out DateTime? watermark
    )
    {
        watermark = null;
        if (decisions.Any(d => d.IsAuthorized))
            return "El SRI ya registra una AUTORIZACIÓN para esta clave de acceso; no se retransmite. Requiere conciliación del estado del comprobante.";
        if (decisions.Any(d => !d.IsNotAuthorized))
            return "El SRI registra la clave de acceso en un estado no definitivo; no se retransmite hasta conocer su resultado.";
        if (decisions.Any(d => d.DecisionDateUtc is null))
            return "El SRI devolvió una respuesta NO AUTORIZADO sin fecha interpretable; no puede distinguirse de un intento nuevo.";

        var newer = decisions
            .Where(d => document.IsCurrentAttemptSriDecision(d.DecisionDateUtc))
            .ToList();
        switch (stage)
        {
            // NO AUTORIZADO: el SRI debe conservar la decisión que rechazó el intento vigente.
            case ElectronicDocumentRejectionStage.Authorization when newer.Count == 0:
                return "El rechazo registrado es NO AUTORIZADO, pero el SRI no conserva esa decisión para la clave; la evidencia es inconsistente.";
            // DEVUELTA: el SRI no guarda los devueltos; una decisión del intento vigente lo contradice.
            case ElectronicDocumentRejectionStage.Reception when newer.Count > 0:
                return "El rechazo registrado es DEVUELTA, pero el SRI registra una decisión posterior para la clave; la evidencia es inconsistente.";
        }

        watermark = decisions.Count == 0 ? null : decisions.Max(d => d.DecisionDateUtc);
        return null;
    }

    private static string DescribeCorrectionBlock(ElectronicDocumentXmlCorrection correction) =>
        correction.Outcome switch
        {
            ElectronicDocumentXmlCorrectionOutcome.NoCorrection =>
                "El XML regenerado es idéntico al rechazado (sin contar la firma): no existe una corrección demostrable y no se retransmite.",
            ElectronicDocumentXmlCorrectionOutcome.SubjectIdentificationChanged =>
                $"La identificación del sujeto retenido cambió respecto al XML rechazado; la retransmisión queda bloqueada. {correction.Summary}",
            ElectronicDocumentXmlCorrectionOutcome.ProtectedDataChanged =>
                $"El XML regenerado modifica datos de la operación comercial, de la contraparte o de la clave; no es la misma operación. {correction.Summary}",
            _ => $"No se pudo comparar el XML rechazado con el corregido. {correction.Summary}",
        };

    /// <summary>
    /// Reclamo transaccional: bajo el lock del origen (el mismo de la anulación y del envío, Fase A /
    /// ADR-036) relee el documento, exige que siga Rejected en el intento evaluado, registra el intento
    /// (únicos por número e idempotencia) y persiste Rejected→Signed(→Dispatching) con xmin. Devuelve
    /// null si quedó confirmado; si no, el Result de bloqueo sin escrituras.
    /// </summary>
    private async Task<Result<ElectronicDocumentDto>?> ClaimRetransmissionAsync(
        ElectronicDocument document,
        IElectronicDocumentSourceLifecycleGuard guard,
        CorrectedRetransmissionRequest request,
        int expectedAttempt,
        ElectronicDocumentRejectionStage stage,
        IReadOnlyList<string> rejectionCodes,
        DateTime? watermark,
        ElectronicDocumentStoredXmlPaths stored,
        ElectronicDocumentXml xml,
        string schemaVersion,
        SignedElectronicDocumentXml signed,
        string previousSha256,
        ElectronicDocumentXmlCorrection correction,
        CancellationToken ct
    )
    {
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "La retransmisión no puede reclamarse dentro de una transacción de negocio abierta (se llamaría al SRI con locks tomados)."
            );

        ElectronicDocumentTransmissionAttempt? attempt = null;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var lifecycle = await guard.EvaluateAsync(
                document.TenantId,
                document.CompanyId,
                document.SourceEntityId,
                lockForUpdate: true,
                ct
            );
            await _repository.ReloadAsync(document, ct);

            if (!lifecycle.AllowsProcessing)
            {
                await _unitOfWork.RollbackAsync(ct);
                return SourceNotProcessable(document.SourceModule, document.SourceEntityId, lifecycle);
            }
            if (
                document.CurrentState != ElectronicDocumentState.Rejected
                || document.TransmissionAttempt != expectedAttempt
                || document.SignedXmlPath is null
            )
            {
                await _unitOfWork.RollbackAsync(ct);
                return await ConcurrentClaimResultAsync(document, request.IdempotencyKey, ct);
            }

            // SRI-TOGGLE-02/04 — dentro del reclamo: el lock compartido del interruptor (hasta el commit)
            // serializa con su apagado/habilitación.
            if (await EmissionBlockedAsync(document, request.UserId, lockForShare: true, ct) is { } emissionBlocked)
            {
                await _unitOfWork.RollbackAsync(ct);
                return emissionBlocked;
            }

            var previousSignedPath = document.SignedXmlPath;
            attempt = ElectronicDocumentTransmissionAttempt.Create(
                document,
                expectedAttempt + 1,
                request.IdempotencyKey,
                request.Reason,
                request.UserId,
                stage.ToString(),
                rejectionCodes,
                watermark,
                previousSignedPath,
                previousSha256,
                stored.DraftXmlPath,
                Sha256(Encoding.UTF8.GetBytes(xml.Xml)),
                stored.SignedXmlPath,
                Sha256(Encoding.UTF8.GetBytes(signed.SignedXml)),
                correction.Summary
            );
            await _repository.AddTransmissionAttemptAsync(attempt, ct);
            document.ClaimCorrectedRetransmission(
                stored.DraftXmlPath,
                stored.SignedXmlPath,
                xml.Version,
                schemaVersion,
                AccessKey.Create(signed.AccessKey),
                watermark,
                request.Reason,
                request.UserId
            );
            if (guard.UsesDispatchProtocol)
                document.MarkDispatching(request.UserId);
            await _repository.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            LogRetransmissionClaimed(document.Id, document.TransmissionAttempt, document.CurrentState);
            return null;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            if (attempt is not null)
                _repository.DiscardTransmissionAttempt(attempt);
            await _repository.ReloadAsync(document, ct);
            document.ClearDomainEvents();

            // Carrera perdida contra otra solicitud (índice único del intento o de la idempotencia, o
            // xmin del documento): nunca se transmite; se informa el resultado del ganador.
            if (
                _dbEx.ClassifyFailureCode(ex)
                is ApiResponseCodes.Common.UniqueViolation
                    or ApiResponseCodes.Common.ConcurrencyConflict
            )
                return await ConcurrentClaimResultAsync(document, request.IdempotencyKey, ct);
            throw;
        }
    }

    private async Task<Result<ElectronicDocumentDto>> ConcurrentClaimResultAsync(
        ElectronicDocument document,
        Guid idempotencyKey,
        CancellationToken ct
    ) =>
        await IsReplayAsync(document, idempotencyKey, ct)
            ? Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document))
            : Result<ElectronicDocumentDto>.Conflict(
                $"El comprobante cambió durante la retransmisión (estado actual: {document.CurrentState}, intento {document.TransmissionAttempt}); no se retransmitió."
            );

    private Result<ElectronicDocumentDto> Blocked(
        ElectronicDocument document,
        string reason,
        string code
    )
    {
        LogRetransmissionBlocked(document.Id, code, reason);
        return Result<ElectronicDocumentDto>.ValidationFailure(reason, code);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Retransmisión corregida bloqueada para {ElectronicDocumentId} ({Code}): {Reason}"
    )]
    private partial void LogRetransmissionBlocked(Guid electronicDocumentId, string code, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Retransmisión corregida reclamada para {ElectronicDocumentId}: intento {Attempt}, estado {State}"
    )]
    private partial void LogRetransmissionClaimed(
        Guid electronicDocumentId,
        int attempt,
        ElectronicDocumentState state
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Solicitud de retransmisión repetida para {ElectronicDocumentId} (intento {Attempt} ya reclamado): sin nuevo envío"
    )]
    private partial void LogRetransmissionReplayed(Guid electronicDocumentId, int attempt);
}
