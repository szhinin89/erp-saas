using ERP.Domain.Common;

namespace ERP.Domain.Inventario.Entities;

/// <summary>
/// Saldo valorizado de un producto en una bodega al cierre de un día calendario (UTC).
/// Generado por el worker nocturno y usado como punto de partida del período en el Kardex,
/// eliminando la necesidad de recorrer todo el historial de movimientos anteriores.
/// </summary>
public sealed class KardexSnapshot : ITenantEntity
{
    public Guid     Id            { get; private set; }
    public Guid     TenantId      { get; private set; }
    public Guid     ProductoId    { get; private set; }
    public Guid     BodegaId      { get; private set; }
    /// <summary>Fecha del día (UTC, sin componente hora) cuyo saldo representa este snapshot.</summary>
    public DateTime FechaSnapshot { get; private set; }
    public decimal  CantidadSaldo { get; private set; }
    public decimal  ValorSaldo    { get; private set; }
    public decimal  CostoPromedio { get; private set; }
    /// <summary>Instante UTC en que se calculó este snapshot.</summary>
    public DateTime ComputadoEn   { get; private set; }

    private KardexSnapshot() { }

    public static KardexSnapshot Create(
        Guid    tenantId,
        Guid    productoId,
        Guid    bodegaId,
        DateTime fechaSnapshot,
        decimal cantidadSaldo,
        decimal valorSaldo,
        decimal costoPromedio)
        => new()
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            ProductoId    = productoId,
            BodegaId      = bodegaId,
            FechaSnapshot = DateTime.SpecifyKind(fechaSnapshot.Date, DateTimeKind.Utc),
            CantidadSaldo = cantidadSaldo,
            ValorSaldo    = Math.Max(0m, valorSaldo),
            CostoPromedio = costoPromedio,
            ComputadoEn   = DateTime.UtcNow,
        };
}
