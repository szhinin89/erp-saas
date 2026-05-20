namespace ERP.Domain.Subscriptions;

/// <summary>Ciclo de facturación del plan (persistido como string en BD para evolución sin migraciones enum).</summary>
public static class CommercialBillingCycle
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Yearly = "yearly";
    public const string OneTime = "one_time";

    public static bool IsValid(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v is Monthly or Quarterly or Yearly or OneTime;
    }
}
