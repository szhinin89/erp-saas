using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Expenses.Entities;

namespace ERP.Domain.Modules.Purchasing.Entities;

/// <summary>
/// Nota de crédito o débito recibida del proveedor (SRI), aplicable a una factura de compra o de gasto.
/// </summary>
public sealed class CompraNotaProveedor : AuditableEntity, ITenantEntity
{
    public const int TipoNotaMaxLen = 20;
    public const int MotivoMaxLen = 300;
    public const int ClaveAccesoMaxLen = 49;
    public const int EstadoMaxLen = 20;
    public const int XmlPathMaxLen = 500;
    public const int EstablecimientoMaxLen = 3;
    public const int PuntoEmisionMaxLen = 3;
    public const int SecuencialMaxLen = 9;
    public const int NumeroAutorizacionMaxLen = 64;

    private readonly List<CompraNotaProveedorDetalle> _detalles = new();

    public Guid ProveedorId { get; private set; }
    public Guid? CompraFacturaId { get; private set; }
    public Guid? GastoFacturaId { get; private set; }
    public string TipoNota { get; private set; } = null!;
    public string Motivo { get; private set; } = null!;
    public string ClaveAcceso { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public string Establecimiento { get; private set; } = null!;
    public string PuntoEmision { get; private set; } = null!;
    public string Secuencial { get; private set; } = null!;
    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal Total { get; private set; }
    public string Estado { get; private set; } = "Borrador";
    public string? XmlPath { get; private set; }
    public string? NumeroAutorizacion { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public Guid? AsientoContableId { get; private set; }

    public Proveedor Proveedor { get; private set; } = null!;
    public CompraFactura? CompraFactura { get; private set; }
    public GastoFactura? GastoFactura { get; private set; }
    public IReadOnlyList<CompraNotaProveedorDetalle> Detalles => _detalles.AsReadOnly();

    private CompraNotaProveedor() { }

    public static CompraNotaProveedor Create(
        Guid tenantId,
        Guid proveedorId,
        Guid? compraFacturaId,
        Guid? gastoFacturaId,
        string tipoNota,
        string motivo,
        string claveAcceso,
        DateTime fechaEmision,
        string establecimiento,
        string puntoEmision,
        string secuencial,
        Guid createdBy)
    {
        if (compraFacturaId.HasValue && gastoFacturaId.HasValue)
            throw new ArgumentException("La nota no puede vincularse a compra y gasto a la vez.");

        var tn = (tipoNota ?? string.Empty).Trim().ToUpperInvariant();
        if (tn is not ("CREDITO" or "DEBITO"))
            throw new ArgumentException("TipoNota debe ser CREDITO o DEBITO.", nameof(tipoNota));

        var n = new CompraNotaProveedor
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            ProveedorId      = proveedorId,
            CompraFacturaId  = compraFacturaId,
            GastoFacturaId   = gastoFacturaId,
            TipoNota         = tn,
            Motivo           = (motivo ?? string.Empty).Trim(),
            ClaveAcceso      = (claveAcceso ?? string.Empty).Trim(),
            FechaEmision     = fechaEmision,
            Establecimiento  = establecimiento.Trim(),
            PuntoEmision     = puntoEmision.Trim(),
            Secuencial       = secuencial.Trim(),
            Estado           = "Borrador",
        };
        n.SetCreated(createdBy);
        return n;
    }

    public void AgregarDetalle(CompraNotaProveedorDetalle detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        detalle.AsignarNotaId(Id);
        _detalles.Add(detalle);
        RecalcularTotales();
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        Impuesto = _detalles.Sum(d => d.Impuesto);
        Total    = _detalles.Sum(d => d.Total);
    }

    public void SetXmlPath(string? path, Guid userId)
    {
        XmlPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        SetUpdated(userId);
    }

    public void VincularFacturaCompra(Guid compraFacturaId, Guid userId)
    {
        if (GastoFacturaId.HasValue)
            throw new InvalidOperationException("La nota ya está vinculada a un gasto.");
        CompraFacturaId = compraFacturaId;
        SetUpdated(userId);
    }

    public void VincularFacturaGasto(Guid gastoFacturaId, Guid userId)
    {
        if (CompraFacturaId.HasValue)
            throw new InvalidOperationException("La nota ya está vinculada a una compra.");
        GastoFacturaId = gastoFacturaId;
        SetUpdated(userId);
    }

    public void Aprobar(
        Guid userId,
        Guid? asientoContableId,
        string? numeroAutorizacion,
        DateTime? fechaAutorizacion,
        IReadOnlyList<CompraNotaProveedorStockLine>? stockLines = null)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException($"Solo se puede aprobar una nota en Borrador (estado: {Estado}).");
        if (!CompraFacturaId.HasValue && !GastoFacturaId.HasValue)
            throw new InvalidOperationException("Debe vincular la nota a una factura de compra o de gasto antes de aprobar.");

        Estado = "Aprobado";
        AsientoContableId = asientoContableId;
        NumeroAutorizacion = string.IsNullOrWhiteSpace(numeroAutorizacion) ? null : numeroAutorizacion.Trim();
        FechaAutorizacion = fechaAutorizacion;
        SetUpdated(userId);

        var lines = stockLines ?? Array.Empty<CompraNotaProveedorStockLine>();
        if (CompraFacturaId.HasValue && lines.Count > 0)
        {
            var numero = $"{Establecimiento}-{PuntoEmision}-{Secuencial}";
            RaiseDomainEvent(new CompraNotaProveedorAprobadaEvent(
                Id, TenantId, userId, CompraFacturaId.Value, TipoNota, numero, lines));
        }
    }
}
