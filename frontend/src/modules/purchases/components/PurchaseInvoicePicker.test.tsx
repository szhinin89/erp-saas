// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseInvoicePicker } from "./PurchaseInvoicePicker";
import { purchaseService } from "../api/purchaseService";

vi.mock("../api/purchaseService", () => ({
  purchaseService: { list: vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 }) },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("PurchaseInvoicePicker", () => {
  it("busca solo facturas Confirmed del proveedor dado", async () => {
    render(
      <I18nProvider>
        <PurchaseInvoicePicker supplierId="supplier-1" value={null} onChange={() => {}} />
      </I18nProvider>,
    );

    const input = screen.getByPlaceholderText("Buscar factura confirmada por número...");
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "001" } });

    await waitFor(() =>
      expect(purchaseService.list).toHaveBeenCalledWith("001", "Confirmed", 1, 10, "supplier-1"),
    );
  });

  it("muestra los resultados y selecciona al hacer click", async () => {
    vi.mocked(purchaseService.list).mockResolvedValueOnce({
      items: [
        { id: "inv-1", invoiceNumber: "001-001-000000001", issueDate: "2026-09-01",
          supplierId: "supplier-1", status: "Confirmed", lineCount: 2, createdAt: "2026-09-01" },
      ],
      total: 1, page: 1, pageSize: 10,
    });
    const onChange = vi.fn();

    render(
      <I18nProvider>
        <PurchaseInvoicePicker supplierId="supplier-1" value={null} onChange={onChange} />
      </I18nProvider>,
    );

    fireEvent.focus(screen.getByPlaceholderText("Buscar factura confirmada por número..."));

    const row = await screen.findByText("001-001-000000001");
    fireEvent.click(row);

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ id: "inv-1", invoiceNumber: "001-001-000000001" }),
    );
  });

  it("muestra el valor seleccionado y permite cambiarlo", async () => {
    const onChange = vi.fn();
    render(
      <I18nProvider>
        <PurchaseInvoicePicker
          supplierId="supplier-1"
          value={{ id: "inv-1", invoiceNumber: "001-001-000000001", issueDate: "2026-09-01",
            supplierId: "supplier-1", status: "Confirmed", lineCount: 2, createdAt: "2026-09-01" }}
          onChange={onChange}
        />
      </I18nProvider>,
    );

    expect(screen.getByText("001-001-000000001")).toBeTruthy();
    fireEvent.click(screen.getByTitle("Cambiar factura"));
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
