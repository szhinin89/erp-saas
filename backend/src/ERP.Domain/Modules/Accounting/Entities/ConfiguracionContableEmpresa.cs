using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>Valores por defecto de cuentas contables por tenant para asientos automáticos.</summary>
public sealed class ConfiguracionContableEmpresa : AuditableEntity, ITenantEntity
{
    public Guid? CuentaInventarioId { get; private set; }
    public Guid? CuentaCostoVentaId { get; private set; }
    public Guid? CuentaProveedoresId { get; private set; }

    public Guid? CuentaVentasId { get; private set; }
    public Guid? CuentaClientesId { get; private set; }

    public Guid? CuentaIvaComprasId { get; private set; }
    public Guid? CuentaIvaVentasId { get; private set; }

    public Guid? CuentaEfectivoId { get; private set; }
    public Guid? CuentaBancoId { get; private set; }

    private ConfiguracionContableEmpresa() { }

    public static ConfiguracionContableEmpresa Create(Guid tenantId, Guid createdBy)
    {
        var e = new ConfiguracionContableEmpresa { TenantId = tenantId };
        e.SetCreated(createdBy);
        return e;
    }

    public void UpdateCuentas(
        Guid? cuentaInventarioId,
        Guid? cuentaCostoVentaId,
        Guid? cuentaProveedoresId,
        Guid? cuentaVentasId,
        Guid? cuentaClientesId,
        Guid? cuentaIvaComprasId,
        Guid? cuentaIvaVentasId,
        Guid? cuentaEfectivoId,
        Guid? cuentaBancoId,
        Guid updatedBy)
    {
        CuentaInventarioId   = cuentaInventarioId;
        CuentaCostoVentaId   = cuentaCostoVentaId;
        CuentaProveedoresId  = cuentaProveedoresId;
        CuentaVentasId       = cuentaVentasId;
        CuentaClientesId     = cuentaClientesId;
        CuentaIvaComprasId   = cuentaIvaComprasId;
        CuentaIvaVentasId    = cuentaIvaVentasId;
        CuentaEfectivoId     = cuentaEfectivoId;
        CuentaBancoId        = cuentaBancoId;
        SetUpdated(updatedBy);
    }
}
