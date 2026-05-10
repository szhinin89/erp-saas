using ERP.Domain.Common;

namespace ERP.Domain.Inventario.Entities;

/// <summary>
/// Saldo de stock de un producto en una bodega específica.
/// Un registro por combinación (TenantId, ProductoId, BodegaId).
/// </summary>
public sealed class StockActual : AuditableEntity, ITenantEntity
{
    public Guid     ProductoId          { get; private set; }
    public Guid     BodegaId            { get; private set; }
    public decimal  Cantidad            { get; private set; }
    public decimal  CantidadReservada   { get; private set; }
    public decimal  CantidadDisponible  => Cantidad - CantidadReservada;
    /// <summary>
    /// Valor total del stock a costo promedio ponderado.
    /// Se mantiene actualizado en cada movimiento de entrada/salida.
    /// Permite calcular el costo promedio actual sin recorrer el historial.
    /// </summary>
    public decimal  ValorTotalStock     { get; private set; }
    /// <summary>Costo unitario promedio ponderado actual. 0 si no hay stock.</summary>
    public decimal  CostoPromedioActual => Cantidad > 0m ? ValorTotalStock / Cantidad : 0m;
    public DateTime UltimaActualizacion { get; private set; }

    private StockActual() { }

    public static StockActual Create(
        Guid tenantId,
        Guid productoId,
        Guid bodegaId,
        Guid createdBy)
    {
        var s = new StockActual
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            ProductoId          = productoId,
            BodegaId            = bodegaId,
            Cantidad            = 0,
            CantidadReservada   = 0,
            ValorTotalStock     = 0m,
            UltimaActualizacion = DateTime.UtcNow,
        };
        s.SetCreated(createdBy);
        return s;
    }

    /// <summary>
    /// Aplica un delta positivo (entrada) o negativo (salida).
    /// Para entradas: pasar el costo unitario de compra.
    /// Para salidas: pasar el costo promedio actual (CostoPromedioActual) como costoUnitario.
    /// </summary>
    public void AplicarMovimiento(decimal delta, Guid updatedBy, decimal costoUnitario = 0m)
    {
        var nueva = Cantidad + delta;
        if (nueva < 0)
            throw new InvalidOperationException(
                $"El movimiento dejaría el stock en {nueva}. Stock insuficiente.");

        // Actualizar valoración:
        // Entradas (delta > 0): suma el valor que ingresa
        // Salidas  (delta < 0): resta el valor que sale (costoUnitario = promedio vigente)
        ValorTotalStock = Math.Max(0m, ValorTotalStock + delta * costoUnitario);

        Cantidad            = nueva;
        UltimaActualizacion = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void Reservar(decimal cantidad, Guid updatedBy)
    {
        if (cantidad > CantidadDisponible)
            throw new InvalidOperationException(
                $"No hay suficiente stock disponible. Disponible: {CantidadDisponible}, solicitado: {cantidad}.");
        CantidadReservada  += cantidad;
        UltimaActualizacion = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void LiberarReserva(decimal cantidad, Guid updatedBy)
    {
        CantidadReservada   = Math.Max(0, CantidadReservada - cantidad);
        UltimaActualizacion = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }
}
