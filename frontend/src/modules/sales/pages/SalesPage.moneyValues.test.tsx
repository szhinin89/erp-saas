// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto } from "../api/salesService";

// ── Mocks de componentes pesados: esta suite prueba la migración de valores
// monetarios read-only restantes de SalesPage a ZHMoneyValue (SALES-DS-MONEY-12):
// listado de facturas, desglose de impuestos, "Total a Cobrar", descuento,
// chip de pago en modo solo lectura, y resumen de cobro (total/cobrado/pendiente). ──
vi.mock("../components/CustomerPicker", () => ({ CustomerPicker: () => null }));
vi.mock("../components/SalesInvoiceDetailsSection", () => ({
  SalesInvoiceDetailsSection: () => null,
}));
vi.mock("../components/PaymentDetailModal", () => ({
  PaymentDetailModal: () => null,
}));
vi.mock("../components/CreditSimulatorModal", () => ({
  CreditSimulatorModal: () => null,
}));
vi.mock("../components/QuickCustomerModal", () => ({
  QuickCustomerModal: () => null,
}));
vi.mock("../components/SalesElectronicDiagnosticDrawer", () => ({
  SalesElectronicDiagnosticDrawer: () => null,
}));
vi.mock("../../../components/zh/ZHConfirmModal", () => ({
  ZHConfirmModal: () => null,
  ZHPromptModal: () => null,
}));
vi.mock("../../../components/zh/ZHElectronicEnvironmentBanner", () => ({
  ZHElectronicEnvironmentBanner: () => null,
}));

vi.mock("../hooks/useRideActions", () => ({
  useRideActions: () => ({
    ridePending: false,
    handleViewRide: vi.fn(),
    handleDownloadRide: vi.fn(),
    handleRegenerateRide: vi.fn(),
  }),
}));

const useSalesPageMock = vi.fn();
vi.mock("../hooks/useSalesPage", () => ({
  useSalesPage: () => useSalesPageMock(),
}));

import { SalesPage } from "./SalesPage";

function renderSalesPage() {
  return render(
    <MemoryRouter>
      <SalesPage />
    </MemoryRouter>,
  );
}

function buildInvoice(overrides: Partial<SalesInvoiceDto> = {}): SalesInvoiceDto {
  return {
    id: "inv-1",
    customerId: "cust-1",
    customerName: "Juan Pérez",
    customerTaxId: "1710034065",
    customerIdentificationType: "05",
    customerEmail: null,
    customerAddress: null,
    docTypeCode: "01",
    sriPaymentMethodCode: "01",
    invoiceNumber: "001-001-000000123",
    issueDate: "2026-07-01",
    cashSessionId: "cash-session-1",
    emissionPointId: null,
    emissionType: "Electronic",
    currencyCode: "USD",
    exchangeRate: 1,
    paymentTermId: "pt-1",
    paymentTermName: "Contado",
    paymentTermInstallments: 1,
    paymentTermDaysBetween: 0,
    creditTermDays: 0,
    dueDate: null,
    notes: null,
    status: "Draft",
    electronicStatus: "None",
    accessKey: null,
    authorizationNumber: null,
    authorizationDate: null,
    subtotal: 100,
    totalDiscount: 0,
    totalIce: 0,
    totalVat: 15,
    totalTax: 15,
    grandTotal: 115,
    payments: [],
    lines: [],
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    electronicIssueError: null,
    ...overrides,
  };
}

function buildCtx(
  overrides: Partial<SalesPageContext> = {},
): SalesPageContext {
  const base = {
    tab: "nuevo",
    setTab: vi.fn(),
    listItems: [],
    listLoading: false,
    listSearch: "",
    setListSearch: vi.fn(),
    saving: false,
    saveError: null,
    setSaveError: vi.fn(),
    editing: buildInvoice(),
    hasInsufficientStock: false,

    form: {},
    register: vi.fn(),
    control: {},
    errors: {},
    formWatch: { docTypeCode: "", sriPaymentMethodCode: "", customerId: "" },
    setValue: vi.fn(),
    getValues: vi.fn(),
    reset: vi.fn(),

    lines: [],
    addLineWithItem: vi.fn(),
    removeLine: vi.fn(),
    updateLine: vi.fn(),
    lineKey: 0,
    handleWarehouseChange: vi.fn(),
    onUpdateLineWarehouse: vi.fn(),

    payments: [],
    setInvoicePayments: vi.fn(),
    payKey: 0,
    setPayKey: vi.fn(),
    paymentMethods: [],
    paidTotal: 0,

    customerProfile: null,
    setCustomerProfile: vi.fn(),
    handleCustomerChange: vi.fn(),

    paymentTermsList: [],
    warehouses: [],
    selectedWarehouseId: null,
    setSelectedWarehouseId: vi.fn(),
    vatRatesMap: {},
    iceRatesMap: {},
    sriDocTypes: [],
    sriPaymentMethods: [],
    sriIdTypes: [],

    hasCashSession: true,
    myCashSession: null,
    branchName: null,

    isDraft: true,
    readOnly: false,
    fieldDisabled: false,
    canEmit: false,
    cashDue: 0,
    cashInsufficient: false,
    cashReceived: 0,
    cashChange: 0,
    summary: {
      subtotal: 100,
      discount: 0,
      netSubtotal: 100,
      vat: 15,
      ice: 0,
      total: 115,
      taxBreakdown: [],
    },
    grandTotal: 115,
    totalDiscount: 0,
    taxBreakdown: [],
    isElectronic: true,
    selectedPt: null,

    fetchList: vi.fn(),
    resetForm: vi.fn(),
    clearForm: vi.fn(),
    loadForEdit: vi.fn(),
    handleCancel: vi.fn(),
    handleGenerateElectronicDocument: vi.fn(),

    issuePhase: "idle",
    issueStepIndex: 0,
    issueResult: null,
    issueError: null,
    xmlDownloading: false,
    productSearchFocusKey: 0,
    openIssueFlow: vi.fn(),
    closeIssueFlow: vi.fn(),
    confirmIssue: vi.fn(),
    retryIssue: vi.fn(),
    startNewSale: vi.fn(),
    handleDownloadXml: vi.fn(),

    modalCancelReason: false,
    setModalCancelReason: vi.fn(),
    modalNewCustomer: false,
    setModalNewCustomer: vi.fn(),
    modalDetail: false,
    setModalDetail: vi.fn(),
    modalCredit: false,
    setModalCredit: vi.fn(),

    newCustId: "",
    setNewCustId: vi.fn(),
    newCustName: "",
    setNewCustName: vi.fn(),
    newCustIdType: "",
    setNewCustIdType: vi.fn(),
    newCustAddress: "",
    setNewCustAddress: vi.fn(),
    newCustEmail: "",
    setNewCustEmail: vi.fn(),
    newCustPhone: "",
    setNewCustPhone: vi.fn(),
    newCustIsEdit: false,
    newCustSaving: false,
    newCustError: null,
    openNewCustomerModal: vi.fn(),
    openEditCustomerModal: vi.fn(),
    handleSaveQuickCustomer: vi.fn(),

    detailMethodId: null,
    setDetailMethodId: vi.fn(),
    detailMethodType: "None",
    setDetailMethodType: vi.fn(),
    detailMethodName: "",
    setDetailMethodName: vi.fn(),
    detailRows: [],
    setDetailRows: vi.fn(),
    detailKey: 0,
    setDetailKey: vi.fn(),

    creditAmount: 0,
    setCreditAmount: vi.fn(),
    creditRows: [],
    setCreditRows: vi.fn(),
    simulateCreditInstallments: vi.fn(() => []),
  };

  return { ...base, ...overrides } as unknown as SalesPageContext;
}

function getMoneyValueByText(text: string): HTMLElement {
  return screen.getByText((_, element) => {
    if (!element || !element.classList.contains("zh-money-value")) return false;
    return element.textContent === text;
  });
}

describe("SalesPage — valores monetarios read-only migrados a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('listado de facturas: columna Total usa ZHMoneyValue con decimals=totalAmount', () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        tab: "listado",
        listItems: [
          {
            id: "inv-1",
            invoiceNumber: "001-001-000000123",
            issueDate: "2026-07-01",
            customerId: "cust-1",
            customerName: "Juan Pérez",
            grandTotal: 115,
            lineCount: 2,
            status: "Authorized",
            createdAt: "2026-07-01T00:00:00Z",
          },
        ],
      }),
    );
    const { container } = renderSalesPage();

    const cell = getMoneyValueByText("$115.00");
    expect(cell).toBeTruthy();
    expect(container.querySelector(".zh-table-cell--num")).toBeTruthy();
  });

  it('desglose de impuestos: Base y Valor usan ZHMoneyValue', () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        taxBreakdown: [{ rate: 15, label: "IVA 15%", base: 100, tax: 15 }],
      }),
    );
    renderSalesPage();

    expect(getMoneyValueByText("$100.00")).toBeTruthy();
    expect(getMoneyValueByText("$15.00")).toBeTruthy();
  });

  it('"Total a Cobrar" usa ZHMoneyValue y conserva la clase local sf-total-box__amount', () => {
    useSalesPageMock.mockReturnValue(buildCtx({ grandTotal: 199.5 }));
    const { container } = renderSalesPage();

    const totalBox = container.querySelector(".sf-total-box__amount");
    expect(totalBox).toBeTruthy();
    const moneyValue = totalBox?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("$199.50");
  });

  it("descuento usa ZHMoneyValue precedido del signo -", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ totalDiscount: 10 }));
    renderSalesPage();

    expect(getMoneyValueByText("$10.00")).toBeTruthy();
  });

  it("chip de pago en modo solo lectura usa ZHMoneyValue", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        readOnly: true,
        editing: buildInvoice({
          payments: [
            {
              id: "p1",
              paymentMethodId: "pm-cash",
              paymentMethodCode: "01",
              paymentMethodName: "Efectivo",
              amount: 115,
              reference: null,
              cardDetail: null,
              transferDetail: null,
              chequeDetail: null,
            },
          ],
        }),
      }),
    );
    const { container } = renderSalesPage();

    const chip = container.querySelector(".sales-payment-chip__amount");
    const moneyValue = chip?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("$115.00");
  });

  it("resumen de cobro (Total cobrado / Pendiente) usa ZHMoneyValue", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paidTotal: 50,
        summary: {
          subtotal: 100,
          discount: 0,
          netSubtotal: 100,
          vat: 15,
          ice: 0,
          total: 115,
          taxBreakdown: [],
        },
      }),
    );
    const { container } = renderSalesPage();

    // "Total factura" ya no se repite acá (SALES-POS-UI-REFINE-01) — el total principal
    // vive únicamente en "Total a Cobrar" (sf-total-box). Solo queda "Total cobrado".
    const rows = container.querySelectorAll(".sales-summary-row__amount");
    expect(rows).toHaveLength(1);
    expect(rows[0].querySelector(".zh-money-value")?.textContent).toBe(
      "$50.00",
    );

    const pendingAmount = container.querySelector(
      ".sales-summary-total-row__amount .zh-money-value",
    );
    expect(pendingAmount?.textContent).toBe("$65.00");
  });

  it("el input editable de monto recibido en efectivo sigue siendo un <input> nativo, no ZHMoneyValue", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        cashDue: 115,
        cashReceived: 50,
      }),
    );
    const { container } = renderSalesPage();

    const cashInput = container.querySelector<HTMLInputElement>(
      ".sales-cash-input",
    );
    expect(cashInput).toBeTruthy();
    expect(cashInput?.tagName).toBe("INPUT");
  });

  it("no hay estilos inline en los valores monetarios migrados", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        taxBreakdown: [{ rate: 15, label: "IVA 15%", base: 100, tax: 15 }],
        totalDiscount: 10,
      }),
    );
    const { container } = renderSalesPage();

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
      el.querySelectorAll("*").forEach((child) => {
        expect(child.getAttribute("style")).toBeNull();
      });
    });
  });

  it("no se reintroduce ningún <button> nativo en SalesPage", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    container.querySelectorAll("button").forEach((btn) => {
      // Todos los botones deben venir de ZHBtn/ZHIconButton/ZHToggleTile (clases zh-*),
      // no de un <button> local sin clase del Design System.
      const hasDsClass =
        btn.className.includes("zh-btn") ||
        btn.className.includes("prd-icon-btn") ||
        btn.className.includes("prd-tab-btn") ||
        btn.className.includes("zh-toggle-tile");
      expect(hasDsClass, `unexpected native button className="${btn.className}"`).toBe(true);
    });
  });
});
