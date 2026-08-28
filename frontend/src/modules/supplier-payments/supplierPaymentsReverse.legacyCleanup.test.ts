import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { supplierPaymentService } from "./api/supplierPaymentService";

/**
 * SUPPLIER-PAYMENTS-REVERSE-FRONTEND-16C — guard de regresión: la acción de reversa es exclusiva
 * del módulo independiente `supplier-payments` (`supplierPaymentService.reverse`,
 * `POST /api/v1/supplier-payments/{id}/reverse`) — nunca debe reintroducir el flujo legacy de CxP
 * (`RegisterPaymentModal`, `paymentService.registerPayment`, `/finance/payables`).
 */
describe("supplier-payments — sin flujo legacy de pago a proveedor", () => {
  it("supplierPaymentService expone reverse pero nunca registerPayment", () => {
    expect(typeof supplierPaymentService.reverse).toBe("function");
    expect("registerPayment" in supplierPaymentService).toBe(false);
  });

  it("SupplierPaymentDetailPage no referencia RegisterPaymentModal ni paymentService.registerPayment", () => {
    const source = readFileSync(
      new URL("./pages/SupplierPaymentDetailPage.tsx", import.meta.url),
      "utf8",
    );

    expect(source).not.toMatch(/RegisterPaymentModal/);
    expect(source).not.toMatch(/paymentService\.registerPayment/);
    expect(source).not.toMatch(/\/finance\/payables/);
  });

  it("SupplierPaymentReverseModal no referencia RegisterPaymentModal ni el flujo legacy", () => {
    const source = readFileSync(
      new URL("./components/SupplierPaymentReverseModal.tsx", import.meta.url),
      "utf8",
    );

    expect(source).not.toMatch(/RegisterPaymentModal/);
    expect(source).not.toMatch(/paymentService/);
    expect(source).not.toMatch(/\/finance\/payables/);
  });
});
