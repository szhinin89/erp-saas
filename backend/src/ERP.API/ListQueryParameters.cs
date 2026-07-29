namespace ERP.API;

internal static class ListQueryParameters
{
    public static (int PageNumber, int PageSize) ParsePaged(IQueryCollection query, int defaultPageSize = 20)
    {
        var pageNumber = 1;
        var pageSize = defaultPageSize;
        if (query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni))
            pageNumber = pni;
        if (query.TryGetValue("pageSize", out var psv) && int.TryParse(psv, out var psi))
            pageSize = psi;
        return (pageNumber, pageSize);
    }

    public static Guid? ParseOptionalGuid(IQueryCollection query, string key)
    {
        if (query.TryGetValue(key, out var value) && Guid.TryParse(value, out var id))
            return id;
        return null;
    }

    public static string? ParseOptionalString(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var value))
            return null;
        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static (DateOnly? From, DateOnly? To) ParseDateOnlyRange(
        IQueryCollection query,
        string fromKey = "dateFrom",
        string toKey = "dateTo")
    {
        DateOnly? from = null;
        DateOnly? to = null;
        if (query.TryGetValue(fromKey, out var fromValue) && DateOnly.TryParse(fromValue, out var fromDate))
            from = fromDate;
        if (query.TryGetValue(toKey, out var toValue) && DateOnly.TryParse(toValue, out var toDate))
            to = toDate;
        return (from, to);
    }

    public static (DateTime? From, DateTime? To) ParseDateTimeRange(
        IQueryCollection query,
        string fromKey = "desde",
        string toKey = "hasta")
    {
        DateTime? from = null;
        DateTime? to = null;
        if (query.TryGetValue(fromKey, out var fromValue) && DateTime.TryParse(fromValue, out var fromDate))
            from = fromDate;
        if (query.TryGetValue(toKey, out var toValue) && DateTime.TryParse(toValue, out var toDate))
            to = toDate;
        return (from, to);
    }

    public static (int Skip, int Take) ParseSkipTake(IQueryCollection query, int defaultTake = 50)
    {
        var skip = 0;
        var take = defaultTake;
        if (query.TryGetValue("skip", out var skipValue) && int.TryParse(skipValue, out var skipParsed))
            skip = skipParsed;
        if (query.TryGetValue("take", out var takeValue) && int.TryParse(takeValue, out var takeParsed))
            take = takeParsed;
        return (skip, take);
    }
}
