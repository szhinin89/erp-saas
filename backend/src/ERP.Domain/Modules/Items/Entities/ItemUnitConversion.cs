using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Conversión entre unidades de medida estándar (kg ↔ lb, m ↔ ft).
/// No usar para empaques logísticos — usar ItemPackagingLevel para eso.
/// </summary>
public sealed class ItemUnitConversion : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public string FromUomCode { get; private set; } = null!;
    public string ToUomCode { get; private set; } = null!;
    public decimal Factor { get; private set; }
    public bool IsActive { get; private set; }

    private ItemUnitConversion() { }

    public static ItemUnitConversion Create(
        Guid itemId, Guid tenantId,
        string fromUomCode, string toUomCode, decimal factor, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(fromUomCode))
            throw new ArgumentException("UOM origen es obligatorio.", nameof(fromUomCode));
        if (string.IsNullOrWhiteSpace(toUomCode))
            throw new ArgumentException("UOM destino es obligatorio.", nameof(toUomCode));
        if (factor <= 0)
            throw new ArgumentException("El factor de conversión debe ser mayor que cero.", nameof(factor));

        var entity = new ItemUnitConversion
        {
            Id           = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId       = itemId,
            FromUomCode  = fromUomCode.Trim().ToUpperInvariant(),
            ToUomCode    = toUomCode.Trim().ToUpperInvariant(),
            Factor       = factor,
            IsActive     = true,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Disable(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}
