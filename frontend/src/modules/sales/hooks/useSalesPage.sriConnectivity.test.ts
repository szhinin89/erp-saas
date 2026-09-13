import { describe, it, expect } from "vitest";
import {
  shouldWarnSriUnavailable,
  SRI_UNAVAILABLE_ISSUE_WARNING,
} from "./useSalesPage";
import type { ElectronicInvoicingStatusDto } from "../../configuracion/facturacionElectronica/api/electronicInvoicingService";

// ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: shouldWarnSriUnavailable es la única
// regla que decide si confirmIssue debe advertir al cajero antes de emitir — pura, no hace
// fetch ni recalcula el campo, solo lee sriAvailability ya resuelto por el backend.

function status(
  overrides: Partial<ElectronicInvoicingStatusDto> = {},
): ElectronicInvoicingStatusDto {
  return {
    status: "Ready",
    configured: true,
    environment: "Production",
    environmentName: "Producción",
    emissionType: "Normal",
    certificateInstalled: true,
    certificateValid: true,
    certificateExpiresAt: null,
    certificateDaysRemaining: null,
    sriAvailability: "Unknown",
    canIssue: true,
    ...overrides,
  };
}

describe("shouldWarnSriUnavailable", () => {
  it("true cuando sriAvailability es Unavailable", () => {
    expect(shouldWarnSriUnavailable(status({ sriAvailability: "Unavailable" }))).toBe(
      true,
    );
  });

  it("false cuando sriAvailability es Available", () => {
    expect(shouldWarnSriUnavailable(status({ sriAvailability: "Available" }))).toBe(
      false,
    );
  });

  it("false cuando sriAvailability es Unknown (no verificado — no se asume que falló)", () => {
    expect(shouldWarnSriUnavailable(status({ sriAvailability: "Unknown" }))).toBe(
      false,
    );
  });

  it("false cuando el status todavía no cargó (null)", () => {
    expect(shouldWarnSriUnavailable(null)).toBe(false);
  });
});

describe("SRI_UNAVAILABLE_ISSUE_WARNING", () => {
  it("es el mensaje exacto pedido: claro, sin bloquear la emisión", () => {
    expect(SRI_UNAVAILABLE_ISSUE_WARNING).toBe(
      "No se pudo conectar con el SRI. Intente nuevamente o revise la configuración.",
    );
  });
});
