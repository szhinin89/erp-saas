using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Services;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — única dueña de reintentos y backoff de
/// <c>CommunicationOutbox</c> (ADR-039 D10). Pura y determinística: el instante lo aporta el caller
/// (TimeProvider).
/// <para>
/// <c>RetryCount</c> cuenta intentos fallidos; <c>MaxRetries</c> es el total de intentos automáticos
/// (copiado al encolar). Transient/Unknown reprograman con backoff hasta agotarlo; Permanent y
/// Configuration terminan en <c>Failed</c> al primer fallo (sin busy-loop).
/// </para>
/// </summary>
public static class CommunicationRetryPolicy
{
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(60);

    public static CommunicationFailureOutcome OnFailure(
        int retryCount,
        int maxRetries,
        CommunicationFailureCategory category,
        DateTime utcNow
    )
    {
        var attempts = retryCount + 1;
        var terminal =
            category
                is CommunicationFailureCategory.Permanent
                    or CommunicationFailureCategory.Configuration
            || attempts >= maxRetries;

        return terminal
            ? new CommunicationFailureOutcome(
                CommunicationStatus.Failed,
                attempts,
                NextAttemptAtUtc: null
            )
            : new CommunicationFailureOutcome(
                CommunicationStatus.Pending,
                attempts,
                utcNow + Backoff(attempts)
            );
    }

    /// <summary>
    /// Fallo atribuido a un intento cuyo lease venció (el worker murió): ese intento ya fue contado al
    /// reclamarlo. Si agotó los intentos, la comunicación termina sin volver a enviarse.
    /// </summary>
    public static bool IsExhausted(int retryCount, int maxRetries) => retryCount >= maxRetries;

    /// <summary>2^n minutos tras el n-ésimo fallo, con tope <see cref="MaxBackoff"/> (sin overflow).</summary>
    public static TimeSpan Backoff(int failedAttempts)
    {
        if (failedAttempts <= 0)
            return TimeSpan.Zero;
        if (failedAttempts >= 6)
            return MaxBackoff;

        var minutes = TimeSpan.FromMinutes(1 << failedAttempts);
        return minutes < MaxBackoff ? minutes : MaxBackoff;
    }
}

public readonly record struct CommunicationFailureOutcome(
    CommunicationStatus Status,
    int RetryCount,
    DateTime? NextAttemptAtUtc
);
