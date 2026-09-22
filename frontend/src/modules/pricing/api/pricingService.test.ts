import { describe, expect, it } from "vitest";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { formatRuleGeneral } from "./pricingService";

// ERP-PRECISION-FRONTEND-06B: porcentajes → percentageDecimals; precio fijo/ajuste → salesUnitPriceDecimals.
describe("formatRuleGeneral — precisión de la política de la empresa", () => {
  it("porcentajes usan percentageDecimals", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, percentageDecimals: 4 });

    expect(formatRuleGeneral("PercentDiscount", 12.5, "USD")).toBe("Descuento 12.5000%");
    expect(formatRuleGeneral("PercentMarkup", 3, "USD")).toBe("Recargo 3.0000%");
  });

  it("precio fijo y ajuste unitario usan salesUnitPriceDecimals", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 4 });

    expect(formatRuleGeneral("FixedPrice", 1.5, "USD")).toBe("Precio fijo $1.5000");
    expect(formatRuleGeneral("FixedAdjustment", 0.25, "USD")).toBe("Ajuste +0.2500");
    expect(formatRuleGeneral("FixedAdjustment", -0.25, "USD")).toBe("Ajuste -0.2500");
  });

  it("con el perfil estándar (2 decimales) y traductor, el valor sale formateado", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY });
    const translate = (key: string, params?: Record<string, string | number>) =>
      `${key}|${params?.value ?? ""}`;

    expect(formatRuleGeneral("PercentDiscount", 10, "USD", translate)).toBe("pricing.ux.rule.PercentDiscount|10.00");
    expect(formatRuleGeneral("FixedPrice", 5, "USD", translate)).toBe("pricing.ux.rule.FixedPrice|USD 5.00");
    expect(formatRuleGeneral("FixedAdjustment", 2, "USD", translate)).toBe("pricing.ux.rule.FixedAdjustment|+2.00");
    expect(formatRuleGeneral(null, null, "USD", translate)).toBe("pricing.ux.rule.none|");
  });
});
