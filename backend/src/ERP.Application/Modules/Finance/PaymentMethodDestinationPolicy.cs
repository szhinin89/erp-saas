using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Finance;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — regla medio ↔ destino con
/// <see cref="PaymentMethod"/> como SSOT: medio de crédito prohibido; efectivo físico
/// (<see cref="PaymentMethod.AffectsPhysicalCash"/>) ⇒ caja (banco prohibido); cualquier otro medio ⇒
/// cuenta bancaria (caja prohibida). ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B la extrae de
/// <c>RegisterSupplierPaymentHandler</c> para compartirla con el reembolso de <c>SupplierCredit</c>
/// (mismo dinero real entrando/saliendo por caja o banco) sin duplicarla. Devuelve el mensaje de
/// rechazo o <c>null</c> si la combinación es coherente.
/// </summary>
internal static class PaymentMethodDestinationPolicy
{
    public static string? Validate(PaymentMethod method, bool hasBankAccount, bool hasCashRegister)
    {
        if (method.IsCreditAllowed)
            return $"El medio de pago {method.Name} es de crédito y no puede usarse para pagar a un proveedor.";

        if (method.AffectsPhysicalCash)
            return hasBankAccount || !hasCashRegister
                ? $"El medio de pago {method.Name} mueve efectivo físico: el destino debe ser una caja, no una cuenta bancaria."
                : null;

        return hasCashRegister || !hasBankAccount
            ? $"El medio de pago {method.Name} es bancario: el destino debe ser una cuenta bancaria, no una caja."
            : null;
    }
}
