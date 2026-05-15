using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class VentasDetalleRetencionRecibida : AuditableEntity, ITenantEntity
{
    public const int ImpuestoMaxLen = 20;
    public const int CodigoRetencionMaxLen = 10;

    public Guid VentasRetencionRecibidaId { get; private set; }
    public string Impuesto { get; private set; } = null!;
    public string CodigoRetencion { get; private set; } = null!;
    public decimal BaseImponible { get; private set; }
    public decimal PorcentajeRetencion { get; private set; }
    public decimal ValorRetenido { get; private set; }

    private VentasDetalleRetencionRecibida() { }

    public static VentasDetalleRetencionRecibida Create(
        Guid tenantId,
        string impuesto,
        string codigoRetencion,
        decimal baseImponible,
        decimal porcentajeRetencion,
        decimal valorRetenido,
        Guid createdBy)
    {
        var d = new VentasDetalleRetencionRecibida
        {
            Id                        = Guid.NewGuid(),
            TenantId                  = tenantId,
            VentasRetencionRecibidaId  = Guid.Empty,
            Impuesto                  = (impuesto ?? string.Empty).Trim().ToUpperInvariant(),
            CodigoRetencion           = (codigoRetencion ?? string.Empty).Trim(),
            BaseImponible             = baseImponible,
            PorcentajeRetencion       = porcentajeRetencion,
            ValorRetenido             = valorRetenido,
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AsignarRetencionId(Guid retencionId)
    {
        if (VentasRetencionRecibidaId != Guid.Empty)
            throw new InvalidOperationException("El detalle ya está asignado.");
        VentasRetencionRecibidaId = retencionId;
    }
}
