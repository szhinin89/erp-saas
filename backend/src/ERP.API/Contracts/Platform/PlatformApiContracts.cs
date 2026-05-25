namespace ERP.API.Contracts.Platform;

public sealed record UpdatePlatformSubscriberCompanyBody(
    string Name,
    string Slug,
    string? Ruc,
    string? ShortName,
    string? TradeName,
    string? Dinardap,
    string? LogoUrl,
    int DisplayOrder,
    int Priority);

public sealed record UpdatePlatformGlobalParametersBody(bool ElectronicBillingTrialEnabled);

public sealed record UpdatePlatformOperationalSettingsBody(
    string Currency,
    string Language,
    string Timezone,
    string? InvoicePrefix,
    int DefaultCreditDays);

public sealed record SubscriberMenuPutBody(string MenuConfigJson);

public sealed record SubscriberLifecycleNoteBody(string? Notes = null);
public sealed record SubscriberSuspendBody(string? Reason = null);
public sealed record SubscriberTrialBody(DateTime TrialEndsAtUtc);
public sealed record SubscriberGracePeriodBody(DateTime GracePeriodEndsAtUtc, string? Reason = null);
public sealed record SubscriberChangePlanBody(string NewPlanCode, string? Notes = null);

public sealed record UpsertGlobalConfigBody(string Key, string Value, string DataType = "string");
public sealed record UpsertScopedConfigBody(string Key, string Value, string DataType = "string");
