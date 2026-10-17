using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// ACCOUNTING-12A — cuenta contable de un <see cref="InventoryAdjustmentReason"/> para UNA Company.
/// Entidad separada (mismo criterio que <c>PaymentMethodAccount</c>): el motivo puede ser tenant-wide
/// mientras que <c>Account</c> es estrictamente Company-scoped, así que un campo escalar en el motivo no
/// podría apuntar a la cuenta de cada Company. Sin fila para (Company, motivo) el ajuste opera y su
/// obligación contable queda Failed hasta configurarla (nunca un default oculto).
/// </summary>
public sealed class InventoryAdjustmentReasonAccount
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid InventoryAdjustmentReasonId { get; private set; }
    public Guid AccountingAccountId { get; private set; }

    private InventoryAdjustmentReasonAccount() { }

    public static InventoryAdjustmentReasonAccount Create(
        Guid tenantId,
        Guid companyId,
        Guid inventoryAdjustmentReasonId,
        Guid accountingAccountId,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (inventoryAdjustmentReasonId == Guid.Empty)
            throw new ArgumentException("El motivo de ajuste es obligatorio.", nameof(inventoryAdjustmentReasonId));
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountingAccountId));

        var link = new InventoryAdjustmentReasonAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            InventoryAdjustmentReasonId = inventoryAdjustmentReasonId,
            AccountingAccountId = accountingAccountId,
        };
        link.SetCreated(createdBy);
        return link;
    }

    public void ChangeAccount(Guid accountingAccountId, Guid updatedBy)
    {
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountingAccountId));
        AccountingAccountId = accountingAccountId;
        SetUpdated(updatedBy);
    }
}
