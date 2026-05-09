using ERP.Domain.Common;
using ERP.Domain.Compras.Enums;

namespace ERP.Domain.Compras.Entities;

/// <summary>
/// Factura de compra del tenant.
/// Ciclo: Borrador → Validado → Aprobado
///                           ↘ Rechazado
/// </summary>
public sealed class CompraFactura : AuditableEntity, ITenantEntity
{
    public const int NumeroFacturaMaxLen  = 50;
    public const int ClaveAccesoLen       = 49;
    public const int CondicionPagoMaxLen  = 30;
    public const int ObservacionesMaxLen  = 1000;
    public const int MotivoRechazoMaxLen  = 500;
    public const int XmlPathMaxLen        = 500;
    public const decimal ToleranciaTotal  = 0.01m;

    private readonly List<CompraDetalle> _detalles = new();

    public Guid         ProveedorId       { get; private set; }
    public string       NumeroFactura     { get; private set; } = null!;
    public string?      ClaveAcceso       { get; private set; }
    public string?      XmlPath           { get; private set; }
    public DateTime     FechaFactura      { get; private set; }
    public DateTime?    FechaVencimiento  { get; private set; }
    public EstadoCompra Estado            { get; private set; } = EstadoCompra.Borrador;
    public string       CondicionPago     { get; private set; } = null!;
    public decimal      Subtotal          { get; private set; }
    public decimal      IvaTotal          { get; private set; }
    public decimal      Total             { get; private set; }
    public string?      Observaciones     { get; private set; }

    // ── Auditoría de estado ───────────────────────────────────────────────
    public Guid?      ValidadoPor      { get; private set; }
    public DateTime?  ValidadoEn       { get; private set; }
    public Guid?      AprobadoPor      { get; private set; }
    public DateTime?  AprobadoEn       { get; private set; }
    public Guid?      RechazadoPor     { get; private set; }
    public DateTime?  RechazadoEn      { get; private set; }
    public string?    MotivoRechazo    { get; private set; }
    public Guid?      AsientoContableId { get; private set; }

    public IReadOnlyList<CompraDetalle> Detalles => _detalles.AsReadOnly();

    private CompraFactura() { }

    public static CompraFactura Create(
        Guid      tenantId,
        Guid      proveedorId,
        string    numeroFactura,
        string?   claveAcceso,
        string?   xmlPath,
        DateTime  fechaFactura,
        DateTime? fechaVencimiento,
        string    condicionPago,
        string?   observaciones,
        Guid      createdBy)
    {
        var c = new CompraFactura
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            ProveedorId      = proveedorId,
            NumeroFactura    = numeroFactura.Trim(),
            ClaveAcceso      = string.IsNullOrWhiteSpace(claveAcceso) ? null : claveAcceso.Trim(),
            XmlPath          = string.IsNullOrWhiteSpace(xmlPath) ? null : xmlPath.Trim(),
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
        string  descripcion,
        string? codigoPrincipalProveedor,
        Guid?   productoId,
        decimal cantidad,
        decimal precioUnitario,
        decimal descuentoPorcentaje,
        decimal ivaPorcentaje,
        Guid    createdBy)
    {
        if (Estado != EstadoCompra.Borrador)
            throw new InvalidOperationException("Solo se pueden agregar detalles a una compra en borrador.");

        _detalles.Add(CompraDetalle.Create(
            TenantId, Id,
            descripcion, codigoPrincipalProveedor, productoId,
            cantidad, precioUnitario, descuentoPorcentaje, ivaPorcentaje,
            createdBy));

        RecalcularTotales();
    }

    public void Validar(Guid userId)
    {
        if (Estado != EstadoCompra.Borrador)
            throw new InvalidOperationException("Solo se puede validar una compra en borrador.");
        if (_detalles.Count == 0)
            throw new InvalidOperationException("La compra debe tener al menos un detalle.");

        Estado     = EstadoCompra.Validado;
        ValidadoPor = userId;
        ValidadoEn  = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Aprobar(Guid userId, Guid? asientoContableId)
    {
        if (Estado != EstadoCompra.Validado)
            throw new InvalidOperationException("Solo se puede aprobar una compra validada.");

        Estado            = EstadoCompra.Aprobado;
        AprobadoPor       = userId;
        AprobadoEn        = DateTime.UtcNow;
        AsientoContableId = asientoContableId;
        SetUpdated(userId);
    }

    public void Rechazar(Guid userId, string motivo)
    {
        if (Estado == EstadoCompra.Aprobado)
            throw new InvalidOperationException("No se puede rechazar una compra ya aprobada.");
        if (Estado == EstadoCompra.Rechazado)
            throw new InvalidOperationException("La compra ya está rechazada.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivo));

        Estado        = EstadoCompra.Rechazado;
        RechazadoPor  = userId;
        RechazadoEn   = DateTime.UtcNow;
        MotivoRechazo = motivo.Trim();
        SetUpdated(userId);
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        IvaTotal = _detalles.Sum(d => d.IvaValor);
        Total    = Subtotal + IvaTotal;
    }
}
