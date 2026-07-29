using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class PurchasePaymentSchedule : IMustHaveTenant
{
    public const int NotesMaxLen = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private PurchasePaymentSchedule() { }

    public static PurchasePaymentSchedule Create(
        Guid purchaseInvoiceId,
        Guid tenantId,
        int installmentNumber,
        DateOnly dueDate,
        decimal amount,
        string? notes = null
    )
    {
        if (installmentNumber < 1)
            throw new ArgumentException(
                "El número de cuota debe ser >= 1.",
                nameof(installmentNumber)
            );
        if (amount <= 0)
            throw new ArgumentException(
                "El monto de la cuota debe ser mayor a cero.",
                nameof(amount)
            );

        return new PurchasePaymentSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseInvoiceId = purchaseInvoiceId,
            InstallmentNumber = installmentNumber,
            DueDate = dueDate,
            Amount = amount,
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
