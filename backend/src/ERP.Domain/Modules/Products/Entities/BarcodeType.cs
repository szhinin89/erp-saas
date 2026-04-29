namespace ERP.Domain.Products.Entities;

public enum BarcodeType
{
    EAN13    = 1,
    EAN8     = 2,
    QR       = 3,
    Code128  = 4,
    Internal = 5,   // Código interno de la empresa
    Other    = 99
}
