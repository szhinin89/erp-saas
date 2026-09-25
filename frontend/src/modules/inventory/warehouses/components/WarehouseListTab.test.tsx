// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, render } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { WarehouseListadoTab } from "./WarehouseListTab";
import type { WarehouseDto } from "../api/warehouseService";
import { setPrecisionPolicyForTests } from "../../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04E — el listado muestra la capacidad (m³) con la escala contractual
 * WAREHOUSE_CAPACITY_DECIMALS (numeric(18,4)), la misma del input: sin el `toFixed(2)` anterior que
 * truncaba visualmente 100.1234 → 100.12. No es dato de la PrecisionPolicy: no reacciona a ella.
 */

afterEach(() => cleanup());

const warehouse: WarehouseDto = {
  id: "wh-1",
  branchId: "br-1",
  name: "Bodega Central",
  code: "B01",
  storageType: null,
  address: null,
  phone: null,
  email: null,
  manager: null,
  latitude: null,
  longitude: null,
  capacity: 100.1234,
  dailyDispatchGoal: null,
  isActive: true,
};

function renderList() {
  return render(
    <I18nProvider>
      <WarehouseListadoTab
        warehouses={[warehouse]}
        loading={false}
        toggling={false}
        canUpdate={false}
        canDelete={false}
        branchName={() => "Matriz"}
        onEdit={async () => {}}
        onToggle={async () => {}}
      />
    </I18nProvider>,
  );
}

describe("WarehouseListadoTab — capacidad con override contractual de 4 decimales (04E)", () => {
  it("muestra 100.1234 m³ (no 100.12) y no cambia con la PrecisionPolicy", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 2, moneyDecimals: 2 });
    const { container } = renderList();
    expect(container.textContent).toContain("100.1234 m³");
    expect(container.textContent).not.toContain("100.12 m³");
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 6, moneyDecimals: 4 }));
    expect(container.textContent).toContain("100.1234 m³");
  });
});
