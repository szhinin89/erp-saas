namespace ERP.API.Contracts.Platform;

public sealed record UpdatePlatformSubscriberCompanyBody(
    string Name,
    string Slug,
    int DisplayOrder,
    int Priority,
    string PreferredLanguage = "es");

public sealed record SubscriberMenuPutBody(string MenuConfigJson);

public sealed record SubscriberLifecycleNoteBody(string? Notes = null);
public sealed record SubscriberSuspendBody(string? Reason = null);
public sealed record SubscriberTrialBody(DateTime TrialEndsAtUtc);
public sealed record SubscriberGracePeriodBody(DateTime GracePeriodEndsAtUtc, string? Reason = null);
public sealed record SubscriberChangePlanBody(string NewPlanCode, string? Notes = null);

public sealed record UpsertGlobalConfigBody(string Key, string Value, string DataType = "string");
public sealed record UpsertScopedConfigBody(string Key, string Value, string DataType = "string");
