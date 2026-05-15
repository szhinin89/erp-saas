using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class CompraDetalleRetencionEmitida : AuditableEntity, ITenantEntity
{
    public const int ImpuestoMaxLen = 20;
    public const int CodigoRetencionMaxLen = 10;
    public const int FacturaRelacionadaMaxLen = 50;

    public Guid CompraRetencionEmitidaId { get; private set; }
    /// <summary>IVA o RENTA.</summary>
    public string Impuesto { get; private set; } = null!;
    public string CodigoRetencion { get; private set; } = null!;
    public decimal BaseImponible { get; private set; }
    public decimal PorcentajeRetencion { get; private set; }
    public decimal ValorRetenido { get; private set; }
    public string? FacturaRelacionada { get; private set; }

    private CompraDetalleRetencionEmitida() { }

    public static CompraDetalleRetencionEmitida Create(
        Guid tenantId,
        string impuesto,
        string codigoRetencion,
        decimal baseImponible,
        decimal porcentajeRetencion,
        decimal valorRetenido,
        string? facturaRelacionada,
        Guid createdBy)
    {
        var d = new CompraDetalleRetencionEmitida
        {
            Id                      = Guid.NewGuid(),
            TenantId                = tenantId,
            CompraRetencionEmitidaId = Guid.Empty,
            Impuesto                = (impuesto ?? string.Empty).Trim().ToUpperInvariant(),
            CodigoRetencion         = (codigoRetencion ?? string.Empty).Trim(),
            BaseImponible           = baseImponible,
            PorcentajeRetencion     = porcentajeRetencion,
            ValorRetenido           = valorRetenido,
            FacturaRelacionada      = string.IsNullOrWhiteSpace(facturaRelacionada) ? null : facturaRelacionada.Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AsignarRetencionId(Guid retencionId)
    {
        if (CompraRetencionEmitidaId != Guid.Empty)
            throw new InvalidOperationException("El detalle ya está asignado.");
        CompraRetencionEmitidaId = retencionId;
    }
}
