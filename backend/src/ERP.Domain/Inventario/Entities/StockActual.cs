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
            UltimaActualizacion = DateTime.UtcNow,
        };
        s.SetCreated(createdBy);
        return s;
    }

    /// <summary>Aplica un delta positivo (entrada) o negativo (salida).</summary>
    public void AplicarMovimiento(decimal delta, Guid updatedBy)
    {
        var nueva = Cantidad + delta;
        if (nueva < 0)
            throw new InvalidOperationException(
                $"El movimiento dejaría el stock en {nueva}. Stock insuficiente.");
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
