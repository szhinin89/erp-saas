using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

/// <summary>Tasas y códigos SRI de retención configurables por tenant.</summary>
public sealed class ConfiguracionRetencion : AuditableEntity, ITenantEntity
{
    public const int ImpuestoMaxLen = 20;
    public const int TipoSujetoMaxLen = 20;
    public const int CodigoSriMaxLen = 10;

    /// <summary>IVA o RENTA.</summary>
    public string Impuesto { get; private set; } = null!;
    /// <summary>PROVEEDOR, CLIENTE o AMBOS.</summary>
    public string TipoSujeto { get; private set; } = null!;
    public string CodigoSri { get; private set; } = null!;
    public decimal Porcentaje { get; private set; }
    public bool Activo { get; private set; } = true;

    private ConfiguracionRetencion() { }

    public static ConfiguracionRetencion Create(
        Guid tenantId,
        string impuesto,
        string tipoSujeto,
        string codigoSri,
        decimal porcentaje,
        Guid createdBy)
    {
        var c = new ConfiguracionRetencion
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            Impuesto    = (impuesto ?? string.Empty).Trim().ToUpperInvariant(),
            TipoSujeto  = (tipoSujeto ?? string.Empty).Trim().ToUpperInvariant(),
            CodigoSri   = (codigoSri ?? string.Empty).Trim(),
            Porcentaje  = porcentaje,
            Activo      = true,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void SetActivo(bool activo, Guid userId)
    {
        Activo = activo;
        SetUpdated(userId);
    }
}
