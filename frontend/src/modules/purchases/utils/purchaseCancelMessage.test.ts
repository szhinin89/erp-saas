import { describe, expect, it } from "vitest";
import es from "../../../i18n/locales/es.json";
import { buildPurchaseCancelMessage } from "./purchaseCancelMessage";

const dict = es as Record<string, string>;
const t = (key: string, params?: unknown) => {
  const text = dict[key] ?? (typeof params === "string" ? params : key);
  return params && typeof params === "object"
    ? text.replace(/\{\{(\w+)\}\}/g, (_, k: string) => String((params as Record<string, unknown>)[k]))
    : text;
};

describe("buildPurchaseCancelMessage (P2)", () => {
  it("states local effects and that the supplier invoice is not annulled at SRI", () => {
    const message = buildPurchaseCancelMessage(t, "001-001-000000123", null);
    expect(message).toContain("001-001-000000123");
    expect(message).toContain("revertirá stock y cuentas por pagar");
    expect(message).toContain("no anula ante el SRI la factura del proveedor");
    expect(message).not.toContain("retención");
  });

  it("adds the retention consequence once when an unauthorized retention was issued", () => {
    const message = buildPurchaseCancelMessage(t, "001-001-000000123",
      { status: "Issued", electronicStatus: "Pending", retentionNumber: "001-002-000000045" });
    expect(message).toContain("no anula ante el SRI la factura del proveedor");
    expect(message).toContain("También se anulará la retención emitida");
    expect(message).toContain("(001-002-000000045)");
    expect(message.match(/retención/g)).toHaveLength(2);
  });

  it("with an SRI-authorized retention shows only the SRI requirement, not local effects", () => {
    const message = buildPurchaseCancelMessage(t, "001-001-000000123",
      { status: "Issued", electronicStatus: "Authorized", retentionNumber: "001-002-000000045" });
    expect(message).toContain("primero debe anularse la retención ante el SRI");
    expect(message).toContain("(001-002-000000045)");
    expect(message).not.toContain("revertirá");
  });
});
