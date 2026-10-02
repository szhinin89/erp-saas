using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>Resultado de anular el documento origen al finalizar una anulación SRI confirmada.</summary>
public enum RetentionOriginCancellationOutcome
{
    Cancelled = 1,
    AlreadyCancelled = 2,
}

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — puerto por origen para FINALIZAR una anulación confirmada por el
/// SRI: anula el documento origen por su flujo oficial EXISTENTE (mismos guards, locks, reversos de
/// CxP, contabilidad, Kardex) con el motivo y el usuario de la solicitud. Lo implementa el módulo
/// dueño del origen (Compras, Gastos) — Retentions no los referencia, y ElectronicDocuments nunca
/// ejecuta reversos.
/// </summary>
public interface IRetentionOriginCancellation
{
    RetentionSourceDocumentType SourceType { get; }

    Task<Result<RetentionOriginCancellationOutcome>> CancelAsync(
        RetentionAnnulmentRequest request,
        CancellationToken ct = default
    );
}

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01/01B (ADR-036 D-6…D-8, §24) — ciclo de la anulación ante el SRI de una
/// retención AUTORIZADA. La SOLICITUD es asistida (no existe WS para solicitarla: el usuario la presenta
/// en SRI en Línea y registra que lo hizo); la VERIFICACIÓN es automática vía ConsultaComprobante (Ficha
/// Técnica v2.34 §8): el estado fiscal lo informa el SRI, nunca el usuario.
///
/// Serialización: toda transición toma el lock de la fila de la retención (el mismo del reclamo de
/// envío y de la anulación del origen, ADR-036 §23) y relee la solicitud bajo ese lock; la solicitud
/// tiene además <c>xmin</c>. La CxP del origen queda retenida desde la solicitud hasta su resolución
/// o la finalización (pagos/créditos/ajustes bloqueados en el agregado <c>AccountsPayable</c>).
/// </summary>
public interface IRetentionAnnulmentService
{
    Task<Result<RetentionAnnulmentRequest>> MarkSubmittedAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        DateOnly submittedOn,
        string? reference,
        string? notes,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Consulta el estado fiscal en ConsultaComprobante y lo aplica. La consulta SOAP ocurre SIN
    /// transacción ni lock; después se toma el lock de la retención, se relee la solicitud y se aplica:
    /// ANULADO → solicitud aceptada con evidencia técnica, comprobante Cancelled y finalización del origen
    /// (una vez); AUTORIZADO / PENDIENTE DE ANULAR / NO AUTORIZADO → solo se registra la consulta; consulta
    /// rechazada (99) / timeout / red → solo se registra el fallo, nunca un estado fiscal. Idempotente y
    /// seguro ante verificaciones concurrentes (en línea y job). El código de éxito describe lo informado.
    /// </summary>
    Task<Result<RetentionAnnulmentRequest>> VerifyWithSriAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<RetentionAnnulmentRequest>> AbandonAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// FinalizeRetentionOriginCancellation — idempotente: anula el origen por su flujo oficial y marca
    /// la solicitud finalizada. Un fallo queda registrado y la recuperación lo reintenta.
    /// </summary>
    Task<Result<RetentionAnnulmentRequest>> FinalizeAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid userId,
        CancellationToken ct = default
    );
}

public sealed partial class RetentionAnnulmentService : IRetentionAnnulmentService
{
    private readonly IRetentionAnnulmentRequestRepository _requests;
    private readonly IRetentionDocumentRepository _retentions;
    private readonly IElectronicDocumentSriAnnulment _sriAnnulment;
    private readonly IAccountsPayableRepository _payables;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IRetentionOriginCancellation> _origins;
    private readonly ISriDocumentStatusQuery _statusQuery;
    private readonly ISriSettingsRepository _sriSettings;
    private readonly ICompanyClock _clock;
    private readonly ILogger<RetentionAnnulmentService> _logger;

    public RetentionAnnulmentService(
        IRetentionAnnulmentRequestRepository requests,
        IRetentionDocumentRepository retentions,
        IElectronicDocumentSriAnnulment sriAnnulment,
        IAccountsPayableRepository payables,
        IUnitOfWork unitOfWork,
        IEnumerable<IRetentionOriginCancellation> origins,
        ISriDocumentStatusQuery statusQuery,
        ISriSettingsRepository sriSettings,
        ICompanyClock clock,
        ILogger<RetentionAnnulmentService> logger
    )
    {
        _requests = requests;
        _retentions = retentions;
        _sriAnnulment = sriAnnulment;
        _payables = payables;
        _unitOfWork = unitOfWork;
        _origins = origins;
        _statusQuery = statusQuery;
        _sriSettings = sriSettings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<RetentionAnnulmentRequest>> MarkSubmittedAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        DateOnly submittedOn,
        string? reference,
        string? notes,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        if (request is null)
            return NotFound();

        request.MarkSubmitted(submittedOn, reference, notes, userId);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<RetentionAnnulmentRequest>.Success(request);
    }

    public async Task<Result<RetentionAnnulmentRequest>> VerifyWithSriAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        if (request is null)
            return NotFound();
        if (request.Status != RetentionAnnulmentStatus.PendingSriResolution)
            return await NotVerifiableAsync(request, userId, ct);

        // Consulta SOAP fuera de cualquier transacción/lock (puede tardar: reintentos HTTP del cliente).
        var sri = await QuerySriAsync(companyId, request.AccessKey, ct);
        LogVerified(request.Id, sri.Outcome, sri.FiscalStatus, sri.RawAuthorizationStatus ?? sri.RawQueryStatus);
        var checkedAtUtc = DateTime.UtcNow;
        var verifiedOn = await _clock.TodayAsync(companyId, tenantId, ct);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            request = await LockAndReloadAsync(tenantId, companyId, requestId, ct);
            if (request is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return NotFound();
            }
            if (request.Status != RetentionAnnulmentStatus.PendingSriResolution)
            {
                // Otra verificación concurrente (en línea o job) ya aplicó un resultado.
                await _unitOfWork.RollbackAsync(ct);
                return await NotVerifiableAsync(request, userId, ct);
            }

            request.RecordSriCheck(
                checkedAtUtc,
                sri.Outcome,
                sri.FiscalStatus,
                sri.RawAuthorizationStatus ?? sri.RawQueryStatus,
                userId
            );

            if (sri.FiscalStatus == SriFiscalStatus.Annulled)
            {
                var evidence = new ExternalAnnulmentEvidence(
                    verifiedOn,
                    $"ConsultaComprobante ANULADO {checkedAtUtc:yyyy-MM-ddTHH:mm:ssZ}",
                    userId
                );
                request.AcceptSriAnnulment(verifiedOn, evidence.Reference, sri.RawResponse, userId);
                var electronic = await _sriAnnulment.ConfirmAsync(
                    tenantId,
                    companyId,
                    RetentionElectronicDocumentSource.SourceModule,
                    request.RetentionDocumentId,
                    request.Id,
                    evidence,
                    userId,
                    ct
                );
                if (!electronic.IsSuccess)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<RetentionAnnulmentRequest>.ValidationFailure(electronic.Error!, electronic.Code);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        // ANULADO: el origen se anula ahora, fuera de la transacción de la verificación, por su flujo
        // oficial y una sola vez (FinalizeAsync es idempotente y seguro ante concurrencia).
        if (request.RequiresFinalization)
            return await FinalizeAsync(tenantId, companyId, requestId, userId, ct);

        return Result<RetentionAnnulmentRequest>.Success(request, VerificationCode(sri));
    }

    /// <summary>
    /// SriSettings de la empresa → ConsultaComprobante del mismo ambiente. Normaliza el contrato: sin
    /// éxito no hay estado fiscal, y un éxito sin estado reconocido no es éxito.
    /// </summary>
    private async Task<SriDocumentStatusResult> QuerySriAsync(Guid companyId, string accessKey, CancellationToken ct)
    {
        var settings = await _sriSettings.GetByCompanyIdAsync(companyId, ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.WsdlUrl))
            return new SriDocumentStatusResult
            {
                Outcome = SriStatusQueryOutcome.Unavailable,
                ErrorMessage = "La empresa no tiene configuración SRI (WsdlUrl) para consultar el comprobante.",
            };

        var result = await _statusQuery.QueryAsync(accessKey, settings.WsdlUrl, ct);
        if (result.Outcome != SriStatusQueryOutcome.Success && result.FiscalStatus != SriFiscalStatus.Unknown)
            return result with { FiscalStatus = SriFiscalStatus.Unknown };
        if (result.Outcome == SriStatusQueryOutcome.Success && result.FiscalStatus == SriFiscalStatus.Unknown)
            return result with { Outcome = SriStatusQueryOutcome.Unknown };
        return result;
    }

    private static string VerificationCode(SriDocumentStatusResult sri) =>
        sri.FiscalStatus switch
        {
            SriFiscalStatus.Authorized => ApiResponseCodes.Retentions.SriStillAuthorized,
            SriFiscalStatus.PendingAnnulment => ApiResponseCodes.Retentions.SriAnnulmentPending,
            SriFiscalStatus.NotAuthorized => ApiResponseCodes.Retentions.SriNotAuthorized,
            _ => ApiResponseCodes.Retentions.SriVerificationFailed,
        };

    /// <summary>La solicitud ya no espera al SRI: ANULADO previo (finaliza si quedó pendiente) o no presentada/cerrada.</summary>
    private async Task<Result<RetentionAnnulmentRequest>> NotVerifiableAsync(
        RetentionAnnulmentRequest request,
        Guid userId,
        CancellationToken ct
    )
    {
        if (request.RequiresFinalization)
            return await FinalizeAsync(request.TenantId, request.CompanyId, request.Id, userId, ct);
        if (request.Status == RetentionAnnulmentStatus.Accepted)
            return Result<RetentionAnnulmentRequest>.Success(request, ApiResponseCodes.Retentions.AnnulmentFinalized);
        return Result<RetentionAnnulmentRequest>.ValidationFailure(
            request.Status == RetentionAnnulmentStatus.PendingSubmission
                ? "Primero registre que presentó la solicitud de anulación en SRI en Línea."
                : $"La solicitud ya está cerrada (estado: {request.Status}); no hay nada que verificar en el SRI."
        );
    }

    public async Task<Result<RetentionAnnulmentRequest>> AbandonAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var request = await LockAndReloadAsync(tenantId, companyId, requestId, ct);
            if (request is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return NotFound();
            }
            if (request.Status == RetentionAnnulmentStatus.Abandoned)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<RetentionAnnulmentRequest>.Success(request);
            }

            var submitted = request.Status == RetentionAnnulmentStatus.PendingSriResolution;
            request.Abandon(reason, userId);
            var electronic = await _sriAnnulment.RevertAsync(
                tenantId,
                companyId,
                RetentionElectronicDocumentSource.SourceModule,
                request.RetentionDocumentId,
                request.Id,
                submitted
                    ? $"Solicitud desistida: el SRI informa AUTORIZADO (ConsultaComprobante) — {reason.Trim()}"
                    : $"Solicitud desistida antes de presentarse al SRI — {reason.Trim()}",
                userId,
                ct
            );
            if (!electronic.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<RetentionAnnulmentRequest>.ValidationFailure(electronic.Error!, electronic.Code);
            }
            await ReleasePayableHoldAsync(request, userId, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<RetentionAnnulmentRequest>.Success(request);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<RetentionAnnulmentRequest>> FinalizeAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        if (request is null)
            return NotFound();
        if (request.FinalizedAtUtc is not null)
            return Result<RetentionAnnulmentRequest>.Success(request, ApiResponseCodes.Retentions.AnnulmentFinalized);
        if (request.Status != RetentionAnnulmentStatus.Accepted)
            return Result<RetentionAnnulmentRequest>.ValidationFailure(
                "Solo una anulación confirmada por el SRI (ANULADO) finaliza la anulación del documento origen."
            );

        var origin = _origins.SingleOrDefault(o => o.SourceType == request.SourceDocumentType);
        if (origin is null)
            return await RecordFailureAsync(
                tenantId,
                companyId,
                requestId,
                userId,
                $"No hay un flujo de anulación registrado para el origen {request.SourceDocumentType}.",
                ct
            );

        Result<RetentionOriginCancellationOutcome> outcome;
        try
        {
            outcome = await origin.CancelAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFinalizationThrew(request.Id, ex);
            return await RecordFailureAsync(tenantId, companyId, requestId, userId, ex.Message, ct);
        }

        if (!outcome.IsSuccess)
            return await RecordFailureAsync(tenantId, companyId, requestId, userId, outcome.Error ?? "Error desconocido.", ct);

        request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        request!.MarkFinalized(userId);
        await _unitOfWork.SaveChangesAsync(ct);
        LogFinalized(request.Id, outcome.Value);
        return Result<RetentionAnnulmentRequest>.Success(request, ApiResponseCodes.Retentions.AnnulmentFinalized);
    }

    /// <summary>
    /// Registra el fallo de finalización sin arrastrar mutaciones a medias del intento fallido: el
    /// flujo del origen ya hizo rollback de su transacción, pero sus entidades en memoria pueden
    /// quedar modificadas en este DbContext — se descartan antes de guardar el fallo.
    /// </summary>
    private async Task<Result<RetentionAnnulmentRequest>> RecordFailureAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        Guid userId,
        string error,
        CancellationToken ct
    )
    {
        _unitOfWork.ClearChangeTracker();
        var request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        if (request is null)
            return NotFound();
        request.RecordFinalizationFailure(error, userId);
        await _unitOfWork.SaveChangesAsync(ct);
        LogFinalizationFailed(request.Id, error);
        return Result<RetentionAnnulmentRequest>.Success(
            request,
            ApiResponseCodes.Retentions.AnnulmentFinalizationPending
        );
    }

    /// <summary>Lock de la fila de la retención (mismo orden que 01A) y relectura de la solicitud bajo ese lock.</summary>
    private async Task<RetentionAnnulmentRequest?> LockAndReloadAsync(
        Guid tenantId,
        Guid companyId,
        Guid requestId,
        CancellationToken ct
    )
    {
        var request = await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
        if (request is null)
            return null;
        await _retentions.GetCurrentStatusAsync(tenantId, companyId, request.RetentionDocumentId, forUpdate: true, ct);
        return await _requests.GetByIdAsync(tenantId, companyId, requestId, ct);
    }

    private async Task ReleasePayableHoldAsync(RetentionAnnulmentRequest request, Guid userId, CancellationToken ct)
    {
        if (
            !RetentionCanceller.TryResolveAccountsPayableOriginType(
                request.SourceDocumentType,
                out var originType
            )
        )
            return;
        var payable = await _payables.GetByOriginAsync(
            request.TenantId,
            request.CompanyId,
            originType,
            request.SourceDocumentId,
            ct
        );
        payable?.ReleaseAnnulmentHold(request.Id, userId);
    }

    private static Result<RetentionAnnulmentRequest> NotFound() =>
        Result<RetentionAnnulmentRequest>.NotFound("La solicitud de anulación no existe.");

    [LoggerMessage(Level = LogLevel.Information, Message = "[Retentions] Anulación SRI {RequestId}: ConsultaComprobante {Outcome} / {FiscalStatus} ({Raw})")]
    private partial void LogVerified(Guid requestId, SriStatusQueryOutcome outcome, SriFiscalStatus fiscalStatus, string? raw);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Retentions] Anulación SRI {RequestId}: documento origen finalizado ({Outcome})")]
    private partial void LogFinalized(Guid requestId, RetentionOriginCancellationOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[Retentions] Anulación SRI {RequestId}: la finalización del origen falló y se reintentará: {Error}")]
    private partial void LogFinalizationFailed(Guid requestId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "[Retentions] Anulación SRI {RequestId}: excepción finalizando el origen")]
    private partial void LogFinalizationThrew(Guid requestId, Exception ex);
}

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — registra la solicitud de anulación ante el SRI de una retención
/// AUTORIZADA. Lo invoca la anulación del documento origen (Compra/Gasto) DENTRO de su transacción,
/// después de sus locks y guards: comprobante → AnnulmentPending, CxP retenida, solicitud creada; nada
/// revertido. Separado de <see cref="IRetentionAnnulmentService"/> a propósito: este paso no depende de
/// los flujos de anulación de los orígenes (que sí dependen de él).
/// </summary>
public interface IRetentionAnnulmentRequester
{
    Task<Result<RetentionAnnulmentRequest>> RequestAsync(
        RetentionDocument retention,
        string reason,
        Guid userId,
        CancellationToken ct = default
    );
}

public sealed partial class RetentionAnnulmentRequester : IRetentionAnnulmentRequester
{
    private readonly IRetentionAnnulmentRequestRepository _requests;
    private readonly IElectronicDocumentSriAnnulment _sriAnnulment;
    private readonly IAccountsPayableRepository _payables;
    private readonly IBusinessPartnerRepository _partners;
    private readonly ILogger<RetentionAnnulmentRequester> _logger;

    public RetentionAnnulmentRequester(
        IRetentionAnnulmentRequestRepository requests,
        IElectronicDocumentSriAnnulment sriAnnulment,
        IAccountsPayableRepository payables,
        IBusinessPartnerRepository partners,
        ILogger<RetentionAnnulmentRequester> logger
    )
    {
        _requests = requests;
        _sriAnnulment = sriAnnulment;
        _payables = payables;
        _partners = partners;
        _logger = logger;
    }

    public async Task<Result<RetentionAnnulmentRequest>> RequestAsync(
        RetentionDocument retention,
        string reason,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (retention.Status != RetentionStatus.Issued)
            return Result<RetentionAnnulmentRequest>.ValidationFailure(
                "Solo se puede solicitar la anulación ante el SRI de una retención emitida."
            );

        var open = await _requests.GetOpenByRetentionAsync(
            retention.TenantId,
            retention.CompanyId,
            retention.Id,
            ct
        );
        if (open is not null)
            return Result<RetentionAnnulmentRequest>.ValidationFailure(
                ElectronicDocumentSourceCancellation.AnnulmentPendingMessage,
                ApiResponseCodes.ElectronicDocuments.AnnulmentPending
            );

        var requestId = Guid.NewGuid();
        var electronic = await _sriAnnulment.BeginAsync(
            retention.TenantId,
            retention.CompanyId,
            RetentionElectronicDocumentSource.SourceModule,
            retention.Id,
            requestId,
            userId,
            ct
        );
        if (!electronic.IsSuccess)
            return Result<RetentionAnnulmentRequest>.ValidationFailure(electronic.Error!, electronic.Code);

        if (
            RetentionCanceller.TryResolveAccountsPayableOriginType(
                retention.SourceDocumentType,
                out var originType
            )
        )
        {
            var payable = await _payables.GetByOriginAsync(
                retention.TenantId,
                retention.CompanyId,
                originType,
                retention.SourceDocumentId,
                ct
            );
            // Regla del agregado: rechaza si ya hay pagos/créditos (la anulación del origen no podría completarse).
            payable?.PlaceAnnulmentHold(requestId, userId);
        }

        var subject = await _partners.GetByIdAsync(retention.SubjectBusinessPartnerId, ct);
        var request = RetentionAnnulmentRequest.Create(
            retention,
            electronic.Value!.Id,
            electronic.Value.AccessKey?.Value
                ?? throw new InvalidOperationException(
                    "Invariante violada: un comprobante autorizado sin clave de acceso."
                ),
            subject?.Identification.Number ?? string.Empty,
            subject?.Name.LegalName ?? string.Empty,
            reason,
            userId,
            requestId
        );
        await _requests.AddAsync(request, ct);
        LogRequested(request.Id, retention.Id);
        return Result<RetentionAnnulmentRequest>.Success(request);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[Retentions] Anulación SRI solicitada {RequestId} para la retención {RetentionId}")]
    private partial void LogRequested(Guid requestId, Guid retentionId);
}
