using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Resuelve cuentas del plan según la configuración por tenant.
/// Cuando no hay fila de <c>AccountingSetup</c>, los métodos de compra/venta devuelven éxito con valor null
/// (el llamador aplica la heurística por tipo de cuenta).
/// </summary>
public interface ICuentaContableService
{
    /// <param name="subtotalInventario">Base imponible de inventario (sin IVA).</param>
    /// <param name="iva">Monto de IVA de la compra.</param>
    Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaCompraAsync(
        Guid tenantId,
        decimal subtotalInventario,
        decimal  vatTotal,
        CancellationToken ct);

    /// <param name="subtotalVentas">Base imponible de ventas (sin IVA).</param>
    Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaVentaAsync(
        Guid tenantId,
        decimal subtotalVentas,
        decimal  vatTotal,
        CancellationToken ct);

    /// <summary>Cuenta de gasto mapeada por categoría; null si no hay mapeo (usar heurística legacy).</summary>
    Task<Result<Guid?>> ObtenerCuentaParaGastoAsync(Guid tenantId, string   category, CancellationToken ct);

    /// <summary>Caja o banco preferido para el crédito del asiento de gasto; null si no está configurado.</summary>
    Task<Result<Guid?>> ObtenerCuentaCajaParaGastoAsync(Guid tenantId, CancellationToken ct);
}
