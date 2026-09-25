// @vitest-environment jsdom
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../../test/precisionPolicyFixture";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03E — piloto: la cantidad transferida declara `precision="quantity"`
 * (antes leía quantityDecimals de getPrecisionPolicy()). Reglas de transferencia fuera de alcance:
 * el hook de la página se sustituye por un contexto mínimo con una línea.
 */

const updateLineQuantity = vi.fn();
const mockLine = { quantity: 12.5 };

vi.mock("../hooks/useStockTransferPage", () => ({
  useStockTransferPage: () => ({
    transfer: null,
    isDraft: true,
    formLocked: false,
    warehouses: [],
    sourceWarehouseId: "wh-1",
    targetWarehouseId: "wh-2",
    sourceWarehouse: null,
    targetWarehouse: null,
    sameWarehouse: false,
    branchNameForWarehouse: () => "",
    lines: [{ _key: 1, sku: "ARZ", name: "Arroz", quantity: mockLine.quantity, availableAtSource: null }],
    totalUnits: 12.5,
    reason: "",
    notes: "",
    error: null,
    successMessage: null,
    creating: false,
    confirming: false,
    addLine: vi.fn(),
    removeLine: vi.fn(),
    updateLineQuantity,
    setReason: vi.fn(),
    setNotes: vi.fn(),
    setSourceWarehouseId: vi.fn(),
    setTargetWarehouseId: vi.fn(),
    createTransfer: vi.fn(),
    confirmTransfer: vi.fn(),
    resetForm: vi.fn(),
  }),
}));
vi.mock("../components/TransferProductPicker", () => ({ TransferProductPicker: () => null }));
vi.mock("../../../../components/zh/inputs/ZhWarehouseSelector", () => ({ ZhWarehouseSelector: () => null }));
vi.mock("../../../../templates/ErpPageTemplate", () => ({
  ErpPageTemplate: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

const { StockTransferPage } = await import("./StockTransferPage");

afterEach(() => {
  cleanup();
  updateLineQuantity.mockReset();
  mockLine.quantity = 12.5;
});

function renderPage() {
  const { container } = render(
    <MemoryRouter>
      <I18nProvider>
        <StockTransferPage />
      </I18nProvider>
    </MemoryRouter>,
  );
  return container.querySelector<HTMLInputElement>(".itf-line__qty input")!;
}

describe("StockTransferPage — cantidad con precision='quantity' (03E)", () => {
  it.each([
    [4, "12.5000"],
    [2, "12.50"],
  ])("quantityDecimals=%s → '%s'", (quantityDecimals, expected) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals });
    expect(renderPage().value).toBe(expected);
  });

  it("edición real → updateLineQuantity con el mismo contrato (Number del texto normalizado)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 });
    const qty = renderPage();
    fireEvent.focus(qty);
    fireEvent.change(qty, { target: { value: "3.25" } });
    fireEvent.blur(qty);
    expect(qty.value).toBe("3.2500");
    expect(updateLineQuantity).toHaveBeenLastCalledWith(1, 3.25);
  });

  // 04A: antes el onBlur del consumidor confirmaba el mismo valor; ahora la guarda local lo evita.
  it("foco/blur sin editar: el input no reescribe ni se llama updateLineQuantity (04A)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 });
    const qty = renderPage();
    fireEvent.focus(qty);
    fireEvent.blur(qty);
    expect(qty.value).toBe("12.5000");
    expect(updateLineQuantity).not.toHaveBeenCalled();
  });
});

describe("StockTransferPage — commit solo tras edición real (04A1)", () => {
  it("cantidad almacenada 12.34567 (quantity=4 → '12.3457'): foco/blur sin editar → updateLineQuantity 0; editar → commit", () => {
    mockLine.quantity = 12.34567;
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 });
    const qty = renderPage();
    expect(qty.value).toBe("12.3457");
    fireEvent.focus(qty);
    fireEvent.blur(qty);
    expect(updateLineQuantity).not.toHaveBeenCalled();
    fireEvent.focus(qty);
    fireEvent.change(qty, { target: { value: "12.3456" } });
    fireEvent.blur(qty);
    expect(updateLineQuantity).toHaveBeenCalledTimes(1);
    expect(updateLineQuantity).toHaveBeenCalledWith(1, 12.3456);
  });
});
