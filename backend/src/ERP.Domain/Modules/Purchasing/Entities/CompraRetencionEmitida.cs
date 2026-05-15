using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

/// <summary>Comprobante de retención en la fuente emitido por la empresa al proveedor (compras).</summary>
public sealed class CompraRetencionEmitida : AuditableEntity, ITenantEntity
{
    public const int TipoComprobanteMaxLen = 30;
    public const int ClaveAccesoMaxLen = 49;
    public const int EstablecimientoMaxLen = 3;
    public const int PuntoEmisionMaxLen = 3;
    public const int SecuencialMaxLen = 9;
    public const int EstadoMaxLen = 20;
    public const int XmlPathMaxLen = 500;
    public const int MensajeErrorMaxLen = 1000;

    private readonly List<CompraDetalleRetencionEmitida> _detalles = new();

    public Guid ProveedorId { get; private set; }
    /// <summary>Factura de compra origen del cálculo (opcional).</summary>
    public Guid? CompraFacturaId { get; private set; }
    public string TipoComprobante { get; private set; } = "RETENCION";
    public string ClaveAcceso { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public string Establecimiento { get; private set; } = null!;
    public string PuntoEmision { get; private set; } = null!;
    public string Secuencial { get; private set; } = null!;
    public string Estado { get; private set; } = "Borrador";
    public string? XmlGeneradoPath { get; private set; }
    public string? XmlAutorizacionPath { get; private set; }
    public string? NumeroAutorizacion { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public string? MensajeError { get; private set; }
    public Guid? AsientoContableId { get; private set; }
    public decimal TotalRetenido { get; private set; }

    /// <summary>Cargado por EF Include para emisión / XML.</summary>
    public Proveedor Proveedor { get; private set; } = null!;

    public IReadOnlyList<CompraDetalleRetencionEmitida> Detalles => _detalles.AsReadOnly();

    private CompraRetencionEmitida() { }

    public static CompraRetencionEmitida Create(
        Guid tenantId,
        Guid proveedorId,
        Guid? compraFacturaId,
        string claveAcceso,
        DateTime fechaEmision,
        string establecimiento,
        string puntoEmision,
        string secuencial,
        Guid createdBy)
    {
        var e = new CompraRetencionEmitida
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            ProveedorId      = proveedorId,
            CompraFacturaId  = compraFacturaId,
            ClaveAcceso      = claveAcceso.Trim(),
            FechaEmision     = fechaEmision,
            Establecimiento  = establecimiento.Trim(),
            PuntoEmision     = puntoEmision.Trim(),
            Secuencial       = secuencial.Trim(),
            Estado           = "Borrador",
            TotalRetenido    = 0,
        };
        e.SetCreated(createdBy);
        return e;
    }

    public void AgregarDetalle(CompraDetalleRetencionEmitida detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
        TotalRetenido = _detalles.Sum(d => d.ValorRetenido);
    }

    public void Validar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException($"Solo se puede validar en Borrador (estado: {Estado}).");
        Estado = "Validado";
        SetUpdated(userId);
    }

    public void Autorizar(
        Guid userId,
        string numeroAutorizacion,
        DateTime fechaAutorizacion,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        Guid asientoContableId)
    {
        if (Estado != "Validado")
            throw new InvalidOperationException($"Solo se puede autorizar en Validado (estado: {Estado}).");
        Estado = "Autorizado";
        NumeroAutorizacion = numeroAutorizacion;
        FechaAutorizacion = fechaAutorizacion;
        XmlGeneradoPath = string.IsNullOrWhiteSpace(xmlGeneradoPath) ? null : xmlGeneradoPath;
        XmlAutorizacionPath = string.IsNullOrWhiteSpace(xmlAutorizacionPath) ? null : xmlAutorizacionPath;
        AsientoContableId = asientoContableId;
        SetUpdated(userId);
    }

    public void Rechazar(Guid userId, string mensajeError)
    {
        Estado = "Rechazado";
        MensajeError = mensajeError?.Trim();
        SetUpdated(userId);
    }

    public void MarcarErrorEnvio(Guid userId, string mensajeError)
    {
        Estado = "ErrorEnvio";
        MensajeError = mensajeError?.Trim();
        SetUpdated(userId);
    }
}
