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

    /// <summary>
    /// Crea un asiento contable para una factura de venta autorizada.
    /// Debit → primera cuenta de tipo Activo (Cuentas por Cobrar / Caja) activa del tenant.
    /// Credit → primera cuenta de tipo Ingreso (Ventas) activa del tenant.
    /// </summary>
    Task<Result<Guid>> CrearAsientoVentaAsync(
        Guid     ventaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct);

    /// <summary>
    /// Asiento de gasto aprobado: débito a cuenta de gasto (según categoría si existe coincidencia por nombre),
    /// crédito a caja/banco (primer activo de tipo Activo, naturaleza Deudor).
    /// </summary>
    Task<Result<Guid>> CrearAsientoGastoAsync(
        Guid     gastoId,
        string   categoriaGasto,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string   descripcion,
        CancellationToken ct);
}
