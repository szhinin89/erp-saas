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

    public decimal OutstandingAmount => Amount - PaidAmount;

    private SalesReceivableInstallment() { }

    /// <summary>
    /// CXC-E2E-01 — acumula en la cuota la parte del cobro que le corresponde; la CxC decide el
    /// reparto (<see cref="SalesReceivable.RegisterCollection"/>) y valida los límites.
    /// </summary>
    internal void RegisterCollection(decimal amount)
    {
        PaidAmount += amount;
        RecalculateStatus();
    }

    /// <summary>
    /// Libera hasta <paramref name="amount"/> de lo cobrado en la cuota y devuelve lo liberado —
    /// nunca deja <see cref="PaidAmount"/> negativo (cuotas reconstruidas o cobradas antes de que la
    /// cuota acumulara lo cobrado no tienen nada que liberar).
    /// </summary>
    internal decimal ReverseCollection(decimal amount)
    {
        var released = Math.Min(amount, PaidAmount);
        PaidAmount -= released;
        RecalculateStatus();
        return released;
    }

    private void RecalculateStatus() =>
        Status =
            PaidAmount >= Amount ? "paid"
            : PaidAmount > 0 ? "partial"
            : "pending";

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
