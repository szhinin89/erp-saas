import { describe, expect, it } from "vitest";
import { formatRuleGeneral } from "./pricingService";

// ERP-PRECISION-FRONTEND-06B / ZH-DESIGN-SYSTEM-PRECISION-06: porcentajes → percentageDecimals; precio
// fijo/ajuste → salesUnitPriceDecimals. Las escalas llegan del caller (resueltas con
// usePrecisionDecimals); la utilidad no lee la policy.
describe("formatRuleGeneral — escalas semánticas recibidas", () => {
  it("porcentajes usan percentageDecimals", () => {
    const scales = { percentageDecimals: 4, salesUnitPriceDecimals: 2 };

    expect(formatRuleGeneral("PercentDiscount", 12.5, "USD", scales)).toBe("Descuento 12.5000%");
    expect(formatRuleGeneral("PercentMarkup", 3, "USD", scales)).toBe("Recargo 3.0000%");
  });

  it("precio fijo y ajuste unitario usan salesUnitPriceDecimals", () => {
    const scales = { percentageDecimals: 2, salesUnitPriceDecimals: 4 };

    expect(formatRuleGeneral("FixedPrice", 1.5, "USD", scales)).toBe("Precio fijo $1.5000");
    expect(formatRuleGeneral("FixedAdjustment", 0.25, "USD", scales)).toBe("Ajuste +0.2500");
    expect(formatRuleGeneral("FixedAdjustment", -0.25, "USD", scales)).toBe("Ajuste -0.2500");
  });

  it("con escalas estándar (2 decimales) y traductor, el valor sale formateado", () => {
    const scales = { percentageDecimals: 2, salesUnitPriceDecimals: 2 };
    const translate = (key: string, params?: Record<string, string | number>) =>
      `${key}|${params?.value ?? ""}`;

    expect(formatRuleGeneral("PercentDiscount", 10, "USD", scales, translate)).toBe("pricing.ux.rule.PercentDiscount|10.00");
    expect(formatRuleGeneral("FixedPrice", 5, "USD", scales, translate)).toBe("pricing.ux.rule.FixedPrice|USD 5.00");
    expect(formatRuleGeneral("FixedAdjustment", 2, "USD", scales, translate)).toBe("pricing.ux.rule.FixedAdjustment|+2.00");
    expect(formatRuleGeneral(null, null, "USD", scales, translate)).toBe("pricing.ux.rule.none|");
  });
});
