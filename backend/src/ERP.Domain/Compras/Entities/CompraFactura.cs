using ERP.Domain.Common;
using ERP.Domain.Compras.Enums;

namespace ERP.Domain.Compras.Entities;

/// <summary>
/// Factura de compra del tenant. Ciclo: Borrador → Confirmada → Recibida | Anulada.
/// </summary>
public sealed class CompraFactura : AuditableEntity, ITenantEntity
{
    public const int NumeroFacturaMaxLen = 50;
    public const int CondicionPagoMaxLen = 30;
    public const int ObservacionesMaxLen = 1000;

    private readonly List<CompraDetalle> _detalles = new();

    public Guid         ProveedorId     { get; private set; }
    public string       NumeroFactura   { get; private set; } = null!;
    public DateTime     FechaFactura    { get; private set; }
    public DateTime?    FechaVencimiento { get; private set; }
    public EstadoCompra Estado          { get; private set; } = EstadoCompra.Borrador;
    public string       CondicionPago   { get; private set; } = null!;
    public decimal      Subtotal        { get; private set; }
    public decimal      IvaTotal        { get; private set; }
    public decimal      Total           { get; private set; }
    public string?      Observaciones   { get; private set; }

    public IReadOnlyList<CompraDetalle> Detalles => _detalles.AsReadOnly();

    private CompraFactura() { }

    public static CompraFactura Create(
        Guid     tenantId,
        Guid     proveedorId,
        string   numeroFactura,
        DateTime fechaFactura,
        DateTime? fechaVencimiento,
        string   condicionPago,
        string?  observaciones,
        Guid     createdBy)
    {
        var c = new CompraFactura
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            ProveedorId      = proveedorId,
            NumeroFactura    = numeroFactura.Trim(),
            FechaFactura     = fechaFactura,
            FechaVencimiento = fechaVencimiento,
            CondicionPago    = condicionPago,
            Observaciones    = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
            Estado           = EstadoCompra.Borrador,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void AgregarDetalle(
        Guid    productoId,
        decimal cantidad,
        decimal precioUnitario,
        decimal descuentoPorcentaje,
        decimal ivaPorcentaje,
        Guid    createdBy)
    {
        if (Estado != EstadoCompra.Borrador)
            throw new InvalidOperationException("Solo se pueden agregar detalles a una compra en borrador.");

        _detalles.Add(CompraDetalle.Create(
            TenantId, Id, productoId,
            cantidad, precioUnitario, descuentoPorcentaje, ivaPorcentaje,
            createdBy));

        RecalcularTotales();
    }

    public void Confirmar(Guid userId)
    {
        if (Estado != EstadoCompra.Borrador)
            throw new InvalidOperationException("Solo se puede confirmar una compra en borrador.");
        if (_detalles.Count == 0)
            throw new InvalidOperationException("La compra debe tener al menos un detalle.");
        Estado = EstadoCompra.Confirmada;
        SetUpdated(userId);
    }

    public void Recibir(Guid userId)
    {
        if (Estado != EstadoCompra.Confirmada)
            throw new InvalidOperationException("Solo se puede recibir una compra confirmada.");
        Estado = EstadoCompra.Recibida;
        SetUpdated(userId);
    }

    public void Anular(Guid userId)
    {
        if (Estado == EstadoCompra.Anulada)
            throw new InvalidOperationException("La compra ya está anulada.");
        Estado = EstadoCompra.Anulada;
        SetUpdated(userId);
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        IvaTotal = _detalles.Sum(d => d.IvaValor);
        Total    = Subtotal + IvaTotal;
    }
}
