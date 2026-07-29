namespace ERP.Application.Common;

public static class ProvisionalTaxIdGenerator
{
    private const string Prefix = "TMP-EC-";

    public static string Create() => $"{Prefix}{Guid.NewGuid():N}"[..15];

    public static bool IsProvisional(string? taxId) =>
        !string.IsNullOrWhiteSpace(taxId)
        && taxId.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeOfficial(string ruc)
    {
        var t = ruc.Trim();
        if (t.Length == 13)
            return t;
        if (t.Length < 13)
            return t.PadRight(13, '0')[..13];
        return t[..13];
    }
}
