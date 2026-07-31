namespace ERP.Domain.Modules.Caja.Enums;

public enum CashReferenceType
{
    None = 0,
    SalesInvoice = 1,

    /// <summary>Reembolso en efectivo de una devolución de venta (P0-01, Fase 6).</summary>
    SalesReturn = 2,
}
