namespace ERP.Domain.Modules.Payables.Enums;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — por qué se revierte documentalmente una
/// fuente bancaria de un <see cref="Entities.SupplierPayment"/>. Todas las opciones significan lo
/// mismo en lo esencial: la transferencia NUNCA llegó a ejecutarse (o nunca debió registrarse) —
/// la reversa es una corrección documental, no la devolución de dinero ya transferido (eso será un
/// futuro <c>SupplierPaymentRefund</c>). Estado interno fijo (no catálogo configurable); persistido
/// como int — extensión únicamente con valores nuevos al final.
/// </summary>
public enum SupplierPaymentBankReversalReason
{
    /// <summary>La transferencia se registró pero no llegó a ejecutarse.</summary>
    NotExecuted = 1,

    /// <summary>El banco rechazó la transferencia (no hubo débito efectivo).</summary>
    RejectedByBank = 2,

    /// <summary>El pago se registró por error (monto, proveedor o cuenta equivocados) y no hubo débito real.</summary>
    RegistrationError = 3,
}
