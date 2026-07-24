using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Aggregate Root. Lote de inventario con trazabilidad de fabricación y vencimiento.
/// Scope: company — cada empresa tiene sus propios lotes.
/// REGLA: Solo se crea desde PurchaseReceipt.Confirm() o ajuste de saldo inicial.
/// </summary>
public sealed class Lot : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string LotNumber { get; private set; } = null!;
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateOnly? ManufactureDate { get; private set; }
    public DateOnly? ExpirationDate { get; private set; }
    public decimal InitialQty { get; private set; }
    public decimal CurrentQty { get; private set; }
    public LotStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ReceiptLineId { get; private set; }

    private Lot() { }

    public static Lot Create(
        Guid tenantId,
        Guid companyId,
        string lotNumber,
        Guid itemId,
        Guid warehouseId,
        decimal initialQty,
        Guid createdBy,
        Guid? variantId = null,
        DateOnly? manufactureDate = null,
        DateOnly? expirationDate = null,
        string? notes = null,
        Guid? receiptLineId = null)
    {
        if (string.IsNullOrWhiteSpace(lotNumber))
            throw new ArgumentException("El número de lote es obligatorio.", nameof(lotNumber));
        if (initialQty <= 0)
            throw new ArgumentException("La cantidad inicial debe ser mayor que cero.", nameof(initialQty));
        if (expirationDate.HasValue && manufactureDate.HasValue && expirationDate < manufactureDate)
            throw new ArgumentException("La fecha de vencimiento no puede ser anterior a la de fabricación.");

        var lot = new Lot
        {
            TenantId    = tenantId,
            CompanyId       = companyId,
            LotNumber       = lotNumber.Trim(),
            ItemId          = itemId,
            VariantId       = variantId,
            WarehouseId     = warehouseId,
            ManufactureDate = manufactureDate,
            ExpirationDate  = expirationDate,
            InitialQty      = initialQty,
            CurrentQty      = initialQty,
            Status          = LotStatus.Active,
            Notes           = notes?.Trim(),
            ReceiptLineId   = receiptLineId,
        };
        lot.SetCreated(createdBy);
        return lot;
    }

    public void ConsumeQty(decimal qty)
    {
        if (Status == LotStatus.Depleted)
            throw new InvalidOperationException($"El lote '{LotNumber}' está agotado.");
        if (qty > CurrentQty)
            throw new InvalidOperationException($"Stock insuficiente en lote '{LotNumber}'. Disponible: {CurrentQty}, requerido: {qty}.");

        CurrentQty -= qty;

        if (CurrentQty == 0)
            Status = LotStatus.Depleted;
    }

    public void RegisterEntry(decimal qty)
    {
        if (Status == LotStatus.Depleted)
            throw new InvalidOperationException($"No se puede agregar stock a un lote agotado '{LotNumber}'.");

        CurrentQty += qty;
    }

    public void ChangeStatus(LotStatus newStatus, Guid updatedBy)
    {
        if (newStatus == LotStatus.Active && Status == LotStatus.Depleted)
            throw new InvalidOperationException("No se puede reactivar un lote agotado.");

        Status = newStatus;
        SetUpdated(updatedBy);
    }

    public void UpdateNotes(string? notes, Guid updatedBy)
    {
        Notes = notes?.Trim();
        SetUpdated(updatedBy);
    }
}
