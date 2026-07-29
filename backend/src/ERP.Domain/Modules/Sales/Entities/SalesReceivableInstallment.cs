using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesReceivableInstallment : IMustHaveTenant
{
    public const int StatusMaxLen = 20;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReceivableId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; } = "pending";

    private SalesReceivableInstallment() { }

    internal static SalesReceivableInstallment Create(
        Guid receivableId,
        Guid tenantId,
        int installmentNumber,
        DateOnly dueDate,
        decimal amount
    )
    {
        if (installmentNumber < 1)
            throw new ArgumentException(
                "El número de cuota debe ser al menos 1.",
                nameof(installmentNumber)
            );
        if (amount <= 0)
            throw new ArgumentException(
                "El monto de la cuota debe ser mayor a cero.",
                nameof(amount)
            );

        return new SalesReceivableInstallment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceivableId = receivableId,
            InstallmentNumber = installmentNumber,
            DueDate = dueDate,
            Amount = amount,
            PaidAmount = 0,
            Status = "pending",
        };
    }
}
