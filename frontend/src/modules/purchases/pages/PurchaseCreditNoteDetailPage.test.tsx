// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
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
    linkedPurchaseReturnNumber: null,
    linkedPurchaseReturnStatus: null,
    linkedPurchaseReturnAuthorizedGrandTotal: null,
    ...overrides,
  };
}

function LocationMarker({ label }: { label: string }) {
  const location = useLocation();
  return (
    <div>
      {label}: {location.pathname}
      {location.search}
    </div>
  );
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/purchases/credit-notes/cn-1"]}>
        <Routes>
          <Route path="/purchases/credit-notes/:id" element={<PurchaseCreditNoteDetailPage />} />
          <Route
            path="/purchases/credit-notes"
            element={<LocationMarker label="Listado NC" />}
          />
          <Route path="/purchases" element={<LocationMarker label="Facturas de compra" />} />
          <Route
            path="/purchases/returns/:id"
            element={<LocationMarker label="Devolución" />}
          />
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

// PURCHASE-CREDIT-NOTE-SINGLE-REVIEW-SCREEN-01 — el usuario no debe necesitar abrir
// /purchases/returns/{id} para entender el caso completo de una NC tipo Devolución.
describe("PurchaseCreditNoteDetailPage — pantalla única de revisión (NC tipo Devolución)", () => {
  it("muestra N.º/estado/total de la devolución vinculada y producto/bodega reales sin salir de la pantalla", async () => {
    getByIdMock.mockResolvedValue(
      buildDto({
        applicationType: "Return",
        linkedPurchaseReturnId: "ret-1",
        linkedPurchaseReturnNumber: "DEV-000001",
        linkedPurchaseReturnStatus: "Authorized",
        linkedPurchaseReturnAuthorizedGrandTotal: 115,
        appliedToPayableAmount: 115,
        lines: [
          {
            id: "line-1",
            purchaseInvoiceDetailId: "detail-1",
            quantity: 2,
            iceAmount: 0,
            irbpnrAmount: 0,
            description: "Producto legado",
            subtotal: 100,
            vatCode: "2",
            vatRate: 15,
            vatAmount: 15,
            totalAmount: 115,
            itemSku: "SKU-001",
            itemName: "Producto de prueba",
            warehouseName: "Bodega Principal",
          },
        ],
        taxSummaries: [],
      }),
    );

    const { container } = renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    expect(screen.getByText("DEV-000001")).toBeTruthy();
    expect(screen.getByText("Autorizada")).toBeTruthy();
    expect(screen.getByText("SKU-001 — Producto de prueba")).toBeTruthy();
    expect(screen.getByText("Bodega Principal")).toBeTruthy();
    expect(screen.queryByText("Producto legado")).toBeNull();

    const table = container.querySelector("table.pcn-lines-table");
    expect(table?.textContent).toContain("Bodega");
  });

  it("cae de vuelta a la descripción libre si el backend no pudo resolver producto/bodega", async () => {
    getByIdMock.mockResolvedValue(
      buildDto({
        applicationType: "Return",
        linkedPurchaseReturnId: "ret-1",
        linkedPurchaseReturnNumber: null,
        linkedPurchaseReturnStatus: "Draft",
        linkedPurchaseReturnAuthorizedGrandTotal: null,
        lines: [
          {
            id: "line-1",
            purchaseInvoiceDetailId: "detail-1",
            quantity: 2,
            iceAmount: 0,
            irbpnrAmount: 0,
            description: "Producto legado",
            subtotal: 100,
            vatCode: "2",
            vatRate: 15,
            vatAmount: 15,
            totalAmount: 115,
            itemSku: null,
            itemName: null,
            warehouseName: null,
          },
        ],
        taxSummaries: [],
      }),
    );

    renderPage();

    await screen.findAllByText("NC-001", { exact: false });

    expect(screen.getByText("Producto legado")).toBeTruthy();
    expect(screen.getAllByText("Borrador").length).toBeGreaterThanOrEqual(1);
  });
});

// PURCHASE-CREDIT-NOTE-DETAIL-ROUTING-AUDIT-01 — este detalle pertenece al módulo Notas de
// Crédito de Compra: ningún botón/fallback debe llevar a Facturas de compra (/purchases) salvo
// para abrir la factura afectada puntual (única forma real de llegar a ella en esta app).
describe("PurchaseCreditNoteDetailPage — navegación (PURCHASE-CREDIT-NOTE-DETAIL-ROUTING-AUDIT-01)", () => {
  it('botón "Volver" navega a /purchases/credit-notes, nunca a /purchases', async () => {
    getByIdMock.mockResolvedValue(buildDto());

    renderPage();
    await screen.findAllByText("NC-001", { exact: false });

    fireEvent.click(screen.getByRole("button", { name: "Volver" }));

    expect(await screen.findByText(/Listado NC: \/purchases\/credit-notes/)).toBeTruthy();
    expect(screen.queryByText(/Facturas de compra:/)).toBeNull();
  });

  it('botón "Ver factura afectada" navega a la factura afectada (/purchases?invoiceId=)', async () => {
    getByIdMock.mockResolvedValue(buildDto({ purchaseInvoiceId: "inv-42" }));

    renderPage();
    await screen.findAllByText("NC-001", { exact: false });

    fireEvent.click(screen.getByRole("button", { name: "Ver factura afectada" }));

    expect(
      await screen.findByText("Facturas de compra: /purchases?invoiceId=inv-42"),
    ).toBeTruthy();
  });

  it('botón "Ver devolución vinculada" navega a la devolución vinculada', async () => {
    getByIdMock.mockResolvedValue(
      buildDto({
        applicationType: "Return",
        linkedPurchaseReturnId: "ret-99",
      }),
    );

    renderPage();
    await screen.findAllByText("NC-001", { exact: false });

    fireEvent.click(screen.getByRole("button", { name: "Ver devolución vinculada" }));

    expect(await screen.findByText("Devolución: /purchases/returns/ret-99")).toBeTruthy();
  });

  it("un error al cargar la NC no cae de vuelta a Facturas de compra", async () => {
    getByIdMock.mockRejectedValue(new Error("network error"));

    renderPage();

    expect(await screen.findByText(/Listado NC: \/purchases\/credit-notes/)).toBeTruthy();
    expect(screen.queryByText(/Facturas de compra:/)).toBeNull();
  });
});
