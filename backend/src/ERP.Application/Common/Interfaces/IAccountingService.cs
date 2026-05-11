using ERP.Application.Common;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Servicio de alto nivel para operaciones contables que requieren lógica de negocio
/// específica del dominio de compras / gastos.
/// Si existe <see cref="ICuentaContableService"/> / configuración por tenant, se usan esas cuentas;
/// en caso contrario se aplica la heurística por tipo de cuenta del plan.
/// </summary>
public interface IAccountingService
{
    /// <summary>
    /// Crea un asiento contable para una factura de compra aprobada.
    /// Con configuración explícita: débito inventario + IVA compras (si aplica), crédito proveedores por el total.
    /// Sin configuración: débito primera cuenta de Gasto (débito), crédito primera Pasivo (crédito), ambas con movimientos permitidos.
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
    /// Con configuración: débito clientes por total, crédito ventas por subtotal e IVA ventas por el impuesto (si aplica).
    /// Sin configuración: débito / crédito en primera cuenta Activo (débito) e Ingreso (crédito) con movimientos permitidos.
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
    /// Asiento de gasto aprobado: débito según mapeo por categoría o heurística por nombre;
    /// crédito a cuenta de efectivo/banco configurada o primer activo deudor con movimientos permitidos.
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

    /// <summary>Reversión parcial de ventas por nota de crédito (débito ventas/IVA, crédito clientes).</summary>
    Task<Result<Guid>> CrearAsientoNotaCreditoVentaAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct);

    /// <summary>Incremento de ventas por nota de débito (débito clientes, crédito ventas/IVA).</summary>
    Task<Result<Guid>> CrearAsientoNotaDebitoVentaAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct);

    /// <summary>Retención emitida a proveedor: reduce deuda con proveedor y registra pasivo por retener.</summary>
    Task<Result<Guid>> CrearAsientoRetencionEmitidaAsync(
        Guid     retencionId,
        string   referencia,
        DateTime fecha,
        decimal  totalRetenido,
        string   descripcion,
        CancellationToken ct);

    /// <summary>Retención recibida de cliente: reduce impuesto por pagar y cuentas por cobrar.</summary>
    Task<Result<Guid>> CrearAsientoRetencionRecibidaAsync(
        Guid     retencionId,
        string   referencia,
        DateTime fecha,
        decimal  totalRetenido,
        string   descripcion,
        CancellationToken ct);
}
