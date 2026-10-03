using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — entrega de <c>CommunicationOutbox</c> segura con varios
/// workers, nodos o ejecuciones solapadas (ADR-039 D9/D10). La exclusión vive en PostgreSQL
/// (<see cref="CommunicationOutboxDeliveryStore"/>), no en Hangfire.
/// <para>
/// Por iteración: claim de UNA fila (sentencia corta, abre el intento) → scope de esa fila → envío
/// fuera de toda transacción, acotado por <see cref="CommunicationEmailSettings.SmtpTimeout"/> →
/// finalización con fencing (cierra el intento). Una fila por claim mantiene el lease fresco: nunca
/// espera detrás de otros envíos.
/// </para>
/// <para>
/// ZH-COMMUNICATIONS-CONTRACT-01 — scope explícito por fila: Company abre
/// <c>JobExecutionContext(tenant, empresa)</c> y resuelve el perfil de ESA empresa; System no abre
/// contexto de tenant y resuelve solo el perfil de instancia. Nunca hay fallback cruzado.
/// </para>
/// <para>
/// Garantía real: un solo claim vigente por fila + fencing + entrega AL MENOS UNA VEZ. Si SMTP
/// acepta el mensaje y el proceso muere antes de <c>MarkSent</c>, la fila se recupera al vencer el
/// lease y se reenvía (mismo Message-ID); no hay exactly-once con SMTP.
/// </para>
/// </summary>
public sealed partial class CommunicationOutboxProcessor : ICommunicationOutboxProcessor
{
    private const int BatchSize = 50;

    private readonly CommunicationOutboxDeliveryStore _store;
    private readonly IEmailSender _emailSender;
    private readonly ICommunicationSettingsResolver _settingsResolver;
    private readonly TimeProvider _time;
    private readonly ILogger<CommunicationOutboxProcessor> _logger;

    public CommunicationOutboxProcessor(
        CommunicationOutboxDeliveryStore store,
        IEmailSender emailSender,
        ICommunicationSettingsResolver settingsResolver,
        TimeProvider time,
        ILogger<CommunicationOutboxProcessor> logger
    )
    {
        _store = store;
        _emailSender = emailSender;
        _settingsResolver = settingsResolver;
        _time = time;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        for (var processed = 0; processed < BatchSize; processed++)
        {
            ct.ThrowIfCancellationRequested();
            var claim = await _store.ClaimNextAsync(UtcNow(), CommunicationDeliveryTiming.Lease, ct);
            if (claim is null)
                return;

            await ProcessClaimAsync(claim, ct);
        }
    }

    private async Task ProcessClaimAsync(ClaimedCommunication claim, CancellationToken ct)
    {
        var scope = claim.GetScope();

        // Scope de la fila (fail-closed): settings, carga y finalización se resuelven con el alcance de
        // ESTA comunicación, nunca con los de otra del mismo lote. System no tiene tenant/empresa.
        using var context = scope.Kind == CommunicationScopeKind.Company
            ? JobExecutionContext.Begin(scope.TenantId!.Value, scope.CompanyId!.Value)
            : null;

        if (claim.Recovered)
        {
            LogRecoveredAfterLease(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount, claim.MaxRetries, claim.AttemptNumber);
            if (CommunicationRetryPolicy.IsExhausted(claim.RetryCount, claim.MaxRetries))
            {
                // El intento del worker muerto ya se contó al reclamar; agotado, no se reenvía.
                await FinalizeFailureAsync(
                    claim,
                    new CommunicationFailureOutcome(CommunicationStatus.Failed, claim.RetryCount, null),
                    CommunicationFailureCategory.Unknown,
                    "Lease vencido sin finalizar (worker interrumpido); intentos agotados.",
                    providerCode: null,
                    ct
                );
                return;
            }
        }
        else
        {
            LogClaimed(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount, claim.MaxRetries, claim.AttemptNumber);
        }

        EmailDeliveryReceipt receipt;
        try
        {
            var communication = await _store.LoadOwnedAsync(claim, ct);
            if (communication is null)
            {
                LogClaimLost(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount);
                return;
            }

            if (communication.Channel != CommunicationChannel.Email)
            {
                await FailAsync(claim, CommunicationFailureCategory.Permanent, $"El canal {communication.Channel} no tiene procesador.", null, ct);
                return;
            }

            var settings = await _settingsResolver.ResolveEmailAsync(scope, ct);
            if (!settings.CanSend)
            {
                await FailAsync(
                    claim,
                    CommunicationFailureCategory.Configuration,
                    "La configuración SMTP de Communications está incompleta o inactiva; corregirla y reencolar.",
                    null,
                    ct
                );
                return;
            }

            receipt = await SendWithTimeoutAsync(communication, settings, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado del host: no se finaliza; el lease vence y otro worker recupera la fila.
            throw;
        }
        catch (Exception ex)
        {
            var category = CommunicationFailureClassifier.Classify(ex);
            if (category == CommunicationFailureCategory.Unknown)
                LogUnknownFailureDetail(ex, claim.Id);
            await FailAsync(
                claim,
                category,
                CommunicationFailureClassifier.SafeDescription(ex),
                CommunicationFailureClassifier.ProviderCode(ex),
                ct
            );
            return;
        }

        // Fuera del try: SMTP ya aceptó el mensaje. Si esta finalización falla (p. ej. BD caída), la
        // fila NO se marca fallida (sería un reintento seguro de un correo entregado): queda en
        // Processing y el lease la recupera — la ventana at-least-once documentada.
        if (await _store.MarkSentAsync(claim, receipt, UtcNow(), ct))
            LogDeliverySucceeded(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount, claim.AttemptNumber);
        else
            LogClaimLost(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount);
    }

    private async Task<EmailDeliveryReceipt> SendWithTimeoutAsync(
        CommunicationOutbox communication,
        CommunicationEmailSettings settings,
        CancellationToken ct
    )
    {
        // Cota del processor para cualquier IEmailSender (el SMTP real además fija su propio timeout).
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.SmtpTimeout);
        try
        {
            return await _emailSender.SendAsync(ToEmailMessage(communication), settings, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"El envío superó el timeout SMTP de {settings.SmtpTimeout.TotalSeconds:0} s."
            );
        }
    }

    private Task FailAsync(
        ClaimedCommunication claim,
        CommunicationFailureCategory category,
        string errorSafeText,
        string? providerCode,
        CancellationToken ct
    ) =>
        FinalizeFailureAsync(
            claim,
            CommunicationRetryPolicy.OnFailure(claim.RetryCount, claim.MaxRetries, category, UtcNow()),
            category,
            errorSafeText,
            providerCode,
            ct
        );

    private async Task FinalizeFailureAsync(
        ClaimedCommunication claim,
        CommunicationFailureOutcome outcome,
        CommunicationFailureCategory category,
        string errorSafeText,
        string? providerCode,
        CancellationToken ct
    )
    {
        if (!await _store.MarkFailedAsync(claim, outcome, category, errorSafeText, providerCode, UtcNow(), ct))
        {
            LogClaimLost(claim.Id, claim.TenantId, claim.CompanyId, claim.Purpose, claim.Channel, claim.RetryCount);
            return;
        }

        LogDeliveryFailed(
            claim.Id,
            claim.TenantId,
            claim.CompanyId,
            claim.Purpose,
            claim.Channel,
            category,
            outcome.RetryCount,
            claim.MaxRetries,
            outcome.Status,
            outcome.NextAttemptAtUtc
        );
    }

    private DateTime UtcNow() => _time.GetUtcNow().UtcDateTime;

    private static EmailMessage ToEmailMessage(CommunicationOutbox communication) =>
        new(
            communication.RecipientEmail!,
            communication.RecipientName,
            communication.Subject,
            communication.BodyHtml,
            communication.BodyText,
            communication.Attachments
                .Select(a => new EmailAttachment(a.FileName, a.ContentType, a.FileStoragePath, a.BinaryContent))
                .ToList(),
            communication.Id
        );

    // Eventos estructurados: nunca cuerpo, destinatario, adjuntos, credenciales ni tokens.
    // Tenant/empresa null = comunicación System (instancia).
    [LoggerMessage(EventId = 4201, EventName = "CommunicationClaimed", Level = LogLevel.Debug,
        Message = "Communications: claimed {CommunicationId} tenant={TenantId} company={CompanyId} purpose={Purpose} channel={Channel} retry={RetryCount}/{MaxRetries} attempt={AttemptNumber}")]
    private partial void LogClaimed(Guid communicationId, Guid? tenantId, Guid? companyId, string purpose, string channel, int retryCount, int maxRetries, int attemptNumber);

    [LoggerMessage(EventId = 4202, EventName = "CommunicationRecoveredAfterLease", Level = LogLevel.Warning,
        Message = "Communications: recovered {CommunicationId} after expired lease tenant={TenantId} company={CompanyId} purpose={Purpose} channel={Channel} retry={RetryCount}/{MaxRetries} attempt={AttemptNumber}")]
    private partial void LogRecoveredAfterLease(Guid communicationId, Guid? tenantId, Guid? companyId, string purpose, string channel, int retryCount, int maxRetries, int attemptNumber);

    [LoggerMessage(EventId = 4203, EventName = "CommunicationDeliverySucceeded", Level = LogLevel.Information,
        Message = "Communications: sent {CommunicationId} tenant={TenantId} company={CompanyId} purpose={Purpose} channel={Channel} retry={RetryCount} attempt={AttemptNumber}")]
    private partial void LogDeliverySucceeded(Guid communicationId, Guid? tenantId, Guid? companyId, string purpose, string channel, int retryCount, int attemptNumber);

    [LoggerMessage(EventId = 4204, EventName = "CommunicationDeliveryFailed", Level = LogLevel.Warning,
        Message = "Communications: failed {CommunicationId} tenant={TenantId} company={CompanyId} purpose={Purpose} channel={Channel} category={FailureCategory} retry={RetryCount}/{MaxRetries} status={Status} nextAttempt={NextAttemptAtUtc}")]
    private partial void LogDeliveryFailed(Guid communicationId, Guid? tenantId, Guid? companyId, string purpose, string channel, CommunicationFailureCategory failureCategory, int retryCount, int maxRetries, CommunicationStatus status, DateTime? nextAttemptAtUtc);

    [LoggerMessage(EventId = 4205, EventName = "CommunicationClaimLost", Level = LogLevel.Warning,
        Message = "Communications: claim lost for {CommunicationId} tenant={TenantId} company={CompanyId} purpose={Purpose} channel={Channel} retry={RetryCount}; another worker owns it, result discarded")]
    private partial void LogClaimLost(Guid communicationId, Guid? tenantId, Guid? companyId, string purpose, string channel, int retryCount);

    // Solo para fallos no clasificados: la traza es imprescindible para diagnosticarlos.
    [LoggerMessage(EventId = 4206, EventName = "CommunicationUnknownFailure", Level = LogLevel.Warning,
        Message = "Communications: unclassified delivery failure for {CommunicationId}")]
    private partial void LogUnknownFailureDetail(Exception ex, Guid communicationId);
}
