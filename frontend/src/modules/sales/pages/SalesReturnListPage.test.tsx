// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesReturnListItemDto } from "../api/salesReturnService";
import { I18nProvider } from "../../../i18n/i18n";

// SALES-DS-MONEY-12 — la columna "Total" del listado de devoluciones migró
// de formatMoney a ZHMoneyValue (decimals=dc.totalAmount, sin símbolo de
// moneda — nunca mostró "$").

const listMock = vi.fn();
vi.mock("../api/salesReturnService", async () => {
  const actual = await vi.importActual<
    typeof import("../api/salesReturnService")
  >("../api/salesReturnService");
  return {
    ...actual,
    salesReturnService: {
      ...actual.salesReturnService,
      list: (...a: unknown[]) => listMock(...a),
    },
  };
});

import { SalesReturnListPage } from "./SalesReturnListPage";

function buildItem(
  overrides: Partial<SalesReturnListItemDto> = {},
): SalesReturnListItemDto {
  return {
    id: "sr-1",
    returnNumber: "DEV-001",
    salesInvoiceId: "inv-1",
    customerId: "cust-1",
    status: "Authorized",
    lineCount: 2,
    grandTotal: 115,
    createdAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <SalesReturnListPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  listMock.mockReset();
});

describe("SalesReturnListPage — columna Total migrada a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it("la columna Total usa ZHMoneyValue sin símbolo de moneda", async () => {
    listMock.mockResolvedValue({
      items: [buildItem({ grandTotal: 115 })],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    const { container } = renderPage();

    await screen.findByText("DEV-001");

    const cell = container.querySelector(
      "td.zh-text-align-right .zh-money-value",
    );
    expect(cell).toBeTruthy();
    expect(cell?.textContent).toBe("115.00");
  });

  it("no hay estilos inline en el valor migrado", async () => {
    listMock.mockResolvedValue({
      items: [buildItem()],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    const { container } = renderPage();

    await screen.findByText("DEV-001");

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
