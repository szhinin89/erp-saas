namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// Tipo de comprobante de origen detectado en la columna <c>TIPO_COMPROBANTE</c> del TXT del SRI.
/// Fase 1 solo procesa <see cref="Invoice"/> (Factura) — los demás valores se reconocen para
/// permitir, en una fase futura, un importador dedicado por tipo sin rediseñar el parser.
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
        if (string.IsNullOrWhiteSpace(raw)) return PurchaseReceptionSourceDocType.Unknown;

        return raw.Trim().ToUpperInvariant() switch
        {
            "FACTURA" => PurchaseReceptionSourceDocType.Invoice,
            "NOTA DE CRÉDITO" or "NOTA DE CREDITO" => PurchaseReceptionSourceDocType.CreditNote,
            "NOTA DE DÉBITO" or "NOTA DE DEBITO" => PurchaseReceptionSourceDocType.DebitNote,
            "COMPROBANTE DE RETENCIÓN" or "COMPROBANTE DE RETENCION" => PurchaseReceptionSourceDocType.Retention,
            _ => PurchaseReceptionSourceDocType.Unknown,
        };
    }
}
