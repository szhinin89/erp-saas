// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, cleanup, within } from "@testing-library/react";
import { renderSalesUi as render } from "../test/renderSalesUi";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";
import type { SalesInvoiceDto } from "../api/salesService";
import { calcSummary } from "../utils/salesCalc";

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

vi.mock("../../caja/facades/manualCashMovementFacade", () => ({
  ManualCashMovementModal: () => null,
}));


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
    paymentSchedule: [],
    isPaymentScheduleManual: false,
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
    manualCashMovement: {
      allowManualMovements: false,
      canRecordManualMovements: false,
      saving: false,
      saveError: null,
      setSaveError: () => {},
      movementForm: { register: () => ({}), formState: { errors: {} } },
      movementTypes: [],
      reasons: [],
      reasonsLoading: false,
      selectedMovementType: null,
      movementModalOpen: false,
      openMovementModal: () => {},
      closeMovementModal: () => {},
      handleRecordMovement: () => {},
    },
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
    sriAvailability: "Unknown",
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

  return withPosDerivedCtx({ ...base, ...overrides });
}

function getMoneyValueByText(text: string): HTMLElement {
  return screen.getByText((_, element) => {
    if (!element || !element.classList.contains("zh-money-value")) return false;
    return element.textContent === text;
  });
}

describe("SalesPage — valores monetarios read-only migrados a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it.each([
    { pct: 25, discount: 0.075, subtotal: 0.305, base: 0.23, vat: 0.03, total: 0.26, baseDisplay: "0.23", vatDisplay: "0.03", totalDisplay: "0.26", display: "0.08", subtotalDisplay: "0.31" },
    { pct: 10, discount: 0.03, subtotal: 0.3, base: 0.27, vat: 0.04, total: 0.31, baseDisplay: "0.27", vatDisplay: "0.04", totalDisplay: "0.31", display: "0.03", subtotalDisplay: "0.30" },
    { pct: 1.15, discount: 0.00345, subtotal: 0.30345, base: 0.3, vat: 0.05, total: 0.35, baseDisplay: "0.30", vatDisplay: "0.05", totalDisplay: "0.35", display: "0.00", subtotalDisplay: "0.30" },
  ])("sidebar, emission modal and reopened discount agree for $pct%", (testCase) => {
    const summary = calcSummary([{
      description: "Producto", quantity: 1, unitPrice: 0.3,
      discountPct: testCase.pct, vatCode: "10",
    }], { "10": 15 });
    const original = structuredClone(summary);
    expect(summary).toMatchObject({
      discount: testCase.discount, subtotal: testCase.subtotal,
      netSubtotal: testCase.base, vat: testCase.vat, total: testCase.total,
    });
    useSalesPageMock.mockReturnValue(buildCtx({
      summary, totalDiscount: summary.discount, grandTotal: summary.total,
      taxBreakdown: summary.taxBreakdown, issuePhase: "confirm",
    }));
    const { container, rerender } = renderSalesPage();
    const sidebarDiscount = () => container.querySelector(
      ".sf-summary__discount-total .zh-money-value",
    )?.textContent;
    expect(sidebarDiscount()).toBe(`$${testCase.display}`);
    // Acotado al modal: el resumen inline de cobro (POS-COLLECTION-INLINE-B-01) también tiene
    // una fila "Total".
    const modalAmount = (label: string) =>
      within(screen.getByRole("dialog"))
        .getByText(label, { selector: "dt" })
        .nextElementSibling?.textContent;
    expect(modalAmount("Descuento")).toBe(`$${testCase.display}`);
    expect(modalAmount("Subtotal")).toBe(`$${testCase.subtotalDisplay}`);
    expect(modalAmount("IVA")).toBe(`$${testCase.vatDisplay}`);
    expect(modalAmount("Total")).toBe(`$${testCase.totalDisplay}`);
    expect(container.querySelectorAll(".sf-tax-table .zh-money-value")[0]?.textContent)
      .toBe(`$${testCase.baseDisplay}`);

    // Reopened read-only context uses the persisted DTO, as useSalesPage does.
    const invoice = buildInvoice({
      status: "Authorized", subtotal: testCase.subtotal,
      totalDiscount: testCase.discount, grandTotal: testCase.total,
      totalVat: testCase.vat,
    });
    useSalesPageMock.mockReturnValue(buildCtx({
      readOnly: true, isDraft: false, editing: invoice, summary,
      totalDiscount: invoice.totalDiscount, grandTotal: invoice.grandTotal,
      taxBreakdown: summary.taxBreakdown,
    }));
    rerender(<MemoryRouter><SalesPage /></MemoryRouter>);
    expect(sidebarDiscount()).toBe(`$${testCase.display}`);
    expect(container.querySelector(".sf-total-box__amount .zh-money-value")?.textContent)
      .toBe(`$${testCase.totalDisplay}`);
    expect(summary).toEqual(original);
  });

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
            emissionType: "Electronic",
          },
        ],
      }),
    );
    const { container } = renderSalesPage();

    const cell = getMoneyValueByText("$115.00");
    expect(cell).toBeTruthy();
    expect(container.querySelector(".zh-table-cell--num")).toBeTruthy();
  });

  it('listado de facturas: muestra "N°" primero, antes de "Nro. Factura", sin reemplazar el número funcional (ZH-LISTING-MAIN-ROW-NUMBER-FIX-07)', () => {
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
            emissionType: "Electronic",
          },
        ],
      }),
    );
    renderSalesPage();

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers[0]).toBe("N°");
    expect(headers.indexOf("N°")).toBeLessThan(headers.indexOf("Nro. Factura"));
    expect(screen.getByText("001-001-000000123")).toBeTruthy();
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
              tenderedAmount: null,
              changeAmount: null,
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

  // SALES-PAYMENT-TOLERANCE-NOTE-01: el pago mostrado sigue siendo el monto real cobrado (nunca
  // se falsea como si se hubiera cobrado el total exacto); cuando la diferencia contra el total
  // cae dentro de la tolerancia de settlement de la empresa (CompanyPrecisionPolicy — 0.01 en el
  // fixture de tests), se aclara con una nota corta.
  it("muestra la nota de tolerancia cuando el pago difiere del total dentro de la tolerancia de settlement", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        readOnly: true,
        grandTotal: 4.0,
        editing: buildInvoice({
          grandTotal: 4.0,
          payments: [
            {
              id: "p1",
              paymentMethodId: "pm-cash",
              paymentMethodCode: "01",
              paymentMethodName: "Efectivo",
              amount: 3.99,
              reference: null,
              cardDetail: null,
              transferDetail: null,
              chequeDetail: null,
              tenderedAmount: null,
              changeAmount: null,
            },
          ],
        }),
      }),
    );
    const { container } = renderSalesPage();

    // El chip sigue mostrando el monto real cobrado, no el total.
    const chip = container.querySelector(".sales-payment-chip__amount");
    expect(chip?.querySelector(".zh-money-value")?.textContent).toBe("$3.99");

    const note = container.querySelector(".sales-payment-tolerance-note");
    expect(note?.textContent).toBe(
      "Diferencia $0.01 dentro de tolerancia — saldada",
    );
  });

  it("no muestra la nota de tolerancia cuando el pago coincide exactamente con el total", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        readOnly: true,
        grandTotal: 4.0,
        editing: buildInvoice({
          grandTotal: 4.0,
          payments: [
            {
              id: "p1",
              paymentMethodId: "pm-cash",
              paymentMethodCode: "01",
              paymentMethodName: "Efectivo",
              amount: 4.0,
              reference: null,
              cardDetail: null,
              transferDetail: null,
              chequeDetail: null,
              tenderedAmount: null,
              changeAmount: null,
            },
          ],
        }),
      }),
    );
    const { container } = renderSalesPage();

    expect(container.querySelector(".sales-payment-tolerance-note")).toBeNull();
  });

  it("resumen de cobro (Total / Cobrado / Falta por cobrar) usa ZHMoneyValue", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paymentMethods: [
          {
            id: "pm-card",
            code: "TARJETA",
            name: "Tarjeta",
            isActive: true,
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            detailType: "None",
            affectsPhysicalCash: false,
            sriPaymentMethodCode: null,
            accountSource: "PaymentMethodAccount",
            accountingAccountId: null,
          },
        ],
        payments: [{ _key: 1, paymentMethodId: "pm-card", amount: 50, reference: null }],
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

    // POS-COLLECTION-INLINE-B-01: resumen compacto Total / Cobrado + un único estado.
    const amounts = Array.from(
      container.querySelectorAll(".sales-result__rows dd .zh-money-value"),
    ).map((el) => el.textContent);
    expect(amounts).toEqual(["$115.00", "$50.00"]);

    const pendingAmount = container.querySelector(
      ".sales-result__highlight-amount .zh-money-value",
    );
    expect(pendingAmount?.textContent).toBe("$65.00");
  });

  it("el input editable de efectivo recibido sigue siendo un <input> nativo, no ZHMoneyValue", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paymentMethods: [
          {
            id: "pm-cash",
            code: "EFECTIVO",
            name: "Efectivo",
            isActive: true,
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            detailType: "None",
            affectsPhysicalCash: true,
            sriPaymentMethodCode: "01",
            accountSource: "CashRegister",
            accountingAccountId: null,
          },
        ],
        payments: [{ _key: 1, paymentMethodId: "pm-cash", amount: 115, reference: null }],
        cashReceivedInput: "50",
      } as unknown as Partial<SalesPageContext>),
    );
    const { container } = renderSalesPage();

    const cashInput = container.querySelector<HTMLInputElement>(
      ".sales-tender__input",
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
      // Todos los botones deben venir de ZHBtn/ZHIconButton/ZHToggleTile/ZHHelpIcon (clases
      // zh-*/prd-*), no de un <button> local sin clase del Design System. zh-help-icon es el
      // trigger de ayuda contextual (ZHHelpIcon, components/zh/help/) que SalesPage ya usa vía
      // ZHFieldHelp/ZHSectionHelp/ZHNoticeBadge — la lista simplemente no lo incluía todavía.
      const hasDsClass =
        btn.className.includes("zh-btn") ||
        btn.className.includes("prd-icon-btn") ||
        btn.className.includes("prd-tab-btn") ||
        btn.className.includes("zh-toggle-tile") ||
        btn.className.includes("zh-help-icon");
      expect(hasDsClass, `unexpected native button className="${btn.className}"`).toBe(true);
    });
  });
});
