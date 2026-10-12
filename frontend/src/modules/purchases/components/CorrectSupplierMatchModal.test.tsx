// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CorrectSupplierMatchModal } from "./CorrectSupplierMatchModal";

const mocks = vi.hoisted(() => ({ getById: vi.fn() }));
vi.mock("../../items/facades/itemLookupFacade", () => ({ itemLookupFacade: { getById: mocks.getById } }));
vi.mock("./ProductPicker", () => ({
  ProductPicker: ({ onSelect }: { onSelect: (p: { id: string; label: string }) => void }) => (
    <button type="button" onClick={() => onSelect({ id: "C", label: "C-1 — Correcto" })}>pick</button>
  ),
}));
vi.mock("../../../i18n/i18n", () => ({
  useI18n: () => ({ t: (key: string, fallback?: unknown) => (typeof fallback === "string" ? fallback : key) }),
}));
afterEach(cleanup);

const props = (onSubmit = vi.fn().mockResolvedValue(null)) => ({
  open: true, supplierName: "Proveedor", supplierCode: "COD-A", xmlDescription: "XML A",
  currentLabel: "B-1 — Equivocado", onClose: vi.fn(), onSubmit,
});

describe("correct supplier match modal", () => {
  it("submits the chosen product with its supplier presentation preselected", async () => {
    mocks.getById.mockResolvedValue({ packagingLevels: [
      { id: "unit", name: "Unidad", baseQuantity: 1, isActive: true, isBaseUnit: true, isPurchaseDefault: false },
      { id: "box", name: "Caja x12", baseQuantity: 12, isActive: true, isBaseUnit: false, isPurchaseDefault: true },
    ] });
    const p = props();
    render(<CorrectSupplierMatchModal {...p} />);
    fireEvent.click(screen.getByText("Corregir relación"));
    await waitFor(() => expect(screen.getByText("Seleccione el producto correcto.")).toBeTruthy());
    expect(p.onSubmit).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText("pick"));
    await waitFor(() => expect((document.querySelector('[name="packagingLevelId"]') as HTMLSelectElement).value).toBe("box"));
    fireEvent.click(screen.getByText("Corregir relación"));
    await waitFor(() => expect(p.onSubmit).toHaveBeenCalledWith("C", "box"));
    expect(p.onClose).toHaveBeenCalledOnce();
  });

  it("keeps the dialog open with the server message when the correction is rejected", async () => {
    mocks.getById.mockResolvedValue({ packagingLevels: [] });
    const p = props(vi.fn().mockResolvedValue("La compra de este comprobante ya está confirmada"));
    render(<CorrectSupplierMatchModal {...p} />);
    fireEvent.click(screen.getByText("pick"));
    await waitFor(() => expect(screen.getByText("C-1 — Correcto")).toBeTruthy());
    fireEvent.click(screen.getByText("Corregir relación"));
    await waitFor(() => expect(screen.getByText("La compra de este comprobante ya está confirmada")).toBeTruthy());
    expect(p.onSubmit).toHaveBeenCalledWith("C", null);
    expect(p.onClose).not.toHaveBeenCalled();
  });
});
