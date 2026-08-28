import { describe, expect, it } from "vitest";
import { paymentService } from "./paymentService";

/**
 * PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — guard de regresión: `registerPayment` (CxP contra
 * AccountsPayable, backend `POST /api/v1/finance/payments`) se eliminó junto con
 * RegisterPaymentCommand/FinancePaymentsController — sin UI ni endpoint activo, sin
 * PagoCabecera/PagoDetalle todavía. Solo `registerCollection` (CxC, en uso real vía
 * RegisterCollectionModal.tsx) debe seguir existiendo.
 */
describe("paymentService — limpieza del flujo legacy de pago a proveedor", () => {
  it("no expone registerPayment", () => {
    expect("registerPayment" in paymentService).toBe(false);
  });

  it("sigue exponiendo registerCollection", () => {
    expect(typeof paymentService.registerCollection).toBe("function");
  });
});
