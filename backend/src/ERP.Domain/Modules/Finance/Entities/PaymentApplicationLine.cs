using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// Línea de aplicación de un <see cref="Payment"/> contra un documento de AR/AP (Fase 5.5.5.2) —
/// entidad hija del mismo aggregate, sin repositorio propio (mismo criterio que
/// <c>JournalEntryLine</c> frente a <c>JournalEntry</c>). Exactamente uno de
/// <see cref="ReceivableId"/>/<see cref="PayableId"/> tiene valor, según la
/// <see cref="Enums.PaymentDirection"/> del <see cref="Payment"/> propietario — <c>Create()</c> es
/// público (como el resto de las entidades hija de este ERP, p. ej. <c>PurchaseInvoiceDetail</c>),
/// pero solo <see cref="Payment.AddApplicationLine"/> decide cuál de los dos IDs corresponde.
/// </summary>
public sealed class PaymentApplicationLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentId { get; private set; }

    /// <summary>Cuenta por cobrar aplicada — no nulo únicamente cuando el pago es de dirección Collection.</summary>
    public Guid? ReceivableId { get; private set; }

    /// <summary>Cuenta por pagar aplicada — no nulo únicamente cuando el pago es de dirección Payment.</summary>
    public Guid? PayableId { get; private set; }

    /// <summary>
    /// Cuota específica de la CxC/CxP a la que se aplica el monto — opcional: un cobro/pago puede
    /// aplicarse al documento completo sin distinguir cuota (queda nulo en ese caso).
    /// </summary>
    public Guid? InstallmentId { get; private set; }

    public decimal AppliedAmount { get; private set; }
    public short SortOrder { get; private set; }

    private PaymentApplicationLine() { }

    /// <summary>Usar únicamente desde <see cref="Payment.AddApplicationLine"/> — valida la dirección del pago propietario.</summary>
    internal static PaymentApplicationLine CreateForReceivable(
        Guid paymentId,
        Guid tenantId,
        Guid receivableId,
        Guid? installmentId,
        decimal appliedAmount,
        short sortOrder
    )
    {
        if (receivableId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta por cobrar es obligatoria.",
                nameof(receivableId)
            );

        return Create(
            paymentId,
            tenantId,
            receivableId,
            null,
            installmentId,
            appliedAmount,
            sortOrder
        );
    }

    /// <summary>Usar únicamente desde <see cref="Payment.AddApplicationLine"/> — valida la dirección del pago propietario.</summary>
    internal static PaymentApplicationLine CreateForPayable(
        Guid paymentId,
        Guid tenantId,
        Guid payableId,
        Guid? installmentId,
        decimal appliedAmount,
        short sortOrder
    )
    {
        if (payableId == Guid.Empty)
            throw new ArgumentException("La cuenta por pagar es obligatoria.", nameof(payableId));

        return Create(
            paymentId,
            tenantId,
            null,
            payableId,
            installmentId,
            appliedAmount,
            sortOrder
        );
    }

    private static PaymentApplicationLine Create(
        Guid paymentId,
        Guid tenantId,
        Guid? receivableId,
        Guid? payableId,
        Guid? installmentId,
        decimal appliedAmount,
        short sortOrder
    )
    {
        if (appliedAmount <= 0)
            throw new ArgumentException(
                "El monto aplicado debe ser mayor a cero.",
                nameof(appliedAmount)
            );

        return new PaymentApplicationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceivableId = receivableId,
            PayableId = payableId,
            InstallmentId = installmentId,
            AppliedAmount = appliedAmount,
            SortOrder = sortOrder,
        };
    }
}
