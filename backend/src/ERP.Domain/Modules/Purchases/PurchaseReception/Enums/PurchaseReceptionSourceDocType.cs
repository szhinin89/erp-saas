using System.Globalization;
using System.Text;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// Tipo de comprobante de origen detectado en la columna <c>TIPO_COMPROBANTE</c> del TXT del SRI.
/// Fase 1 solo procesa <see cref="Invoice"/> (Factura) — los demás valores se reconocen para
/// permitir, en una fase futura, un importador dedicado por tipo sin rediseñar el parser.
/// Enum técnico de clasificación de texto (deriva de la etiqueta en español del TXT, ej. "FACTURA")
/// — no es fuente fiscal ni equivale al código SRI <c>codDoc</c>. El documento persistido también
/// guarda por separado el <c>DocTypeCode</c> real (string, derivado del XML <c>codDoc</c> cuando
/// existe) — ambos campos describen el mismo comprobante desde dos fuentes de importación distintas
/// (TXT vs XML) y no deben confundirse entre sí.
/// </summary>
public enum PurchaseReceptionSourceDocType
{
    Unknown = 0,
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    Retention = 4,
}

public static class PurchaseReceptionSourceDocTypeMapper
{
    public static PurchaseReceptionSourceDocType FromRawText(string? raw)
    {
        var normalized = Normalize(raw);
        if (normalized is null)
            return PurchaseReceptionSourceDocType.Unknown;

        return normalized switch
        {
            "FACTURA" => PurchaseReceptionSourceDocType.Invoice,
            "NOTA DE CREDITO" or "NOTA CREDITO" => PurchaseReceptionSourceDocType.CreditNote,
            "NOTA DE DEBITO" => PurchaseReceptionSourceDocType.DebitNote,
            "COMPROBANTE DE RETENCION" => PurchaseReceptionSourceDocType.Retention,
            _ => PurchaseReceptionSourceDocType.Unknown,
        };
    }

    /// <summary>
    /// TIPO_COMPROBANTE del TXT del SRI llega con variaciones de acento, mayúsculas/minúsculas,
    /// espacios múltiples e incluso sin la palabra "de" ("Nota Crédito" en vez de "Nota de
    /// Crédito") — normaliza a mayúsculas, sin diacríticos y con espacio simple para que la
    /// clasificación no dependa de una comparación exacta del texto original.
    /// </summary>
    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var decomposed = raw.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        var words = stripped
            .ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? null : string.Join(' ', words);
    }
}
