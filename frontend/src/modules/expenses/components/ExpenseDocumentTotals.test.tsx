// @vitest-environment jsdom
import type { ReactElement } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render as rtlRender, type RenderOptions } from "@testing-library/react";
import { ExpenseDocumentTotals } from "./ExpenseDocumentTotals";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { I18nProvider } from "../../../i18n/i18n";

// F-03: los componentes del formulario de gastos leen sus textos de i18n.
const render = (ui: ReactElement, options?: Omit<RenderOptions, "wrapper">) =>
  rtlRender(ui, { wrapper: I18nProvider, ...options });

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04E — los totales del gasto declaran semántica (`precision="money"`,
 * IVA `precision="tax"`) en vez de leer la policy en render: reaccionan A → B sin remount y
 * conservan el símbolo "$" que ya mostraban.
 */

afterEach(() => cleanup());

const totals = { subtotal: 100.5, totalDiscount: 1.25, totalTax: 15.075, grandTotal: 114.325 };

function texts(container: HTMLElement) {
  return Array.from(container.querySelectorAll(".zh-money-value")).map((e) => e.textContent);
}

describe("ExpenseDocumentTotals — money/tax semánticos y reactivos (04E)", () => {
  it("money y tax reaccionan A → B sin remount, con símbolo $ preservado", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2, taxDecimals: 2 });
    const { container } = render(<ExpenseDocumentTotals totals={totals} />);
    expect(texts(container)).toEqual(["$100.50", "$1.25", "$15.08", "$114.33"]);

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3, taxDecimals: 4 }));
    expect(texts(container)).toEqual(["$100.500", "$1.250", "$15.0750", "$114.325"]);
  });
});
