using ERP.Application.Common;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Servicio de alto nivel para operaciones contables que requieren lógica de negocio
/// específica del dominio de compras / gastos.
/// </summary>
public interface IAccountingService
{
    /// <summary>
    /// Crea un asiento contable para una factura de compra aprobada.
    /// Debit → primera cuenta de tipo Gasto/Compras activa del tenant.
    /// Credit → primera cuenta de tipo Pasivo (Cuentas por Pagar) activa del tenant.
    /// </summary>
    /// <returns>ID del asiento creado, o Failure si no se encuentran las cuentas configuradas.</returns>
    Task<Result<Guid>> CrearAsientoCompraAsync(
        Guid     compraId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct);
}
