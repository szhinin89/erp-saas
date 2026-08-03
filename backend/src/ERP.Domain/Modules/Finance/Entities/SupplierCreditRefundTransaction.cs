using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// Entidad hija append-only de <c>SupplierCredit</c> (vía <c>SupplierCreditMovement</c>, relación
/// 1:1 estricta) que almacena tanto el ingreso original del reembolso (<see cref="RefundTransactionTypeCode.RefundReceived"/>)
/// como su reversa compensatoria (<see cref="RefundTransactionTypeCode.RefundReversed"/>) — diseño
/// §6.4. Sin mutadores tras <see cref="CreateReceived"/>/<see cref="CreateReversal"/>: es la única
/// fuente de verdad del hecho financiero del reembolso (destino usado, cuenta contable
/// efectivamente usada, método, referencia, fecha efectiva, efecto de caja) — nunca del saldo del
/// crédito, que sigue siendo exclusivo de <c>SupplierCreditMovement</c>/<c>SupplierCredit.AvailableAmount</c>
/// (§13.5).
/// </summary>
public sealed class SupplierCreditRefundTransaction
    : ICompanyOperationalEntity
{
    public const int PaymentMethodCodeMaxLen = 20;
    public const int ExternalReferenceMaxLen = 100;
    public const int ReasonMaxLen = 500;
    public const int CurrencyCodeMaxLen = 3;
    public const int SnapshotMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid SupplierCreditId { get; private set; }

    /// <summary>Única FK autoritativa de la relación 1:1 con el movimiento que la origina (§6.4, §10).</summary>
    public Guid SupplierCreditMovementId { get; private set; }

    public RefundTransactionTypeCode TransactionTypeCode { get; private set; }

    /// <summary>Solo tiene valor cuando <see cref="TransactionTypeCode"/> == <see cref="RefundTransactionTypeCode.RefundReversed"/>.</summary>
    public Guid? OriginalTransactionId { get; private set; }

    public Guid FinancialDestinationId { get; private set; }

    /// <summary>
    /// Cuenta contable realmente usada por esta transacción concreta — congelada al confirmar,
    /// nunca resuelta de nuevo contra el destino (§6.4bis). Fuente autoritativa del asiento y de
    /// todo reporte histórico, nunca <see cref="AccountingAccountCodeSnapshot"/> ni el
    /// <c>AccountingAccountId</c> mutable actual de <see cref="CompanyFinancialDestination"/>.
    /// </summary>
    public Guid AccountingAccountId { get; private set; }

    public string PaymentMethodCode { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>Solo en <see cref="RefundTransactionTypeCode.RefundReceived"/> — queda null en la fila de reversa (§6.4quinquies).</summary>
    public string? ExternalReference { get; private set; }

    /// <summary>Solo obligatorio en <see cref="RefundTransactionTypeCode.RefundReversed"/> (§6.4quinquies).</summary>
    public string? Reason { get; private set; }

    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; private set; }

    public string FinancialDestinationCodeSnapshot { get; private set; } = null!;
    public string FinancialDestinationNameSnapshot { get; private set; } = null!;
    public string DestinationTypeCodeSnapshot { get; private set; } = null!;
    public string AccountingAccountCodeSnapshot { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Obligatorio e inmutable — único por (TenantId, ClientRequestId), §6.4/§16.2.</summary>
    public Guid ClientRequestId { get; private set; }

    /// <summary>Huella determinista del payload de la operación — obligatoria e inmutable (§6.4/§16.2).</summary>
    public string PayloadHash { get; private set; } = null!;

    private SupplierCreditRefundTransaction() { }

    /// <summary>Ingreso original del reembolso (§6.4, §6.4quater — la validación de destino/cuenta activos y bloqueados es responsabilidad de Application, Fase 8).</summary>
    public static SupplierCreditRefundTransaction CreateReceived(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        Guid supplierCreditId,
        Guid supplierCreditMovementId,
        Guid financialDestinationId,
        Guid accountingAccountId,
        string accountingAccountCodeSnapshot,
        string financialDestinationCodeSnapshot,
        string financialDestinationNameSnapshot,
        string destinationTypeCodeSnapshot,
        string paymentMethodCode,
        decimal amount,
        string currencyCode,
        DateOnly effectiveDate,
        Guid createdBy,
        Guid clientRequestId,
        string payloadHash,
        string? externalReference = null,
        Guid? cashSessionId = null,
        Guid? cashMovementId = null
    )
    {
        if (supplierCreditMovementId == Guid.Empty)
            throw new ArgumentException(
                "El movimiento de crédito que origina la transacción es obligatorio.",
                nameof(supplierCreditMovementId)
            );
        if (financialDestinationId == Guid.Empty)
            throw new ArgumentException(
                "El destino financiero es obligatorio.",
                nameof(financialDestinationId)
            );
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable es obligatoria.",
                nameof(accountingAccountId)
            );
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
            throw new ArgumentException(
                "El método de pago es obligatorio.",
                nameof(paymentMethodCode)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));
        if (clientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia de la transacción es obligatorio.",
                nameof(clientRequestId)
            );
        if (string.IsNullOrWhiteSpace(payloadHash))
            throw new ArgumentException(
                "La huella del payload de la transacción es obligatoria.",
                nameof(payloadHash)
            );

        return new SupplierCreditRefundTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            SupplierId = supplierId,
            SupplierCreditId = supplierCreditId,
            SupplierCreditMovementId = supplierCreditMovementId,
            TransactionTypeCode = RefundTransactionTypeCode.RefundReceived,
            OriginalTransactionId = null,
            FinancialDestinationId = financialDestinationId,
            AccountingAccountId = accountingAccountId,
            AccountingAccountCodeSnapshot = accountingAccountCodeSnapshot.Trim(),
            FinancialDestinationCodeSnapshot = financialDestinationCodeSnapshot.Trim(),
            FinancialDestinationNameSnapshot = financialDestinationNameSnapshot.Trim(),
            DestinationTypeCodeSnapshot = destinationTypeCodeSnapshot.Trim(),
            PaymentMethodCode = paymentMethodCode.Trim(),
            Amount = amount,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            EffectiveDate = effectiveDate,
            ExternalReference = OptionalCode.Normalize(externalReference),
            Reason = null,
            CashSessionId = cashSessionId,
            CashMovementId = cashMovementId,
            CreatedByUserId = createdBy,
            CreatedAtUtc = DateTime.UtcNow,
            ClientRequestId = clientRequestId,
            PayloadHash = payloadHash,
        };
    }

    /// <summary>
    /// Reversa append-only — hereda destino/cuenta/moneda/importe/método del ingreso original sin
    /// volver a resolverlos (§6.4quinquies). <see cref="ExternalReference"/> queda null en la fila
    /// de reversa; <paramref name="reason"/> es obligatorio.
    /// </summary>
    public static SupplierCreditRefundTransaction CreateReversal(
        SupplierCreditRefundTransaction original,
        Guid supplierCreditMovementId,
        string reason,
        DateOnly effectiveDate,
        Guid createdBy,
        Guid clientRequestId,
        string payloadHash,
        Guid? cashSessionId = null,
        Guid? cashMovementId = null
    )
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.TransactionTypeCode != RefundTransactionTypeCode.RefundReceived)
            throw new InvalidOperationException(
                "Solo se puede revertir una transacción de tipo ingreso de reembolso."
            );
        if (supplierCreditMovementId == Guid.Empty)
            throw new ArgumentException(
                "El movimiento de reversa que origina la transacción es obligatorio.",
                nameof(supplierCreditMovementId)
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo de la reversa es obligatorio.",
                nameof(reason)
            );
        if (clientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia de la reversa es obligatorio.",
                nameof(clientRequestId)
            );
        if (string.IsNullOrWhiteSpace(payloadHash))
            throw new ArgumentException(
                "La huella del payload de la reversa es obligatoria.",
                nameof(payloadHash)
            );

        return new SupplierCreditRefundTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = original.TenantId,
            CompanyId = original.CompanyId,
            SupplierId = original.SupplierId,
            SupplierCreditId = original.SupplierCreditId,
            SupplierCreditMovementId = supplierCreditMovementId,
            TransactionTypeCode = RefundTransactionTypeCode.RefundReversed,
            OriginalTransactionId = original.Id,
            FinancialDestinationId = original.FinancialDestinationId,
            AccountingAccountId = original.AccountingAccountId,
            AccountingAccountCodeSnapshot = original.AccountingAccountCodeSnapshot,
            FinancialDestinationCodeSnapshot = original.FinancialDestinationCodeSnapshot,
            FinancialDestinationNameSnapshot = original.FinancialDestinationNameSnapshot,
            DestinationTypeCodeSnapshot = original.DestinationTypeCodeSnapshot,
            PaymentMethodCode = original.PaymentMethodCode,
            Amount = original.Amount,
            CurrencyCode = original.CurrencyCode,
            EffectiveDate = effectiveDate,
            ExternalReference = null,
            Reason = reason.Trim(),
            CashSessionId = cashSessionId,
            CashMovementId = cashMovementId,
            CreatedByUserId = createdBy,
            CreatedAtUtc = DateTime.UtcNow,
            ClientRequestId = clientRequestId,
            PayloadHash = payloadHash,
        };
    }
}
