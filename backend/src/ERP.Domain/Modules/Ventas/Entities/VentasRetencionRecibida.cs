using ERP.Domain.Common;

namespace ERP.Domain.Modules.Ventas.Entities;

/// <summary>Retención en la fuente emitida por un cliente sobre nuestra factura de venta (solo registro interno).</summary>
public sealed class VentasRetencionRecibida : AuditableEntity, ITenantEntity
{
    public const int TipoComprobanteMaxLen = 30;
    public const int ClaveAccesoMaxLen = 49;
    public const int EstadoMaxLen = 20;
    public const int XmlPathMaxLen = 500;

    private readonly List<VentasDetalleRetencionRecibida> _detalles = new();

    public Guid ClienteId { get; private set; }
    public string TipoComprobante { get; private set; } = "RETENCION";
    public string ClaveAcceso { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public decimal ValorRetenido { get; private set; }
    public string Estado { get; private set; } = "Activo";
    public Guid? VentasFacturaId { get; private set; }
    public string? XmlRegistroPath { get; private set; }
    public Guid? AsientoContableId { get; private set; }

    public Customer Cliente { get; private set; } = null!;
    public IReadOnlyList<VentasDetalleRetencionRecibida> Detalles => _detalles.AsReadOnly();

    private VentasRetencionRecibida() { }

    public static VentasRetencionRecibida Create(
        Guid tenantId,
        Guid clienteId,
        string claveAcceso,
        DateTime fechaEmision,
        decimal valorRetenido,
        Guid? ventasFacturaId,
        string? xmlRegistroPath,
        Guid createdBy)
    {
        var r = new VentasRetencionRecibida
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ClienteId      = clienteId,
            ClaveAcceso    = claveAcceso.Trim(),
            FechaEmision   = fechaEmision,
            ValorRetenido  = valorRetenido,
            VentasFacturaId = ventasFacturaId,
            XmlRegistroPath = string.IsNullOrWhiteSpace(xmlRegistroPath) ? null : xmlRegistroPath.Trim(),
        };
        r.SetCreated(createdBy);
        return r;
    }

    public void AgregarDetalle(VentasDetalleRetencionRecibida detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
    }

    public void VincularAsiento(Guid asientoId, Guid userId)
    {
        AsientoContableId = asientoId;
        SetUpdated(userId);
    }
}
