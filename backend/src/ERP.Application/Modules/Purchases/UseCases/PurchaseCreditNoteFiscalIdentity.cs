using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Constants;

namespace ERP.Application.Modules.Purchases.UseCases;

internal static class PurchaseCreditNoteFiscalIdentity
{
    public static bool IsNumber(string? number)
    {
        var parts = number?.Trim().Split('-');
        return parts is { Length: 3 } && parts[0].Length == 3 && parts[1].Length == 3
            && parts[2].Length == 9 && parts.All(p => p.All(c => c is >= '0' and <= '9'));
    }

    public static bool IsCompatible(string? number, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return true;
        if (!IsNumber(number)) return false;
        try
        {
            var value = AccessKey.Create(key).Value;
            return value[8..10] == SriDocumentTypeCodes.CreditNote
                && value[24..39] == number!.Trim().Replace("-", "", StringComparison.Ordinal);
        }
        catch (ArgumentException) { return false; }
    }

    public static bool HasDifferentIssuer(string? key, string supplierTaxId) =>
        key is { Length: AccessKey.Length } && key.All(c => c is >= '0' and <= '9')
        && key[8..10] == SriDocumentTypeCodes.CreditNote && key[10..23] != supplierTaxId.Trim();
}
