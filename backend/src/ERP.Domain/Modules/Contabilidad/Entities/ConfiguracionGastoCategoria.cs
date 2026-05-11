using ERP.Domain.Common;

namespace ERP.Domain.Modules.Contabilidad.Entities;

/// <summary>Mapea una categoría de gasto (texto libre del módulo Gastos) a una cuenta de gasto.</summary>
public sealed class ConfiguracionGastoCategoria : AuditableEntity, ITenantEntity
{
    public const int CategoriaMaxLen = 120;

    public string Categoria { get; private set; } = null!;
    public Guid CuentaGastoId { get; private set; }

    private ConfiguracionGastoCategoria() { }

    public static ConfiguracionGastoCategoria Create(
        Guid tenantId,
        string categoria,
        Guid cuentaGastoId,
        Guid createdBy)
    {
        var cat = categoria.Trim();
        if (cat.Length == 0)
            throw new ArgumentException("La categoría no puede estar vacía.", nameof(categoria));

        var e = new ConfiguracionGastoCategoria
        {
            TenantId       = tenantId,
            Categoria      = cat,
            CuentaGastoId  = cuentaGastoId,
        };
        e.SetCreated(createdBy);
        return e;
    }

    public void UpdateCuenta(Guid cuentaGastoId, Guid updatedBy)
    {
        CuentaGastoId = cuentaGastoId;
        SetUpdated(updatedBy);
    }
}
