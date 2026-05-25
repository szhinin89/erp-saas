using System.Globalization;

namespace ERP.API;

internal static class AccountingQueryParameters
{
    public static bool TryParseReportDateRange(
        string? desde,
        string? hasta,
        out DateTime rangeStart,
        out DateTime rangeEndInclusive)
    {
        rangeStart = default;
        rangeEndInclusive = default;

        if (string.IsNullOrWhiteSpace(desde) || string.IsNullOrWhiteSpace(hasta))
            return false;

        if (!DateOnly.TryParseExact(desde.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateOnly.TryParseExact(hasta.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            return false;

        if (to < from)
            return false;

        rangeStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        rangeEndInclusive = DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        return true;
    }
}
