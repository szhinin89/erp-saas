namespace ERP.Domain.Subscriptions.Entities;

/// <summary>
/// Cuota / límite comercial del plan (separado de <see cref="CommercialPlanFeature"/>).
/// </summary>
public sealed class CommercialPlanLimit
{
    public const int CodeMaxLen = 64;
    public const int PeriodTypeMaxLen = 16;

    public static class Codes
    {
        public const string MaxCompanies = "MAX_COMPANIES";
        public const string MaxUsers = "MAX_USERS";
        public const string MaxStorageMb = "MAX_STORAGE_MB";
        public const string MaxProducts = "MAX_PRODUCTS";
        public const string MaxMonthlyInvoices = "MAX_MONTHLY_INVOICES";
        public const string MaxApiRequests = "MAX_API_REQUESTS";
        public const string MaxAiTokens = "MAX_AI_TOKENS";
        public const string MaxBranches = "MAX_BRANCHES";
        public const string MaxWarehouses = "MAX_WAREHOUSES";
    }

    public Guid Id { get; private set; }
    public Guid CommercialPlanId { get; private set; }
    public string LimitCode { get; private set; } = null!;
    public long LimitValue { get; private set; }
    public string PeriodType { get; private set; } = "none";
    public bool IsHardLimit { get; private set; } = true;

    private CommercialPlanLimit() { }

    public static CommercialPlanLimit Create(
        Guid planId,
        string limitCode,
        long limitValue,
        string periodType = "none",
        bool isHardLimit = true)
        => new()
        {
            Id = Guid.NewGuid(),
            CommercialPlanId = planId,
            LimitCode = limitCode.Trim().ToUpperInvariant(),
            LimitValue = limitValue,
            PeriodType = periodType.Trim().ToLowerInvariant(),
            IsHardLimit = isHardLimit,
        };
}
