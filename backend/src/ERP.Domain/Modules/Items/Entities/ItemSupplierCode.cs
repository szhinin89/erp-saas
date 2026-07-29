using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemSupplierCode : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string Code { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }

    private ItemSupplierCode() { }

    /// <summary>
    /// Un código de proveedor siempre está asociado a un proveedor específico —
    /// no existe el concepto de "código de proveedor sin proveedor" (Fase 2).
    /// </summary>
    public static ItemSupplierCode Create(
        Guid itemId, Guid tenantId, Guid supplierId,
        string code, Guid createdBy, bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de proveedor es obligatorio.", nameof(code));
        if (code.Length > 100)
            throw new ArgumentException("El código de proveedor no puede superar 100 caracteres.", nameof(code));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));

        var entity = new ItemSupplierCode
        {
            Id         = Guid.NewGuid(),
            TenantId   = tenantId,
            ItemId     = itemId,
            SupplierId = supplierId,
            Code       = code.Trim(),
            IsPrimary  = isPrimary,
            IsActive   = true,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Disable() => IsActive = false;
    public void Enable()  => IsActive = true;
    public void MarkAsPrimary() => IsPrimary = true;
    public void UnmarkAsPrimary() => IsPrimary = false;
}
