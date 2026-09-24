// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceLineGridRow } from "./SalesInvoiceLineGridRow";
import { mapInvoiceLinesToFormValues } from "../utils/salesInvoiceHydration";
import {
  setPrecisionPolicyForTests,
  type PrecisionPolicy,
} from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { SalesInvoiceDetailDto, SalesInvoiceDto } from "../api/salesService";
import type { WarehouseDto } from "../../inventory/types";

// ZH-DESIGN-SYSTEM-PRECISION-02B — "Costo al vender" (snapshot histórico unitCostAtSale) se muestra
// con la semántica unitCost de la PrecisionPolicy. Solo cambia la escala de DISPLAY: el valor
// histórico persistido no se recalcula ni se sustituye por el costo promedio actual.

afterEach(() => cleanup());

const POLICY_A: PrecisionPolicy = { ...TEST_PRECISION_POLICY, unitCostDecimals: 6 };
const POLICY_B: PrecisionPolicy = { ...TEST_PRECISION_POLICY, unitCostDecimals: 4 };
const WAREHOUSES: WarehouseDto[] = [{ id: "wh-1", name: "Bodega" } as WarehouseDto];

/** Línea autorizada tal como la persiste el backend (snapshot fiscal + costo al vender). */
const PERSISTED_LINE = {
  id: "line-1",
  itemId: "item-1",
  warehouseId: "wh-1",
  description: "MANJAR",
  quantity: 2,
  unitPrice: 0.5,
  discountPct: 0,
  vatCode: "10",
  unitCostAtSale: 0.2261,
  taxableBase: 1,
  vatAmount: 0.15,
  taxInclusiveTotal: 1.15,
} as unknown as SalesInvoiceDetailDto;

function renderAuthorizedRow() {
  const snapshot = Object.freeze({ ...PERSISTED_LINE });
  const line = Object.freeze(mapInvoiceLinesToFormValues({ lines: [snapshot] } as unknown as SalesInvoiceDto)[0]!);
  const utils = render(
    <MemoryRouter>
      <SalesInvoiceLineGridRow
        line={line}
        backendLine={snapshot}
        readOnly
        disabled={false}
        index={0}
        vatLabel="IVA 15%"
        vatRates={{ "10": 15 }}
        warehouses={WAREHOUSES}
        selectedWarehouseId="wh-1"
        onUpdate={() => {}}
        onUpdateWarehouse={() => {}}
        onRemove={() => {}}
      />
    </MemoryRouter>,
  );
  const cost = () => utils.container.querySelector(".zh-money-value.sf-product__stock-wh");
  const fiscal = () =>
    [...utils.container.querySelectorAll(".sf-product__subtotal-value, .sf-product__total-amount")].map(
      (el) => el.textContent,
    );
  return { ...utils, line, snapshot, cost, fiscal };
}

describe("Ventas — Costo al vender con precision=\"unitCost\" (02B)", () => {
  it("policy unitCost=6: 0.2261 → $0.226100 (antes caía al default visual de 2)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { cost } = renderAuthorizedRow();
    expect(cost()?.textContent).toBe("$0.226100");
  });

  it("cambio de policy 6 → 4 sin remount: $0.226100 → $0.2261; el valor histórico no cambia", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { cost, line, snapshot, fiscal } = renderAuthorizedRow();
    const node = cost();
    const fiscalBefore = fiscal();
    expect(node?.textContent).toBe("$0.226100");

    act(() => setPrecisionPolicyForTests(POLICY_B));

    expect(cost()).toBe(node);
    expect(node?.textContent).toBe("$0.2261");
    // Solo display: línea hidratada, snapshot backend y Base/IVA/Total intactos.
    expect(line._unitCostAtSale).toBe(0.2261);
    expect(snapshot).toEqual(PERSISTED_LINE);
    expect(fiscal()).toEqual(fiscalBefore);
    expect(fiscalBefore).toEqual(["$1.00", "$0.15", "$1.15"]);
  });
});
