namespace ERP.Application.Subscriptions.CommercialPlanLimits;

public sealed record CommercialLimitEvaluationResult(
    string LimitCode,
    bool Allowed,
    long CurrentUsage,
    long? LimitValue,
    long RequestedIncrement,
    bool IsHardLimit,
    string? DenialReason)
{
    public static CommercialLimitEvaluationResult Allow(
        string limitCode,
        long currentUsage,
        long? limitValue,
        long requestedIncrement,
        bool isHardLimit)
        => new(limitCode, true, currentUsage, limitValue, requestedIncrement, isHardLimit, null);

    public static CommercialLimitEvaluationResult Deny(
        string limitCode,
        long currentUsage,
        long? limitValue,
        long requestedIncrement,
        bool isHardLimit,
        string reason)
        => new(limitCode, false, currentUsage, limitValue, requestedIncrement, isHardLimit, reason);
}
