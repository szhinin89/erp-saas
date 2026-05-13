using ERP.Domain.Common;
using ERP.Domain.Modules.Ventas.Events;

namespace ERP.Domain.Modules.Ventas.Entities;

/// <summary>Nota de crédito o débito electrónica asociada a una factura de venta (SRI Ecuador).</summary>
public sealed class VentasNotaCreditoDebito : AuditableEntity, ITenantEntity
{
    public const int TipoNotaMaxLen = 20;
    public const int MotivoMaxLen = 300;
    public const int TipoDocumentoMaxLen = 10;
    public const int EstablecimientoMaxLen = 3;
    public const int PuntoEmisionMaxLen = 3;
    public const int SecuencialMaxLen = 9;
    public const int ClaveAccesoMaxLen = 49;
    public const int EstadoMaxLen = 20;
    public const int XmlPathMaxLen = 500;
    public const int MensajeErrorMaxLen = 1000;

    private readonly List<VentasNotaDetalle> _detalles = new();

    public Guid VentasFacturaOriginalId { get; private set; }
    /// <summary>CREDITO o DEBITO.</summary>
    public string TipoNota { get; private set; } = null!;
    /// <summary>Código o texto de motivo según catálogo SRI.</summary>
    public string Motivo { get; private set; } = null!;
    /// <summary>04 nota crédito, 05 nota débito.</summary>
    public string TipoDocumento { get; private set; } = null!;
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

    public VentasFactura FacturaOriginal { get; private set; } = null!;
    public IReadOnlyList<VentasNotaDetalle> Detalles => _detalles.AsReadOnly();

    private VentasNotaCreditoDebito() { }

    public static VentasNotaCreditoDebito Create(
        Guid tenantId,
        Guid facturaOriginalId,
        string tipoNota,
        string motivo,
        string tipoDocumento,
        string establecimiento,
        string puntoEmision,
        string secuencial,
        string claveAcceso,
        DateTime fechaEmision,
        Guid createdBy)
    {
        var tn = (tipoNota ?? string.Empty).Trim().ToUpperInvariant();
        if (tn is not ("CREDITO" or "DEBITO"))
            throw new ArgumentException("TipoNota debe ser CREDITO o DEBITO.", nameof(tipoNota));

        var td = (tipoDocumento ?? string.Empty).Trim();
        if (td is not ("04" or "05"))
            td = tn == "CREDITO" ? "04" : "05";

        var nota = new VentasNotaCreditoDebito
        {
            Id                      = Guid.NewGuid(),
            TenantId                = tenantId,
            VentasFacturaOriginalId = facturaOriginalId,
            TipoNota                = tn,
            Motivo                  = (motivo ?? string.Empty).Trim(),
            TipoDocumento           = td,
            Establecimiento         = establecimiento.Trim(),
            PuntoEmision            = puntoEmision.Trim(),
            Secuencial              = secuencial.Trim(),
            ClaveAcceso             = claveAcceso.Trim(),
            FechaEmision            = fechaEmision,
            Estado                  = "Borrador",
        };
        nota.SetCreated(createdBy);
        return nota;
    }

    public void Validar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException($"Solo se puede validar una nota en Borrador (estado: {Estado}).");
        Estado = "Validado";
        SetUpdated(userId);
    }

    public void AgregarDetalle(VentasNotaDetalle detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
        RecalcularTotales();
    }

    private void RecalcularTotales()
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        Impuesto = _detalles.Sum(d => d.Impuesto);
        Total    = _detalles.Sum(d => d.Total);
    }

    public void AutorizarNotaCredito(
        Guid userId,
        Guid bodegaId,
        string numeroAutorizacion,
        DateTime fechaAutorizacion,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        Guid asientoContableId,
        IReadOnlyList<NotaCreditoStockLine>? stockLines = null)
    {
        if (Estado != "Validado")
            throw new InvalidOperationException($"Solo se puede autorizar una nota Validada (estado: {Estado}).");
        if (!string.Equals(TipoNota, "CREDITO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AutorizarNotaCredito solo aplica a notas de crédito.");

        Estado = "Autorizado";
        NumeroAutorizacion = numeroAutorizacion;
        FechaAutorizacion = fechaAutorizacion;
        XmlGeneradoPath = string.IsNullOrWhiteSpace(xmlGeneradoPath) ? null : xmlGeneradoPath;
        XmlAutorizacionPath = string.IsNullOrWhiteSpace(xmlAutorizacionPath) ? null : xmlAutorizacionPath;
        AsientoContableId = asientoContableId;
        SetUpdated(userId);

        var lines = stockLines ?? Array.Empty<NotaCreditoStockLine>();
        if (lines.Count > 0)
        {
            var numero = $"{Establecimiento}-{PuntoEmision}-{Secuencial}";
            RaiseDomainEvent(new NotaCreditoAutorizadaEvent(
                Id, TenantId, userId, bodegaId, numero, lines));
        }
    }

    public void AutorizarNotaDebito(
        Guid userId,
        string numeroAutorizacion,
        DateTime fechaAutorizacion,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        Guid asientoContableId)
    {
        if (Estado != "Validado")
            throw new InvalidOperationException($"Solo se puede autorizar una nota Validada (estado: {Estado}).");
        if (!string.Equals(TipoNota, "DEBITO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AutorizarNotaDebito solo aplica a notas de débito.");

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
