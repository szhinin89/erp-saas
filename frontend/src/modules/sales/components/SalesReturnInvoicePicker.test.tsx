// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import type { SalesListItemDto } from "../api/salesService";
import { salesService } from "../api/salesService";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { SalesReturnInvoicePicker } from "./SalesReturnInvoicePicker";

vi.mock("../api/salesService", () => ({
  salesService: {
    list: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const invoice: SalesListItemDto = {
  id: "inv-1",
  invoiceNumber: "001-001-000000123",
  issueDate: "2026-07-01",
  customerId: "cust-1",
  customerName: "Juan Pérez",
  status: "Authorized",
  lineCount: 1,
  grandTotal: 115,
  createdAt: "2026-07-01T00:00:00Z",
};

describe("SalesReturnInvoicePicker — botón limpiar selección (SALES-DS-BUTTONS-04)", () => {
  it('muestra el botón "Cambiar factura" con ícono de cerrar cuando hay una factura seleccionada', () => {
    render(<SalesReturnInvoicePicker value={invoice} onChange={() => {}} />);

    const btn = screen.getByTitle("Cambiar factura");
    expect(btn.getAttribute("aria-label")).toBe("Cambiar factura");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "close",
    );
  });

  it("click en el botón limpiar invoca onChange(null)", () => {
    const onChange = vi.fn();
    render(<SalesReturnInvoicePicker value={invoice} onChange={onChange} />);

    fireEvent.click(screen.getByTitle("Cambiar factura"));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("no renderiza el botón limpiar cuando disabled=true", () => {
    render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} disabled />,
    );

    expect(screen.queryByTitle("Cambiar factura")).toBeNull();
  });

  it("no hay estilos inline en el botón limpiar", () => {
    render(<SalesReturnInvoicePicker value={invoice} onChange={() => {}} />);

    expect(screen.getByTitle("Cambiar factura").getAttribute("style")).toBeNull();
  });
});

describe("SalesReturnInvoicePicker — valor seleccionado (ZHPickerSelectedValue, SALES-DS-PICKER-SELECTED-09)", () => {
  it("la factura seleccionada se renderiza usando ZHPickerSelectedValue", () => {
    const { container } = render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} />,
    );

    const card = container.querySelector(".zh-picker-selected-value");
    expect(card).toBeTruthy();
  });

  it("muestra invoiceNumber como title", () => {
    const { container } = render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} />,
    );

    const title = container.querySelector(".zh-picker-selected-value__title");
    expect(title?.textContent).toBe("001-001-000000123");
  });

  it("muestra customerName/fecha/total igual que antes, en el mismo formato", () => {
    const { container } = render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} />,
    );

    const meta = container.querySelector(".zh-picker-selected-value__meta");
    expect(meta?.textContent).toBe(
      `Juan Pérez — ${formatDate(invoice.issueDate)} — ${formatMoney(invoice.grandTotal)}`,
    );
  });

  it('la acción "Cambiar factura" existe con ícono close', () => {
    render(<SalesReturnInvoicePicker value={invoice} onChange={() => {}} />);

    const btn = screen.getByTitle("Cambiar factura");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "close",
    );
  });

  it('click en "Cambiar factura" limpia/quita la factura igual que antes', () => {
    const onChange = vi.fn();
    render(<SalesReturnInvoicePicker value={invoice} onChange={onChange} />);

    fireEvent.click(screen.getByTitle("Cambiar factura"));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('no usa el ícono "delete" para cambiar factura (no es un borrado)', () => {
    render(<SalesReturnInvoicePicker value={invoice} onChange={() => {}} />);

    const btn = screen.getByTitle("Cambiar factura");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).not.toBe(
      "delete",
    );
  });

  it('no agrega acción de editar ("edit") — este picker no edita la factura', () => {
    render(<SalesReturnInvoicePicker value={invoice} onChange={() => {}} />);

    const icons = Array.from(
      document.querySelectorAll(".zh-picker-selected-value .material-symbols-outlined"),
    ).map((el) => el.textContent);
    expect(icons).not.toContain("edit");
  });

  it("no queda ninguna tarjeta local duplicada (sr-invoice-picker-selected)", () => {
    const { container } = render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} />,
    );

    expect(container.querySelector(".sr-invoice-picker-selected")).toBeNull();
  });

  it("no hay estilos inline en la tarjeta de factura seleccionada", () => {
    const { container } = render(
      <SalesReturnInvoicePicker value={invoice} onChange={() => {}} />,
    );

    const card = container.querySelector(".zh-picker-selected-value")!;
    card.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
    expect(card.getAttribute("style")).toBeNull();
  });
});

const secondInvoice: SalesListItemDto = {
  id: "inv-2",
  invoiceNumber: "001-001-000000456",
  issueDate: "2026-07-10",
  customerId: "cust-2",
  customerName: "María López",
  status: "Authorized",
  lineCount: 2,
  grandTotal: 45.5,
  createdAt: "2026-07-10T00:00:00Z",
};

const oneItemResponse = {
  items: [secondInvoice],
  total: 1,
  page: 1,
  pageSize: 10,
};

describe("SalesReturnInvoicePicker — fila de resultado (ZHPickerResultItem, SALES-DS-PICKER-RESULT-06)", () => {
  it("renderiza los resultados de búsqueda usando ZHPickerResultItem", async () => {
    vi.mocked(salesService.list).mockResolvedValueOnce(oneItemResponse);

    render(<SalesReturnInvoicePicker value={null} onChange={() => {}} />);
    fireEvent.focus(
      screen.getByPlaceholderText(
        "Buscar factura autorizada por número o cliente...",
      ),
    );

    const row = await screen.findByText("001-001-000000456");
    const btn = row.closest("button")!;
    expect(btn.className.includes("zh-picker-result-item")).toBe(true);
    const meta = btn.querySelector(".zh-picker-result-item__meta");
    expect(meta?.textContent).toContain("María López");
  });

  it("click en un resultado selecciona la factura igual que antes", async () => {
    vi.mocked(salesService.list).mockResolvedValueOnce(oneItemResponse);
    const onChange = vi.fn();

    render(<SalesReturnInvoicePicker value={null} onChange={onChange} />);
    fireEvent.focus(
      screen.getByPlaceholderText(
        "Buscar factura autorizada por número o cliente...",
      ),
    );

    const row = await screen.findByText("001-001-000000456");
    fireEvent.click(row);

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ id: "inv-2", invoiceNumber: "001-001-000000456" }),
    );
  });

  it("no queda ningún <button> local de fila de resultado en SalesReturnInvoicePicker", async () => {
    vi.mocked(salesService.list).mockResolvedValueOnce(oneItemResponse);

    const { container } = render(
      <SalesReturnInvoicePicker value={null} onChange={() => {}} />,
    );
    fireEvent.focus(
      screen.getByPlaceholderText(
        "Buscar factura autorizada por número o cliente...",
      ),
    );

    await screen.findByText("001-001-000000456");

    container.querySelectorAll("button").forEach((btn) => {
      if (btn.textContent?.includes("001-001-000000456")) {
        expect(btn.className.includes("zh-picker-result-item")).toBe(true);
        expect(btn.className.includes("sr-invoice-picker__item")).toBe(false);
      }
    });
  });

  it("no hay estilos inline en la fila de resultado", async () => {
    vi.mocked(salesService.list).mockResolvedValueOnce(oneItemResponse);

    render(<SalesReturnInvoicePicker value={null} onChange={() => {}} />);
    fireEvent.focus(
      screen.getByPlaceholderText(
        "Buscar factura autorizada por número o cliente...",
      ),
    );

    const row = await screen.findByText("001-001-000000456");
    expect(row.closest("button")!.getAttribute("style")).toBeNull();
  });

  it("sin resultados sigue mostrando el mensaje de vacío (búsqueda sin cambios)", async () => {
    render(<SalesReturnInvoicePicker value={null} onChange={() => {}} />);
    fireEvent.focus(
      screen.getByPlaceholderText(
        "Buscar factura autorizada por número o cliente...",
      ),
    );

    expect(
      await screen.findByText("Sin facturas autorizadas que coincidan."),
    ).toBeTruthy();
  });
});
