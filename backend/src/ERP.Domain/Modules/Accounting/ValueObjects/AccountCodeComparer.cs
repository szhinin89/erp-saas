namespace ERP.Domain.Modules.Accounting.ValueObjects;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: orden natural por segmentos del código contable.
/// Compara segmento a segmento (separados por '.'), numéricamente cuando ambos segmentos son
/// dígitos, para que "1.1.2" ordene antes de "1.1.10" — un <c>OrderBy(x =&gt; x.Code)</c> lexicográfico
/// simple (SQL <c>ORDER BY code</c> o <see cref="StringComparer.Ordinal"/>) pondría "1.1.10" antes
/// de "1.1.2" porque compara carácter a carácter ('1' &lt; '2'). Reutilizable en repositorio,
/// reportes contables y cualquier consumidor futuro que necesite jerarquía contable confiable.
/// </summary>
public sealed class AccountCodeComparer : IComparer<string>
{
    public static readonly AccountCodeComparer Instance = new();

    private AccountCodeComparer() { }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var xSegments = x.Split('.');
        var ySegments = y.Split('.');
        var length = Math.Min(xSegments.Length, ySegments.Length);

        for (var i = 0; i < length; i++)
        {
            var segmentComparison = CompareSegment(xSegments[i], ySegments[i]);
            if (segmentComparison != 0)
                return segmentComparison;
        }

        return xSegments.Length.CompareTo(ySegments.Length);
    }

    private static int CompareSegment(string a, string b)
    {
        if (IsAllDigits(a) && IsAllDigits(b) && long.TryParse(a, out var an) && long.TryParse(b, out var bn))
        {
            var numericComparison = an.CompareTo(bn);
            return numericComparison != 0 ? numericComparison : string.CompareOrdinal(a, b);
        }

        return string.CompareOrdinal(a, b);
    }

    private static bool IsAllDigits(string value)
    {
        if (value.Length == 0)
            return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        return true;
    }
}
