// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPicker } from "./SupplierPicker";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn().mockResolvedValue({
      id: "sup-1",
      identificationNumber: "1791415132001",
      tradeName: "",
      legalName: "Proveedor Uno S.A.",
      isActive: true,
    }),
    searchBusinessPartners: vi.fn().mockResolvedValue([]),
  },
  RoleTypeEnum: { Supplier: "Supplier" },
}));

function renderPicker(props: Partial<ComponentProps<typeof SupplierPicker>> = {}) {
  return render(
    <I18nProvider>
      <SupplierPicker value={null} onChange={() => {}} {...props} />
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SupplierPicker — valor seleccionado (ZHPickerSelectedValue)", () => {
  it("muestra el proveedor seleccionado usando ZHPickerSelectedValue", async () => {
    const { container } = renderPicker({ value: "sup-1" });

    await screen.findByText("Proveedor Uno S.A.");
    const card = container.querySelector(".zh-picker-selected-value");
    expect(card).toBeTruthy();
    expect(
      card?.querySelector(".zh-picker-selected-value__title")?.textContent,
    ).toBe("Proveedor Uno S.A.");
    expect(
      card?.querySelector(".zh-picker-selected-value__subtitle")?.textContent,
    ).toBe("1791415132001");
  });

  it('click en "Cambiar proveedor" invoca onChange(null)', async () => {
    const onChange = vi.fn();
    renderPicker({ value: "sup-1", onChange });

    const btn = await screen.findByTitle("Cambiar proveedor");
    fireEvent.click(btn);

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(null));
  });

  it("no renderiza el botón de cambiar cuando disabled=true", async () => {
    renderPicker({ value: "sup-1", disabled: true });

    await screen.findByText("Proveedor Uno S.A.");
    expect(screen.queryByTitle("Cambiar proveedor")).toBeNull();
  });

  it("muestra el badge de proveedor inactivo como meta cuando isActive=false", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValueOnce({
      id: "sup-2",
      identificationNumber: "1791415132002",
      tradeName: "",
      legalName: "Proveedor Inactivo",
      isActive: false,
    } as never);

    renderPicker({ value: "sup-2" });

    await screen.findByText("Proveedor Inactivo");
    expect(screen.getByText("Proveedor inactivo")).toBeTruthy();
  });
});

describe("SupplierPicker — fila de resultado (ZHPickerResultItem)", () => {
  it("renderiza los resultados de búsqueda usando ZHPickerResultItem", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValueOnce([
      {
        id: "sup-3",
        identificationNumber: "0992345678001",
        tradeName: "",
        legalName: "María Proveedora",
        isActive: true,
      },
    ] as never);

    renderPicker();
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María Proveedora");
    const btn = row.closest("button")!;
    expect(btn.className.includes("zh-picker-result-item")).toBe(true);
    expect(screen.getByText("0992345678001")).toBeTruthy();
  });

  it("click en un resultado selecciona al proveedor igual que antes", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValueOnce([
      {
        id: "sup-3",
        identificationNumber: "0992345678001",
        tradeName: "",
        legalName: "María Proveedora",
        isActive: true,
      },
    ] as never);
    const onChange = vi.fn();

    renderPicker({ onChange });
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María Proveedora");
    fireEvent.click(row);

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ id: "sup-3", fullName: "María Proveedora" }),
    );
  });

  it("no hay estilos inline en la fila de resultado", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartners).mockResolvedValueOnce([
      {
        id: "sup-3",
        identificationNumber: "0992345678001",
        tradeName: "",
        legalName: "María Proveedora",
        isActive: true,
      },
    ] as never);

    renderPicker();
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "María" } });

    const row = await screen.findByText("María Proveedora");
    expect(row.closest("button")!.getAttribute("style")).toBeNull();
  });
});
