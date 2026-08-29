import { describe, it, expect } from "vitest";
import { buildWithholdingIssueMessage } from "./withholdingMessages";

describe("buildWithholdingIssueMessage — CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03", () => {
  it("incluye documento de compra, proveedor y total retenido cuando está disponible", () => {
    const msg = buildWithholdingIssueMessage(
      "001-001-000000123",
      "Proveedor Uno",
      42.5,
    );

    expect(msg).toContain("001-001-000000123");
    expect(msg).toContain("Proveedor Uno");
    expect(msg).toContain("$42.50");
  });

  it("advierte impacto tributario (SRI) y contable, y aclara el vínculo con la compra", () => {
    const msg = buildWithholdingIssueMessage("001-001-000000123", "Proveedor Uno", 10);

    expect(msg).toMatch(/SRI/);
    expect(msg).toMatch(/contable/i);
    expect(msg).toMatch(/vinculad[oa] a esta compra/i);
  });

  it("referencia el campo de punto de emisión sin asumir un valor ya seleccionado", () => {
    const msg = buildWithholdingIssueMessage("001-001-000000123", "Proveedor Uno", 10);

    expect(msg).toMatch(/punto de emisión/i);
  });

  it("omite el total retenido cuando no está disponible, sin romper el resto del mensaje", () => {
    const msg = buildWithholdingIssueMessage("001-001-000000123", "Proveedor Uno", null);

    expect(msg).not.toContain("total retenido");
    expect(msg).toContain("001-001-000000123");
    expect(msg).toContain("Proveedor Uno");
  });
});
