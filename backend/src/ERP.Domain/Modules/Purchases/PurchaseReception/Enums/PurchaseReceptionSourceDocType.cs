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
        if (string.IsNullOrWhiteSpace(raw))
            return PurchaseReceptionSourceDocType.Unknown;

        return raw.Trim().ToUpperInvariant() switch
        {
            "FACTURA" => PurchaseReceptionSourceDocType.Invoice,
            "NOTA DE CRÉDITO" or "NOTA DE CREDITO" => PurchaseReceptionSourceDocType.CreditNote,
            "NOTA DE DÉBITO" or "NOTA DE DEBITO" => PurchaseReceptionSourceDocType.DebitNote,
            "COMPROBANTE DE RETENCIÓN" or "COMPROBANTE DE RETENCION" =>
                PurchaseReceptionSourceDocType.Retention,
            _ => PurchaseReceptionSourceDocType.Unknown,
        };
    }
}
