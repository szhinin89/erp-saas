using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchasePayableInstallment : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayableId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; } = "pending";

    private PurchasePayableInstallment() { }

    internal static PurchasePayableInstallment Create(
        Guid payableId, Guid tenantId, int number, DateOnly dueDate, decimal amount)
    {
        return new PurchasePayableInstallment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PayableId = payableId,
            InstallmentNumber = number,
            DueDate = dueDate,
            Amount = amount,
            PaidAmount = 0,
            Status = "pending",
        };
    }
}
