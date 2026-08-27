using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 — cuota/vencimiento de una <see cref="AccountsPayable"/>. Entidad
/// hija del mismo aggregate, sin repositorio propio (mismo patrón cabecera+líneas usado en todo el
/// ERP: <c>ExpenseDocument</c>+<c>ExpenseLine</c>, <c>PurchasePayable</c>+<c>PurchasePayableInstallment</c>).
/// Pago/abono aún no existen en esta fase — <see cref="PaidAmount"/> nace y permanece en 0 hasta que
/// la fase de Pagos agregue el método que lo mute (nunca lo hace este archivo).
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
    public AccountsPayableStatus Status { get; private set; } = AccountsPayableStatus.Pending;

    public decimal OutstandingAmount =>
        Math.Round(Amount - PaidAmount, 2, MidpointRounding.AwayFromZero);

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
            PaidAmount = 0m,
            Status = AccountsPayableStatus.Pending,
        };
    }
}
