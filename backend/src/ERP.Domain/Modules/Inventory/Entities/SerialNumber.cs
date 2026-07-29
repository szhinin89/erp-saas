using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Aggregate Root. Número de serie de una unidad individual.
/// Scope: company.
/// REGLA: Solo se crea desde PurchaseReceipt.Confirm() o ajuste de saldo inicial.
/// Máquina de estados: InStock → Sold → Returned → InStock / Defective / Lost
/// </summary>
public sealed class SerialNumber : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string Serial { get; private set; } = null!;
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public SerialStatus Status { get; private set; }
    public DateTime AcquiredAt { get; private set; }
    public DateTime? SoldAt { get; private set; }
    public string? DocumentRef { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ReceiptLineId { get; private set; }

    private SerialNumber() { }

    public static SerialNumber Create(
        Guid tenantId,
        Guid companyId,
        string serial,
        Guid itemId,
        Guid warehouseId,
        Guid createdBy,
        Guid? variantId = null,
        Guid? lotId = null,
        string? notes = null,
        Guid? receiptLineId = null)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("El número de serie es obligatorio.", nameof(serial));

        var sn = new SerialNumber
        {
            TenantId = tenantId,
            CompanyId = companyId,
            Serial = serial.Trim(),
            ItemId = itemId,
            VariantId = variantId,
            WarehouseId = warehouseId,
            LotId = lotId,
            Status = SerialStatus.InStock,
            AcquiredAt = DateTime.UtcNow,
            Notes = notes?.Trim(),
            ReceiptLineId = receiptLineId,
        };
        sn.SetCreated(createdBy);
        return sn;
    }

    public void Sell(string documentRef, Guid updatedBy)
    {
        if (Status != SerialStatus.InStock)
            throw new InvalidOperationException($"El serial '{Serial}' no está disponible (estado: {Status}).");

        Status = SerialStatus.Sold;
        SoldAt = DateTime.UtcNow;
        DocumentRef = documentRef.Trim();
        SetUpdated(updatedBy);
    }

    public void Return(Guid updatedBy)
    {
        if (Status != SerialStatus.Sold)
            throw new InvalidOperationException($"El serial '{Serial}' no puede devolverse (estado: {Status}).");

        Status = SerialStatus.Returned;
        SoldAt = null;
        SetUpdated(updatedBy);
    }

    public void ReInstock(Guid updatedBy)
    {
        if (Status != SerialStatus.Returned)
            throw new InvalidOperationException($"Solo los seriales devueltos pueden re-ingresarse (estado: {Status}).");

        Status = SerialStatus.InStock;
        SetUpdated(updatedBy);
    }

    public void MarkDefective(Guid updatedBy)
    {
        if (Status is not (SerialStatus.InStock or SerialStatus.Returned))
            throw new InvalidOperationException($"No se puede marcar como defectuoso desde el estado {Status}.");

        Status = SerialStatus.Defective;
        SetUpdated(updatedBy);
    }

    public void MarkLost(Guid updatedBy)
    {
        if (Status != SerialStatus.InStock)
            throw new InvalidOperationException($"Solo los seriales en stock pueden marcarse como perdidos (estado: {Status}).");

        Status = SerialStatus.Lost;
        SetUpdated(updatedBy);
    }

    public void Dispose(Guid updatedBy)
    {
        if (Status != SerialStatus.Defective)
            throw new InvalidOperationException($"Solo los seriales defectuosos pueden darse de baja (estado: {Status}).");

        Status = SerialStatus.Disposed;
        SetUpdated(updatedBy);
    }
}
