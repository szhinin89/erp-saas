import type { SupplierPaymentBankReversalReason } from "../api/supplierPaymentService";

/**
 * ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — etiquetas de UI del enum interno fijo
 * `SupplierPaymentBankReversalReason` (no es un catálogo configurable: sus valores los define el
 * backend). Todas significan que la transferencia NUNCA se debitó — la reversa es una corrección
 * documental, no una devolución de fondos.
 */
export const SUPPLIER_PAYMENT_BANK_REVERSAL_REASONS: ReadonlyArray<{
  value: SupplierPaymentBankReversalReason;
  label: string;
}> = [
  { value: "NotExecuted", label: "La transferencia no llegó a ejecutarse" },
  { value: "RejectedByBank", label: "El banco rechazó la transferencia" },
  { value: "RegistrationError", label: "Error de registro (no hubo débito real)" },
];
