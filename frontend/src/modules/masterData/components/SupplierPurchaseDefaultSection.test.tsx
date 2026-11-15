// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { paymentTermService } from "../api/paymentTermService";
import { SupplierPurchaseDefaultSection } from "./SupplierPurchaseDefaultSection";

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getPurchaseSettings: vi.fn(),
    upsertPurchaseSettings: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({
  paymentTermService: { list: vi.fn() },
}));

describe("SupplierPurchaseDefaultSection", () => {
  beforeEach(() => {
    vi.mocked(paymentTermService.list).mockResolvedValue([
      { id: "pt1", code: "30D", name: "30 días", summary: "30d", isActive: true },
    ] as never);
    vi.mocked(businessPartnerFacade.getPurchaseSettings)
      .mockReset()
      .mockResolvedValue({ paymentTermId: null } as never);
    vi.mocked(businessPartnerFacade.upsertPurchaseSettings)
      .mockReset()
      .mockResolvedValue({ paymentTermId: "pt1" } as never);
  });
  afterEach(cleanup);

  it("guarda solo la condición de compras/gastos, de forma independiente", async () => {
    render(<SupplierPurchaseDefaultSection bpId="bp1" />);
    await screen.findByText(/30D — 30 días/);
    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "pt1" },
    });
    fireEvent.click(
      screen.getByRole("button", {
        name: "Guardar condición de compras/gastos",
      }),
    );
    await waitFor(() =>
      expect(businessPartnerFacade.upsertPurchaseSettings).toHaveBeenCalledWith(
        "bp1",
        { paymentTermId: "pt1" },
      ),
    );
    expect(await screen.findByText("Guardado ✓")).toBeTruthy();
  });

  it("un fallo de carga muestra su propio error", async () => {
    vi.mocked(businessPartnerFacade.getPurchaseSettings).mockRejectedValue(
      new Error("boom"),
    );
    render(<SupplierPurchaseDefaultSection bpId="bp1" />);
    expect(
      await screen.findByText(/No se pudo cargar la condición|boom/),
    ).toBeTruthy();
  });
});
