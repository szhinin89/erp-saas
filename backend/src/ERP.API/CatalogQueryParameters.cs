namespace ERP.API;

internal static class CatalogQueryParameters
{
    /// <summary>
    /// activeStatus=all|active|inactive. Compatibilidad: onlyActive=true → activos; onlyActive=false → todos.
    /// </summary>
    public static bool? ParseActiveFilter(IQueryCollection query)
    {
        if (query.TryGetValue("activeStatus", out var st))
        {
            return st.ToString().Trim().ToLowerInvariant() switch
            {
                "all" => null,
                "inactive" => false,
                "active" => true,
                _ => true,
            };
        }

        if (query.TryGetValue("onlyActive", out var oa) && bool.TryParse(oa, out var legacy))
            return legacy ? true : null;

        return true;
    }

    public static string? ParseSearch(IQueryCollection query)
    {
        if (query.TryGetValue("search", out var s))
        {
            var t = s.ToString().Trim();
            return t.Length == 0 ? null : t;
        }

        return null;
    }
}
