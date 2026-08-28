using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// PAYABLES-PURCHASE-MIGRATION-10 — cuota/vencimiento de una <see cref="AccountsPayable"/>, y única
/// fuente viva de saldo (decisión funcional del ticket: "AccountsPayableInstallment es la
/// cuota/saldo vivo"). Todo pago/abono/ajuste (pago, retención, devolución, crédito de proveedor,
/// nota de crédito) muta esta entidad — nunca un acumulador de cabecera — y
/// <see cref="AccountsPayable"/> deriva sus totales sumando sus cuotas. Reemplaza a
/// <c>PurchasePayableInstallment</c> (eliminado, nunca tuvo saldo vivo propio: era solo un split de
/// fechas de vencimiento recalculado desde la cabecera).
/// </summary>
public sealed class AccountsPayableInstallment : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AccountsPayableId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RetainedAmount { get; private set; }
    public decimal ReturnCreditAmount { get; private set; }
    public decimal SupplierCreditAmount { get; private set; }
    public decimal CreditNoteAmount { get; private set; }
    public AccountsPayableStatus Status { get; private set; } = AccountsPayableStatus.Pending;

    public decimal OutstandingAmount =>
        Math.Round(
            Amount - PaidAmount - RetainedAmount - ReturnCreditAmount - SupplierCreditAmount - CreditNoteAmount,
            2,
            MidpointRounding.AwayFromZero
        );

    private AccountsPayableInstallment() { }

    /// <summary>
    /// Interno — solo <see cref="AccountsPayable.AddInstallment"/> puede crear cuotas, para que la
    /// numeración/unicidad de <c>InstallmentNumber</c> quede garantizada por el aggregate, nunca
    /// por el llamador.
    /// </summary>
    internal static AccountsPayableInstallment Create(
        Guid accountsPayableId,
        Guid tenantId,
        int installmentNumber,
        DateOnly dueDate,
        decimal amount
    )
    {
        if (accountsPayableId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta por pagar es obligatoria.",
                nameof(accountsPayableId)
            );
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (installmentNumber < 1)
            throw new ArgumentException(
                "El número de cuota debe ser mayor o igual a 1.",
                nameof(installmentNumber)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto de la cuota debe ser mayor a cero.", nameof(amount));

        return new AccountsPayableInstallment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountsPayableId = accountsPayableId,
            InstallmentNumber = installmentNumber,
            DueDate = dueDate,
            Amount = amount,
            Status = AccountsPayableStatus.Pending,
        };
    }

    internal decimal GetApplied(AccountsPayableAdjustmentType type) =>
        type switch
        {
            AccountsPayableAdjustmentType.Payment => PaidAmount,
            AccountsPayableAdjustmentType.Retention => RetainedAmount,
            AccountsPayableAdjustmentType.ReturnCredit => ReturnCreditAmount,
            AccountsPayableAdjustmentType.SupplierCredit => SupplierCreditAmount,
            AccountsPayableAdjustmentType.CreditNote => CreditNoteAmount,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    /// <summary>Interno — solo invocado por el motor de asignación FIFO de <see cref="AccountsPayable"/>.</summary>
    internal void Apply(AccountsPayableAdjustmentType type, decimal amount)
    {
        switch (type)
        {
            case AccountsPayableAdjustmentType.Payment:
                PaidAmount += amount;
                break;
            case AccountsPayableAdjustmentType.Retention:
                RetainedAmount += amount;
                break;
            case AccountsPayableAdjustmentType.ReturnCredit:
                ReturnCreditAmount += amount;
                break;
            case AccountsPayableAdjustmentType.SupplierCredit:
                SupplierCreditAmount += amount;
                break;
            case AccountsPayableAdjustmentType.CreditNote:
                CreditNoteAmount += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
        RecalculateStatus();
    }

    /// <summary>Interno — inverso de <see cref="Apply"/>, mismo motor FIFO (en orden inverso).</summary>
    internal void Reverse(AccountsPayableAdjustmentType type, decimal amount)
    {
        switch (type)
        {
            case AccountsPayableAdjustmentType.Payment:
                PaidAmount -= amount;
                break;
            case AccountsPayableAdjustmentType.Retention:
                RetainedAmount -= amount;
                break;
            case AccountsPayableAdjustmentType.ReturnCredit:
                ReturnCreditAmount -= amount;
                break;
            case AccountsPayableAdjustmentType.SupplierCredit:
                SupplierCreditAmount -= amount;
                break;
            case AccountsPayableAdjustmentType.CreditNote:
                CreditNoteAmount -= amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
        RecalculateStatus();
    }

    private void RecalculateStatus()
    {
        if (Status == AccountsPayableStatus.Cancelled)
            return;

        if (OutstandingAmount <= 0)
            Status = AccountsPayableStatus.Paid;
        else if (
            PaidAmount > 0
            || RetainedAmount > 0
            || ReturnCreditAmount > 0
            || SupplierCreditAmount > 0
            || CreditNoteAmount > 0
        )
            Status = AccountsPayableStatus.PartiallyPaid;
        else
            Status = AccountsPayableStatus.Pending;
    }

    internal void MarkCancelled() => Status = AccountsPayableStatus.Cancelled;
}
