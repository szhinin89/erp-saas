namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Política de reintento automático (job Hangfire) para documentos varados en Signed/Received.
/// Único lugar que declara el número de intentos y el backoff — nunca hardcodeado en el job
/// ni en el issuer.
/// </summary>
public static class ElectronicDocumentRetryPolicy
{
    public const int MaxAttempts = 5;

    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(16),
    ];

    /// <summary>
    /// True si el documento ya agotó menos de <see cref="MaxAttempts"/> intentos y transcurrió
    /// el backoff correspondiente desde <paramref name="lastAttemptUtc"/>. Sin intento previo,
    /// siempre es elegible.
    /// </summary>
    public static bool IsEligibleForAutomaticRetry(
        int retryCount,
        DateTime? lastAttemptUtc,
        DateTime nowUtc
    )
    {
        if (retryCount >= MaxAttempts)
            return false;
        if (lastAttemptUtc is null)
            return true;

        var backoffIndex = Math.Min(retryCount, BackoffSchedule.Length - 1);
        return nowUtc >= lastAttemptUtc.Value.Add(BackoffSchedule[backoffIndex]);
    }
}
