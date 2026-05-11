using ERP.Domain.Common;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>Gasto menor pagado desde caja chica.</summary>
public sealed class GastoCajaChica : AuditableEntity, ITenantEntity
{
    public const int ConceptoMaxLen           = 500;
    public const int TipoComprobanteMaxLen    = 40;
    public const int NumeroComprobanteMaxLen  = 80;

    public Guid CajaChicaId { get; private set; }
    public DateTime Fecha { get; private set; }
    public string Concepto { get; private set; } = null!;
    public decimal Monto { get; private set; }
    public string TipoComprobante { get; private set; } = null!;
    public string NumeroComprobante { get; private set; } = "";
    public Guid? AsientoContableId { get; private set; }

    private GastoCajaChica() { }

    public static GastoCajaChica Create(
        Guid tenantId,
        Guid cajaChicaId,
        DateTime fecha,
        string concepto,
        decimal monto,
        string tipoComprobante,
        string? numeroComprobante,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(concepto))
            throw new ArgumentException("El concepto es obligatorio.", nameof(concepto));
        if (monto <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.", nameof(monto));
        if (string.IsNullOrWhiteSpace(tipoComprobante))
            throw new ArgumentException("El tipo de comprobante es obligatorio.", nameof(tipoComprobante));

        var g = new GastoCajaChica
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            CajaChicaId        = cajaChicaId,
            Fecha              = fecha,
            Concepto           = concepto.Trim(),
            Monto              = monto,
            TipoComprobante    = tipoComprobante.Trim(),
            NumeroComprobante  = numeroComprobante?.Trim() ?? "",
            AsientoContableId  = null,
        };
        g.SetCreated(createdBy);
        return g;
    }

    public void VincularAsiento(Guid asientoId, Guid updatedBy)
    {
        AsientoContableId = asientoId;
        SetUpdated(updatedBy);
    }
}
