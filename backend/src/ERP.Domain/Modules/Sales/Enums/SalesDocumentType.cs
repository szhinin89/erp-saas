namespace ERP.Domain.Modules.Sales.Enums;

public enum SalesDocumentType
{
    Invoice,
    CreditNote,
    DebitNote,
    Proforma
}

public static class SalesDocumentTypeExtensions
{
    public static string ToDbValue(this SalesDocumentType type) => type switch
    {
        SalesDocumentType.Invoice    => "INVOICE",
        SalesDocumentType.CreditNote => "CREDIT_NOTE",
        SalesDocumentType.DebitNote  => "DEBIT_NOTE",
        SalesDocumentType.Proforma   => "PROFORMA",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>
    /// Mapea NoteType de SalesNote ("CREDIT"/"DEBIT") al enum.
    /// Consumidor: SalesDocumentMapper al rehidratar notas desde BD.
    /// </summary>
    public static SalesDocumentType FromNoteType(string noteType) =>
        noteType.Trim().ToUpperInvariant() is "CREDIT" or "CREDITO"
            ? SalesDocumentType.CreditNote
            : SalesDocumentType.DebitNote;

    /// <summary>
    /// Reconstruye el enum desde el valor almacenado en BD ("INVOICE", "CREDIT_NOTE"...)
    /// o desde el código SRI de comprobante ("04", "05"...).
    /// Usado por EF Core value converter en SalesDocumentConfiguration.
    /// </summary>
    public static SalesDocumentType FromCode(string? code) => code?.Trim() switch
    {
        "04" or "CREDIT" or "CREDITO" or "CREDIT_NOTE" => SalesDocumentType.CreditNote,
        "05" or "DEBIT"  or "DEBITO"  or "DEBIT_NOTE"  => SalesDocumentType.DebitNote,
        "PROFORMA" or "proforma"                        => SalesDocumentType.Proforma,
        _                                               => SalesDocumentType.Invoice
    };

    /// <summary>
    /// Código SRI del tipo de comprobante para XML de facturación electrónica (codDocSustento).
    /// Consumidor: SriXmlFacturaBuilder, SalesDocumentMapper (Infrastructure).
    /// </summary>
    public static string ToSriDocCode(this SalesDocumentType type) => type switch
    {
        SalesDocumentType.CreditNote => "04",
        SalesDocumentType.DebitNote  => "05",
        SalesDocumentType.Proforma   => "PROFORMA",
        _                            => "01"
    };
}
