namespace ERP.Domain.Subscriptions.Exceptions;

/// <summary>
/// Límite duro de plan comercial (<c>commercial_plan_limits</c>) alcanzado.
/// </summary>
public sealed class CommercialPlanLimitExceededException : Exception
{
    public CommercialPlanLimitExceededException(
        string limitCode,
        long? limitValue,
        long currentUsage,
        long requestedIncrement,
        string? message = null)
        : base(message ?? BuildDefaultMessage(limitCode))
    {
        LimitCode = limitCode;
        LimitValue = limitValue;
        CurrentUsage = currentUsage;
        RequestedIncrement = requestedIncrement;
    }

    public string LimitCode { get; }
    public long? LimitValue { get; }
    public long CurrentUsage { get; }
    public long RequestedIncrement { get; }

    public static string BuildDefaultMessage(string limitCode) =>
        string.Equals(limitCode, "MAX_COMPANIES", StringComparison.OrdinalIgnoreCase)
            ? "Your commercial plan limit for companies has been reached."
            : $"Commercial plan limit '{limitCode}' has been reached.";
}
