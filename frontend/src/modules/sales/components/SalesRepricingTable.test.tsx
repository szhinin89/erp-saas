// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { SalesRepricingTable } from "./SalesRepricingTable";
import type { RepricingRow } from "../hooks/useSalesCustomerRepricing";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04E — la tabla de re-precio ya no recibe `decimals` por prop: ambos
 * precios declaran `precision="salesUnitPrice"` y reaccionan a la policy sin remount.
 */

afterEach(() => cleanup());

const row = {
  key: 1,
  itemId: "item-1",
  description: "Producto X",
  currentUnitPrice: 12.3456,
  fields: { unitPrice: 10.5 },
} as unknown as RepricingRow;

describe("SalesRepricingTable — salesUnitPrice semántico (04E)", () => {
  it("precio actual/nuevo usan salesUnitPriceDecimals y reaccionan A → B", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 2, moneyDecimals: 2 });
    const { container } = render(<SalesRepricingTable rows={[row]} />);
    const read = () => Array.from(container.querySelectorAll(".zh-money-value")).map((e) => e.textContent);
    expect(read()).toEqual(["$12.35", "$10.50"]);

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 4, moneyDecimals: 2 }));
    expect(read()).toEqual(["$12.3456", "$10.5000"]);
  });
});
