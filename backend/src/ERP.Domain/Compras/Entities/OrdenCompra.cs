using ERP.Domain.Common;

namespace ERP.Domain.Compras.Entities;

/// <summary>
/// Orden de Compra (OC) — documento interno que autoriza la compra de productos
/// a un proveedor antes de recibir la factura electrónica.
///
/// Flujo: Borrador → Enviada → Aprobada ⇄ RecibidaParcial → Cerrada
///        Cualquier estado (excepto Cerrada y Cancelada) → Cancelada
/// El stock se actualiza cuando se aprueba la CompraFactura vinculada, no aquí.
/// </summary>
public sealed class OrdenCompra : AuditableEntity, ITenantEntity
{
    public const int NumeroMaxLen    = 20;
    public const int EstadoMaxLen    = 20;
    public const int MonedaMaxLen    = 10;
    public const int ObsMaxLen       = 1000;
    public const int DireccionMaxLen = 500;

    private readonly List<OrdenCompraDetalle> _detalles = new();

    public int       Secuencial       { get; private set; }
    public string    NumeroOrden      { get; private set; } = null!;
    public Guid      ProveedorId      { get; private set; }
    public DateTime  FechaEmision     { get; private set; }
    public DateTime  FechaRequerida   { get; private set; }
    public string    Estado           { get; private set; } = "Borrador";
    public string    Moneda           { get; private set; } = "USD";
    public decimal   Subtotal         { get; private set; }
    public decimal   Impuesto         { get; private set; }
    public decimal   Total            { get; private set; }
    public string?   Observaciones    { get; private set; }
    public string?   DireccionEntrega { get; private set; }
    public Guid?     BodegaDestinoId  { get; private set; }
    public DateTime? FechaEnvio       { get; private set; }
    public DateTime? FechaAprobacion  { get; private set; }
    public Guid?     AprobadoPor      { get; private set; }
    public DateTime? FechaCierre      { get; private set; }

    public IReadOnlyList<OrdenCompraDetalle> Detalles => _detalles.AsReadOnly();

    private OrdenCompra() { }

    public static OrdenCompra Create(
        Guid      tenantId,
        int       secuencial,
        Guid      proveedorId,
        DateTime  fechaRequerida,
        Guid?     bodegaDestinoId,
        string?   direccionEntrega,
        string?   observaciones,
        Guid      createdBy)
    {
        var o = new OrdenCompra
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            Secuencial       = secuencial,
            NumeroOrden      = $"OC-{secuencial:D4}",
            ProveedorId      = proveedorId,
            FechaEmision     = DateTime.UtcNow,
            FechaRequerida   = fechaRequerida,
            Estado           = "Borrador",
            Moneda           = "USD",
            BodegaDestinoId  = bodegaDestinoId,
            DireccionEntrega = string.IsNullOrWhiteSpace(direccionEntrega) ? null : direccionEntrega.Trim(),
            Observaciones    = string.IsNullOrWhiteSpace(observaciones)    ? null : observaciones.Trim(),
        };
        o.SetCreated(createdBy);
        return o;
    }

    public void AgregarDetalle(OrdenCompraDetalle detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
        RecalcularTotales();
    }

    public void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        Impuesto = _detalles.Sum(d => d.Impuesto);
        Total    = _detalles.Sum(d => d.Total);
    }

    // ── Transiciones de estado ─────────────────────────────────────────────

    public void Enviar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException($"Solo se puede enviar una OC en Borrador (estado: {Estado}).");
        Estado      = "Enviada";
        FechaEnvio  = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Aprobar(Guid userId)
    {
        if (Estado is not ("Borrador" or "Enviada"))
            throw new InvalidOperationException($"Solo se puede aprobar una OC en Borrador o Enviada (estado: {Estado}).");
        Estado           = "Aprobada";
        FechaAprobacion  = DateTime.UtcNow;
        AprobadoPor      = userId;
        SetUpdated(userId);
    }

    public void Cancelar(Guid userId)
    {
        if (Estado is "Cerrada" or "Cancelada")
            throw new InvalidOperationException($"No se puede cancelar una OC en estado {Estado}.");
        Estado = "Cancelada";
        SetUpdated(userId);
    }

    public void MarcarRecibidaParcial(Guid userId)
    {
        if (Estado != "Aprobada")
            return; // ya está en RecibidaParcial o superior, no cambiar
        Estado = "RecibidaParcial";
        SetUpdated(userId);
    }

    public void Cerrar(Guid userId)
    {
        if (Estado is not ("Aprobada" or "RecibidaParcial"))
            throw new InvalidOperationException($"Solo se puede cerrar una OC en Aprobada o RecibidaParcial (estado: {Estado}).");
        Estado      = "Cerrada";
        FechaCierre = DateTime.UtcNow;
        SetUpdated(userId);
    }
}
