// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto } from "../api/salesService";
import type { PaymentMethodDto } from "../api/paymentMethodService";

// ── Mocks de componentes pesados: esta suite prueba el ajuste de contraste de
// las filas complementarias del método de pago (monto/referencia/crédito)
// debajo del tile activo (SALES-DS-TOGGLE-TILE-10A). ──
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

function buildPaymentMethod(
  overrides: Partial<PaymentMethodDto> = {},
): PaymentMethodDto {
  return {
    id: "pm-cash",
    code: "01",
    name: "Efectivo",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: false,
    sortOrder: 1,
    detailType: "None",
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
    paymentMethods: [buildPaymentMethod()],
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

describe("SalesPage — contraste de filas complementarias del método de pago (SALES-DS-TOGGLE-TILE-10A)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("renderiza la fila de monto (input) cuando un método simple tiene pago registrado", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [
          { _key: 1, paymentMethodId: "pm-cash", amount: 115, reference: null },
        ],
      }),
    );
    const { container } = renderSalesPage();

    expect(container.querySelector(".sales-payment-dollar")).toBeTruthy();
    const input = container.querySelector<HTMLInputElement>(
      ".sales-payment-input",
    );
    expect(input).toBeTruthy();
  });

  it("renderiza el monto de referencia cuando un método con referencia tiene pago registrado", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paymentMethods: [
          buildPaymentMethod({
            id: "pm-card",
            code: "19",
            name: "Tarjeta",
            requiresReference: true,
            detailType: "Card",
          }),
        ],
        payments: [
          { _key: 1, paymentMethodId: "pm-card", amount: 50, reference: "ref-1" },
        ],
      }),
    );
    const { container } = renderSalesPage();

    const refAmount = container.querySelector(".sales-payment-ref-amount");
    expect(refAmount).toBeTruthy();
    expect(container.querySelector(".sales-payment-ref-count")).toBeTruthy();
  });

  it("renderiza el monto de crédito cuando el método de crédito tiene pago registrado", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paymentMethods: [
          buildPaymentMethod({
            id: "pm-credit",
            code: "20",
            name: "Crédito",
            isCreditAllowed: true,
          }),
        ],
        payments: [
          { _key: 1, paymentMethodId: "pm-credit", amount: 115, reference: null },
        ],
      }),
    );
    const { container } = renderSalesPage();

    expect(container.querySelector(".sales-payment-credit-amount")).toBeTruthy();
  });

  it("el método activo sigue usando aria-pressed=true tras el ajuste de contraste", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [
          { _key: 1, paymentMethodId: "pm-cash", amount: 115, reference: null },
        ],
      }),
    );
    renderSalesPage();

    const tile = screen.getByText("Efectivo").closest("button")!;
    expect(tile.getAttribute("aria-pressed")).toBe("true");
  });

  it("no existe ningún elemento con clase sales-payment-method--active en el DOM", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [
          { _key: 1, paymentMethodId: "pm-cash", amount: 115, reference: null },
        ],
      }),
    );
    const { container } = renderSalesPage();

    expect(
      container.querySelector(".sales-payment-method--active"),
    ).toBeNull();
  });

  it("no existe ningún elemento con clase sales-payment-method__btn en el DOM", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    expect(container.querySelector(".sales-payment-method__btn")).toBeNull();
  });

  it("no hay estilos inline en el área de método de pago", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [
          { _key: 1, paymentMethodId: "pm-cash", amount: 115, reference: null },
        ],
      }),
    );
    const { container } = renderSalesPage();

    container
      .querySelectorAll(".sales-payment-grid, .sales-payment-grid *")
      .forEach((el) => {
        expect(el.getAttribute("style")).toBeNull();
      });
  });

  it("el botón Emitir (EmitButton) sigue intacto y no fue tocado por el ajuste de contraste", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    const emitBtn = container.querySelector(".sf-bottombar__emit");
    expect(emitBtn).toBeTruthy();
    expect(emitBtn?.getAttribute("style")).toBeNull();
  });
});
