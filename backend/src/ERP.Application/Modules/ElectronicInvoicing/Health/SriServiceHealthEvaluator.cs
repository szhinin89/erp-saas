using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Enums;

namespace ERP.Application.Modules.ElectronicInvoicing.Health;

/// <summary>Una observación ya registrada por el monitor (sin datos de tenant ni del comprobante).</summary>
public readonly record struct SriServiceObservation(
    SriObservationOutcome Outcome,
    TimeSpan? Elapsed,
    DateTime ObservedAtUtc
)
{
    public bool IsProbe =>
        Outcome is SriObservationOutcome.ProbeReachable or SriObservationOutcome.ProbeUnreachable;

    public bool IsCallFailure =>
        Outcome
            is SriObservationOutcome.TransportFailure
                or SriObservationOutcome.InvalidResponse
                or SriObservationOutcome.Undetermined;
}

/// <summary>
/// Única regla de derivación del estado técnico de un servicio SRI a partir de evidencia vigente.
/// Pura (sin reloj ni estado): el llamador entrega solo observaciones vigentes, de la más reciente a
/// la más antigua. Principios: sin evidencia → Sin verificar; una caída solo se indica con fallos de
/// transporte consecutivos en llamadas reales; un ping (evidencia débil) nunca la diagnostica; una
/// respuesta fiscal negativa o un polling sin decisión es una respuesta válida.
/// </summary>
public static class SriServiceHealthEvaluator
{
    public static SriServiceStatusDto Evaluate(
        IReadOnlyList<SriServiceObservation> freshNewestFirst,
        SriHealthOptions options
    )
    {
        if (freshNewestFirst.Count == 0)
            return Unverified(SriServiceHealthReason.NoRecentEvidence);

        var lastObservedAt = freshNewestFirst[0].ObservedAtUtc;
        var calls = freshNewestFirst.Where(o => !o.IsProbe).ToList();

        if (calls.Count == 0)
        {
            return freshNewestFirst[0].Outcome == SriObservationOutcome.ProbeReachable
                ? Status(SriServiceHealthState.Available, SriServiceHealthReason.ProbeReachable, SriHealthEvidence.Probe, lastObservedAt)
                : Status(SriServiceHealthState.Intermittent, SriServiceHealthReason.ProbeUnreachable, SriHealthEvidence.Probe, lastObservedAt);
        }

        var latestCall = calls[0];
        var probesAfterLatestCall = freshNewestFirst
            .TakeWhile(o => o.IsProbe)
            .ToList();

        var consecutiveTransportFailures = calls
            .TakeWhile(o => o.Outcome == SriObservationOutcome.TransportFailure)
            .Count();
        if (consecutiveTransportFailures >= Math.Max(1, options.ConsecutiveTransportFailuresForUnavailable))
        {
            // El WSDL responde aunque las llamadas reales fallaron: no se afirma la caída.
            return probesAfterLatestCall.Any(o => o.Outcome == SriObservationOutcome.ProbeReachable)
                ? Status(SriServiceHealthState.Intermittent, SriServiceHealthReason.ProbeReachableAfterFailures, SriHealthEvidence.Probe, lastObservedAt)
                : Status(SriServiceHealthState.Unavailable, SriServiceHealthReason.ConsecutiveTransportFailures, SriHealthEvidence.Call, lastObservedAt);
        }

        if (latestCall.IsCallFailure)
        {
            var reason = latestCall.Outcome switch
            {
                SriObservationOutcome.TransportFailure => SriServiceHealthReason.RecentTransportFailure,
                SriObservationOutcome.InvalidResponse => SriServiceHealthReason.RecentInvalidResponse,
                _ => SriServiceHealthReason.RecentUndeterminedFailure,
            };
            return Status(SriServiceHealthState.Intermittent, reason, SriHealthEvidence.Call, lastObservedAt);
        }

        if (calls.Any(o => o.IsCallFailure))
            return Status(SriServiceHealthState.Intermittent, SriServiceHealthReason.RecoveredAfterFailures, SriHealthEvidence.Call, lastObservedAt);

        if (probesAfterLatestCall.Count > 0 && probesAfterLatestCall[0].Outcome == SriObservationOutcome.ProbeUnreachable)
            return Status(SriServiceHealthState.Intermittent, SriServiceHealthReason.ProbeUnreachable, SriHealthEvidence.Probe, lastObservedAt);

        // Solo las llamadas con duración medible (sin esperas de polling) juzgan la lentitud.
        var latestMeasured = calls.FirstOrDefault(o => o.Elapsed.HasValue);
        if (latestMeasured.Elapsed is { } elapsed
            && elapsed >= TimeSpan.FromSeconds(options.SlowCallThresholdSeconds))
            return Status(SriServiceHealthState.Slow, SriServiceHealthReason.SlowResponse, SriHealthEvidence.Call, lastObservedAt);

        return latestCall.Outcome == SriObservationOutcome.RespondedWithoutDecision
            ? Status(SriServiceHealthState.Available, SriServiceHealthReason.RespondedWithoutDecision, SriHealthEvidence.Call, lastObservedAt)
            : Status(SriServiceHealthState.Available, SriServiceHealthReason.RecentResponses, SriHealthEvidence.Call, lastObservedAt);
    }

    public static SriServiceStatusDto Unverified(SriServiceHealthReason reason) =>
        new(SriServiceHealthState.Unverified, reason, SriHealthEvidence.None, null);

    private static SriServiceStatusDto Status(
        SriServiceHealthState state,
        SriServiceHealthReason reason,
        SriHealthEvidence evidence,
        DateTime lastObservedAtUtc
    ) => new(state, reason, evidence, lastObservedAtUtc);
}
