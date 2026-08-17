// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import type { PurchaseCreditNoteDto } from "../api/purchaseCreditNoteService";
import { I18nProvider } from "../../../i18n/i18n";

// PURCHASES-DS-MONEY-02 — los valores de solo lectura (saldo pendiente, total
// crédito, reduce CxP, resúmenes fiscales y líneas) migraron de formatMoney a
// ZHMoneyValue; ninguno mostraba "$" antes, así que se usa currencySymbol="".

const getByIdMock = vi.fn();
vi.mock("../api/purchaseCreditNoteService", async () => {
  const actual = await vi.importActual<
    typeof import("../api/purchaseCreditNoteService")
  >("../api/purchaseCreditNoteService");
  return {
    ...actual,
    purchaseCreditNoteService: {
      ...actual.purchaseCreditNoteService,
      getById: (...a: unknown[]) => getByIdMock(...a),
    },
  };
});

import { PurchaseCreditNoteDetailPage } from "./PurchaseCreditNoteDetailPage";

function buildDto(overrides: Partial<PurchaseCreditNoteDto> = {}): PurchaseCreditNoteDto {
  return {
    id: "cn-1",
    purchaseInvoiceId: "inv-1",
    supplierId: "sup-1",
    branchId: "br-1",
    receptionDocumentId: null,
    applicationType: "Discount",
    linkedPurchaseReturnId: null,
    status: "Draft",
    creditNoteNumber: "NC-001",
    accessKey: null,
    authorizationNumber: null,
    authorizationDate: null,
    issueDate: "2026-07-01T00:00:00Z",
    reason: "Descuento por pronto pago",
    subtotal: 100,
    iceAmount: 0,
    vatAmount: 15,
    totalAmount: 115,
    appliedToPayableAmount: 115,
    authorizedAtUtc: null,
    cancelledAtUtc: null,
    cancellationReason: null,
    lines: [],
    taxSummaries: [
      {
        id: "ts-1",
        sourcePurchaseInvoiceTaxSummaryId: "src-1",
        vatCode: "2",
        vatRate: 15,
        vatName: "IVA 15%",
        iceCode: null,
        iceRate: 0,
        iceName: null,
        taxableBase: 100,
        iceAmount: 0,
        vatAmount: 15,
        totalAmount: 115,
      },
    ],
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    invoiceNumber: "001-001-000000123",
    supplierName: "Proveedor Demo",
    invoiceBalanceDue: 200,
    receptionDocumentAccessKey: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/purchases/credit-notes/cn-1"]}>
        <Routes>
          <Route path="/purchases/credit-notes/:id" element={<PurchaseCreditNoteDetailPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  getByIdMock.mockReset();
});

describe("PurchaseCreditNoteDetailPage — valores de solo lectura migrados a ZHMoneyValue (PURCHASES-DS-MONEY-02)", () => {
  it("saldo pendiente, total crédito y reduce CxP usan ZHMoneyValue sin símbolo de moneda", async () => {
    getByIdMock.mockResolvedValue(buildDto());

    const { container } = renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    const values = container.querySelectorAll(
      ".pcn-summary-grid__value .zh-money-value",
    );
    // saldo pendiente (200.00), total crédito (115.00), reduce CxP (115.00)
    expect(values.length).toBeGreaterThanOrEqual(3);
    const texts = Array.from(values).map((el) => el.textContent);
    expect(texts).toContain("200.00");
    expect(texts).toContain("115.00");
    values.forEach((el) => {
      expect(el.textContent?.includes("$")).toBe(false);
    });
  });

  it("saldo pendiente ausente (null) muestra el mismo guion largo que antes", async () => {
    getByIdMock.mockResolvedValue(buildDto({ invoiceBalanceDue: null }));

    const { container } = renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    const balanceValueContainer = Array.from(
      container.querySelectorAll(".pcn-summary-grid__value"),
    )[1];
    expect(balanceValueContainer?.textContent).toBe("—");
  });

  it("la tabla de resumen fiscal migró sus celdas numéricas a ZHMoneyValue", async () => {
    getByIdMock.mockResolvedValue(buildDto());

    const { container } = renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    const table = container.querySelector("table.pcn-lines-table");
    expect(table).toBeTruthy();
    const numCells = container.querySelectorAll(
      "table.pcn-lines-table td.zh-table-cell--num .zh-money-value",
    );
    expect(numCells.length).toBeGreaterThan(0);
  });

  it("no hay estilos inline en los valores migrados", async () => {
    getByIdMock.mockResolvedValue(buildDto());

    const { container } = renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
