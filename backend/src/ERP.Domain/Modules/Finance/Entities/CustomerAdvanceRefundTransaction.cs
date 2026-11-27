using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// ADR-052 §4 — hecho financiero append-only (1:1 con su movimiento) de una devolución de anticipo
/// o de su reversa: destino usado, medio, referencia, fecha y vínculo de caja. El saldo sigue siendo
/// exclusivo del libro de <see cref="CustomerAdvance"/>. Su único dato posterior a la creación es el
/// vínculo, de una sola vez, con el movimiento de caja.
/// </summary>
public sealed class CustomerAdvanceRefundTransaction : ICompanyOperationalEntity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerAdvanceId { get; private set; }
    public Guid CustomerAdvanceMovementId { get; private set; }
    public CustomerAdvanceRefundTransactionType TransactionType { get; private set; }

    /// <summary>Solo en <see cref="CustomerAdvanceRefundTransactionType.RefundReversed"/>.</summary>
    public Guid? OriginalTransactionId { get; private set; }

    /// <summary>Exactamente uno de este campo o <see cref="CashRegisterId"/>.</summary>
    public Guid? CompanyBankAccountId { get; private set; }

    public Guid? CashRegisterId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }
    public string Reference { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid ClientRequestId { get; private set; }
    public string PayloadHash { get; private set; } = null!;

    private CustomerAdvanceRefundTransaction() { }

    public static CustomerAdvanceRefundTransaction CreateRefund(
        CustomerAdvance advance,
        CustomerAdvanceMovement movement,
        Guid paymentMethodId,
        Guid? companyBankAccountId,
        Guid? cashRegisterId
    )
    {
        ArgumentNullException.ThrowIfNull(advance);
        ArgumentNullException.ThrowIfNull(movement);
        if (
            movement.CustomerAdvanceId != advance.Id
            || movement.MovementType != CustomerAdvanceMovementType.Refund
        )
            throw new DomainRuleViolationException(
                "La transacción requiere un movimiento de devolución de este anticipo."
            );
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException(
                "El medio de pago es obligatorio.",
                nameof(paymentMethodId)
            );
        CustomerAdvanceGuards.Destination(companyBankAccountId, cashRegisterId);

        return FromMovement(
            movement,
            advance.CurrencyCode,
            CustomerAdvanceRefundTransactionType.Refund,
            originalTransactionId: null,
            companyBankAccountId,
            cashRegisterId,
            paymentMethodId
        );
    }

    /// <summary>La reversa hereda destino, medio y moneda de la devolución original.</summary>
    public static CustomerAdvanceRefundTransaction CreateReversal(
        CustomerAdvanceRefundTransaction original,
        CustomerAdvanceMovement movement
    )
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(movement);
        if (original.TransactionType != CustomerAdvanceRefundTransactionType.Refund)
            throw new DomainRuleViolationException("Solo se puede revertir una devolución.");
        if (
            movement.MovementType != CustomerAdvanceMovementType.ReversalOfRefund
            || movement.TenantId != original.TenantId
            || movement.CompanyId != original.CompanyId
            || movement.CustomerAdvanceId != original.CustomerAdvanceId
            || movement.ReversalOfMovementId != original.CustomerAdvanceMovementId
            || movement.Amount != original.Amount
        )
            throw new DomainRuleViolationException(
                "El movimiento no corresponde a la reversa de esta devolución."
            );

        return FromMovement(
            movement,
            original.CurrencyCode,
            CustomerAdvanceRefundTransactionType.RefundReversed,
            original.Id,
            original.CompanyBankAccountId,
            original.CashRegisterId,
            original.PaymentMethodId
        );
    }

    public void LinkCashMovement(Guid cashSessionId, Guid cashMovementId)
    {
        if (CashRegisterId is null)
            throw new DomainRuleViolationException(
                "Solo una transacción con caja destino puede vincularse a un movimiento de caja."
            );
        if (CashMovementId is not null)
            throw new DomainRuleViolationException(
                "La transacción ya está vinculada a un movimiento de caja."
            );
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La sesión de caja es obligatoria.", nameof(cashSessionId));
        if (cashMovementId == Guid.Empty)
            throw new ArgumentException(
                "El movimiento de caja es obligatorio.",
                nameof(cashMovementId)
            );

        CashSessionId = cashSessionId;
        CashMovementId = cashMovementId;
    }

    private static CustomerAdvanceRefundTransaction FromMovement(
        CustomerAdvanceMovement movement,
        string currencyCode,
        CustomerAdvanceRefundTransactionType type,
        Guid? originalTransactionId,
        Guid? companyBankAccountId,
        Guid? cashRegisterId,
        Guid paymentMethodId
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = movement.TenantId,
            CompanyId = movement.CompanyId,
            CustomerAdvanceId = movement.CustomerAdvanceId,
            CustomerAdvanceMovementId = movement.Id,
            TransactionType = type,
            OriginalTransactionId = originalTransactionId,
            CompanyBankAccountId = companyBankAccountId,
            CashRegisterId = cashRegisterId,
            PaymentMethodId = paymentMethodId,
            Amount = movement.Amount,
            CurrencyCode = currencyCode,
            EffectiveDate = movement.EffectiveDate,
            Reference = movement.Reference!,
            Reason = movement.Reason!,
            CreatedByUserId = movement.CreatedByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            ClientRequestId = movement.ClientRequestId,
            PayloadHash = movement.RequestPayloadHash,
        };
}
