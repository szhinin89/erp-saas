// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { SupplierCreditListPage } from "./SupplierCreditListPage";
import { I18nProvider } from "../../../i18n/i18n";
import type { SupplierCreditListItemDto } from "../api/supplierCreditService";

/**
 * ZH-SUPPLIER-BALANCES-UX-02D-E — listado "Saldos a favor de proveedores": nombre del proveedor
 * (nunca GUID), filtros server-side (default: abiertos), enlace al documento de origen y sin la
 * acción ajena "Cuentas bancarias".
 */

const list = vi.fn();
vi.mock("../api/supplierCreditService", () => ({
  supplierCreditService: { list: (...a: unknown[]) => list(...a) },
}));
vi.mock("../../masterData/facades/supplierPickerFacade", () => ({
  SupplierSearchSelect: ({ onChange }: { onChange: (v: { id: string } | null) => void }) => (
    <button type="button" onClick={() => onChange({ id: "sup-2" })}>
      elegir proveedor
    </button>
  ),
}));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const ROWS: SupplierCreditListItemDto[] = [
  {
    id: "cred-1",
    supplierId: "3f2c9d7e-0000-4000-9000-000000000001",
    supplierName: "Distribuidora Andina",
    sourceType: "SupplierPayment",
    sourceDocumentId: "pay-1",
    sourceDocumentNumber: "PP-000012",
    sourceDate: "2026-09-10",
    currencyCode: "USD",
    originalAmount: 200,
    availableAmount: 20,
    isOpen: true,
  },
  {
    id: "cred-2",
    supplierId: "3f2c9d7e-0000-4000-9000-000000000002",
    supplierName: "Comercial Costa",
    sourceType: "PurchaseReturn",
    sourceDocumentId: "ret-1",
    sourceDocumentNumber: "00000003",
    sourceDate: "2026-09-11",
    currencyCode: "USD",
    originalAmount: 50,
    availableAmount: 50,
    isOpen: true,
  },
];

afterEach(() => cleanup());

beforeEach(() => {
  list.mockReset().mockResolvedValue({ items: ROWS, total: 2, page: 1, pageSize: 25 });
});

function renderPage(initialEntry = "/suppliers/credits") {
  render(
    <I18nProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/suppliers/credits" element={<SupplierCreditListPage />} />
          <Route path="/suppliers/credits/:id" element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

const lastFilters = () => list.mock.calls.at(-1)![2] as Record<string, unknown>;

describe("SupplierCreditListPage (02D-E)", () => {
  it("muestra el nombre del proveedor, nunca el GUID, y el origen de negocio", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Distribuidora Andina")).toBeTruthy());
    expect(screen.getByText("Comercial Costa")).toBeTruthy();
    expect(screen.queryByText(ROWS[0]!.supplierId)).toBeNull();
    const cells = Array.from(document.querySelectorAll("td")).map((td) => td.textContent);
    expect(cells).toContain("Anticipo / pago mayor");
    expect(cells).toContain("Devolución de compra");
    expect(screen.getByText("Saldos a favor de proveedores")).toBeTruthy();
  });

  it("por defecto consulta solo saldos abiertos (server-side)", async () => {
    renderPage();
    await waitFor(() => expect(list).toHaveBeenCalled());
    expect(list.mock.calls[0]![0]).toBe(1);
    expect(list.mock.calls[0]![2]).toEqual({ supplierId: null, sourceType: null, isOpen: true });
  });

  it("filtro de proveedor, origen y estado viajan al servidor y el reset vuelve a abiertos", async () => {
    renderPage();
    await waitFor(() => expect(list).toHaveBeenCalled());

    fireEvent.click(screen.getByText("elegir proveedor"));
    await waitFor(() => expect(lastFilters()).toMatchObject({ supplierId: "sup-2" }));

    fireEvent.change(screen.getByRole("combobox", { name: "Origen" }), { target: { value: "PurchaseReturn" } });
    await waitFor(() => expect(lastFilters()).toMatchObject({ sourceType: "PurchaseReturn" }));

    fireEvent.change(screen.getByRole("combobox", { name: "Estado" }), { target: { value: "closed" } });
    await waitFor(() => expect(lastFilters()).toMatchObject({ isOpen: false }));

    fireEvent.change(screen.getByRole("combobox", { name: "Estado" }), { target: { value: "all" } });
    await waitFor(() => expect(lastFilters()).toMatchObject({ isOpen: null }));

    fireEvent.click(screen.getByText("Restablecer filtros"));
    await waitFor(() => expect(lastFilters()).toEqual({ supplierId: null, sourceType: null, isOpen: true }));
  });

  it("el documento de origen enlaza al pago a proveedor o a la devolución de compra", async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("PP-000012")).toBeTruthy());
    expect(screen.getByText("PP-000012").closest("a")!.getAttribute("href")).toBe("/supplier-payments/pay-1");
    expect(screen.getByText("00000003").closest("a")!.getAttribute("href")).toBe("/purchases/returns/ret-1");
  });

  it("ya no ofrece la acción ajena 'Cuentas bancarias'", async () => {
    renderPage();
    await waitFor(() => expect(list).toHaveBeenCalled());
    expect(screen.queryByText("Cuentas bancarias")).toBeNull();
  });
});

describe("SupplierCreditListPage — filtro por proveedor desde URL (02D-F)", () => {
  it("?supplierId inicializa el filtro server-side del proveedor (con estado abiertos)", async () => {
    renderPage("/suppliers/credits?supplierId=sup-9");
    await waitFor(() => expect(list).toHaveBeenCalled());
    expect(list.mock.calls[0]![2]).toEqual({ supplierId: "sup-9", sourceType: null, isOpen: true });
  });

  it("?applyTo se propaga al detalle al abrir un saldo", async () => {
    renderPage("/suppliers/credits?supplierId=sup-9&applyTo=payable-7");
    await waitFor(() => expect(screen.getAllByText("Ver").length).toBeGreaterThan(0));
    fireEvent.click(screen.getAllByText("Ver")[0]!);
    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe("/suppliers/credits/cred-1?applyTo=payable-7"),
    );
  });
});
