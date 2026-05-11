using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Common;
using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Domain.Modules.Ventas.Entities;

public sealed class VentasFactura : AuditableEntity, ITenantEntity
{
    public const int TipoDocumentoMaxLen = 10;
    public const int EstablecimientoMaxLen = 3;
    public const int PuntoEmisionMaxLen = 3;
    public const int SecuencialMaxLen = 9;
    public const int ClaveAccesoMaxLen = 48;
    public const int EstadoMaxLen = 20;
    public const int XmlPathMaxLen = 500;
    public const int MensajeErrorMaxLen = 1000;

    private readonly List<VentasDetalle> _detalles = new();

    public Guid SucursalId { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid BodegaId { get; private set; }
    public string TipoDocumento { get; private set; } = "01";
    public string Establecimiento { get; private set; } = null!;
    public string PuntoEmision { get; private set; } = null!;
    public string Secuencial { get; private set; } = null!;
    public string ClaveAcceso { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal Total { get; private set; }
    public string Estado { get; private set; } = "Borrador";
    public string? XmlGeneradoPath { get; private set; }
    public string? XmlAutorizacionPath { get; private set; }
    public string? NumeroAutorizacion { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public string? MensajeError { get; private set; }
    public Guid? AsientoContableId { get; private set; }

    public Customer Cliente { get; private set; } = null!;
    public Bodega Bodega { get; private set; } = null!;
    public IReadOnlyList<VentasDetalle> Detalles => _detalles.AsReadOnly();

    private VentasFactura() { }

    public static VentasFactura Create(
        Guid tenantId,
        Guid sucursalId,
        Guid clienteId,
        Guid bodegaId,
        string tipoDocumento,
        string establecimiento,
        string puntoEmision,
        string secuencial,
        string claveAcceso,
        DateTime fechaEmision,
        decimal subtotal,
        decimal impuesto,
        decimal total,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        string? numeroAutorizacion,
        DateTime? fechaAutorizacion,
        string? mensajeError,
        Guid createdBy)
    {
        var factura = new VentasFactura
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SucursalId = sucursalId,
            ClienteId = clienteId,
            BodegaId = bodegaId,
            TipoDocumento = string.IsNullOrWhiteSpace(tipoDocumento) ? "01" : tipoDocumento.Trim(),
            Establecimiento = establecimiento.Trim(),
            PuntoEmision = puntoEmision.Trim(),
            Secuencial = secuencial.Trim(),
            ClaveAcceso = claveAcceso.Trim(),
            FechaEmision = fechaEmision,
            Subtotal = subtotal,
            Impuesto = impuesto,
            Total = total,
            Estado = "Borrador",
            XmlGeneradoPath = string.IsNullOrWhiteSpace(xmlGeneradoPath) ? null : xmlGeneradoPath.Trim(),
            XmlAutorizacionPath = string.IsNullOrWhiteSpace(xmlAutorizacionPath) ? null : xmlAutorizacionPath.Trim(),
            NumeroAutorizacion = string.IsNullOrWhiteSpace(numeroAutorizacion) ? null : numeroAutorizacion.Trim(),
            FechaAutorizacion = fechaAutorizacion,
            MensajeError = string.IsNullOrWhiteSpace(mensajeError) ? null : mensajeError.Trim(),
        };
        factura.SetCreated(createdBy);
        return factura;
    }

    public void Validar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException($"Solo se puede validar una factura en Borrador (estado actual: {Estado}).");
        Estado = "Validado";
        SetUpdated(userId);
    }

    public void Autorizar(
        Guid userId,
        string numeroAutorizacion,
        DateTime fechaAutorizacion,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        Guid asientoId)
    {
        if (Estado != "Validado")
            throw new InvalidOperationException($"Solo se puede autorizar una factura Validada (estado actual: {Estado}).");
        Estado = "Autorizado";
        NumeroAutorizacion = numeroAutorizacion;
        FechaAutorizacion = fechaAutorizacion;
        XmlGeneradoPath = string.IsNullOrWhiteSpace(xmlGeneradoPath) ? null : xmlGeneradoPath;
        XmlAutorizacionPath = string.IsNullOrWhiteSpace(xmlAutorizacionPath) ? null : xmlAutorizacionPath;
        AsientoContableId = asientoId;
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

    public void PrepararReintento(Guid userId)
    {
        if (Estado != "ErrorEnvio" && Estado != "Rechazado")
            throw new InvalidOperationException(
                $"Solo se puede reintentar una factura en ErrorEnvio o Rechazado (estado actual: {Estado}).");
        Estado = "Validado";
        MensajeError = null;
        SetUpdated(userId);
    }

    public void Anular(Guid userId)
    {
        if (Estado == "Autorizado" || Estado == "Anulado")
            throw new InvalidOperationException(
                $"No se puede anular una factura en estado {Estado}.");
        Estado = "Anulado";
        SetUpdated(userId);
    }

    public void AgregarDetalle(VentasDetalle detalle)
    {
        if (detalle == null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
        RecalcularTotales();
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        Impuesto = _detalles.Sum(d => d.Impuesto);
        Total = _detalles.Sum(d => d.Total);
    }
}
