namespace ERP.Domain.Common;

public static class UtcDateTime
{
    // Zone-less document dates follow the existing reception import convention: UTC.
    // Do not use the server's local timezone to interpret Unspecified values.
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? Normalize(DateTime? value) =>
        value.HasValue ? Normalize(value.Value) : null;
}
