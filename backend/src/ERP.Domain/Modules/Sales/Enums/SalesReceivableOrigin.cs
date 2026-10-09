namespace ERP.Domain.Modules.Sales.Enums;

/// <summary>
/// IL-5A — origen de una <c>SalesReceivable</c>. <see cref="Invoice"/> nace de una factura de venta
/// autorizada (con <c>InvoiceId</c>); <see cref="InitialBalance"/> es un saldo pendiente al corte
/// cargado desde la Carga Inicial de CxC — nunca reconstruye la venta histórica, por eso no tiene
/// factura y lleva su propio número, fecha de emisión, sucursal y lote de origen. Persistido como
/// entero: nunca reordenar/renumerar.
/// </summary>
public enum SalesReceivableOrigin
{
    Invoice = 1,
    InitialBalance = 2,
}
