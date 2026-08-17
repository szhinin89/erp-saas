// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { PurchaseReturnDto } from "../api/purchaseReturnService";
import { I18nProvider } from "../../../i18n/i18n";

// PURCHASES-DS-MONEY-01 — la columna "Total" del listado de devoluciones de
// compra migró de formatMoney a ZHMoneyValue (decimals=dc.totalAmount, sin
// símbolo de moneda — nunca mostró "$"), mismo patrón que
// SalesReturnListPage (SALES-DS-MONEY-12).

const listMock = vi.fn();
vi.mock("../api/purchaseReturnService", async () => {
  const actual = await vi.importActual<
    typeof import("../api/purchaseReturnService")
  >("../api/purchaseReturnService");
  return {
    ...actual,
    purchaseReturnService: {
      ...actual.purchaseReturnService,
      list: (...a: unknown[]) => listMock(...a),
    },
  };
});

import { PurchaseReturnListPage } from "./PurchaseReturnListPage";

function buildItem(overrides: Partial<PurchaseReturnDto> = {}): PurchaseReturnDto {
  return {
    id: "pr-1",
    purchaseInvoiceId: "inv-1",
    supplierId: "sup-1",
    branchId: "br-1",
    returnNumber: "DEVC-001",
    reason: "Producto defectuoso",
    status: "Authorized",
    fiscalStatus: "Linked",
    supplierCreditNoteDocumentId: null,
    authorizedSubtotal: 100,
    authorizedVatTotal: 15,
    authorizedIceTotal: 0,
    authorizedDiscountTotal: 0,
    authorizedGrandTotal: 115,
    authorizedAtUtc: "2026-07-01T00:00:00Z",
    cancelledAtUtc: null,
    cancellationReason: null,
    lines: [],
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <PurchaseReturnListPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  listMock.mockReset();
});

describe("PurchaseReturnListPage — columna Total migrada a ZHMoneyValue (PURCHASES-DS-MONEY-01)", () => {
  it("la columna Total usa ZHMoneyValue sin símbolo de moneda", async () => {
    listMock.mockResolvedValue({
      items: [buildItem({ authorizedGrandTotal: 115 })],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    const { container } = renderPage();

    await screen.findByText("DEVC-001");

    const cell = container.querySelector(
      "td.zh-text-align-right .zh-money-value",
    );
    expect(cell).toBeTruthy();
    expect(cell?.textContent).toBe("115.00");
  });

  it("sin total autorizado, muestra 0.00 (mismo comportamiento previo de `?? 0`)", async () => {
    listMock.mockResolvedValue({
      items: [buildItem({ authorizedGrandTotal: null })],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    const { container } = renderPage();

    await screen.findByText("DEVC-001");

    const cell = container.querySelector(
      "td.zh-text-align-right .zh-money-value",
    );
    expect(cell?.textContent).toBe("0.00");
  });

  it("no hay estilos inline en el valor migrado", async () => {
    listMock.mockResolvedValue({
      items: [buildItem()],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    const { container } = renderPage();

    await screen.findByText("DEVC-001");

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
