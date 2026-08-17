// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto } from "../api/salesService";

// ── Mocks de componentes pesados: esta suite prueba únicamente la migración
// de EmitButton ("Emitir Factura (F8)") a ZHBtn variant="cta" (SALES-DS-CTA-11). ──
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
    formWatch: { docTypeCode: "", sriPaymentMethodCode: "", customerId: "cust-1" },
    setValue: vi.fn(),
    getValues: vi.fn(),
    reset: vi.fn(),

    lines: [{ itemId: "item-1" }],
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
    paymentOk: true,

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
    canEmit: true,
    openIssueFlow: vi.fn(),
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
    isElectronic: false,
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

describe("SalesPage — EmitButton migrado a ZHBtn variant=cta (SALES-DS-CTA-11)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('renderiza "Emitir Factura (F8)" cuando la factura no es electrónica', () => {
    useSalesPageMock.mockReturnValue(buildCtx({ isElectronic: false }));
    renderSalesPage();

    expect(screen.getByText("Emitir Factura (F8)")).toBeTruthy();
  });

  it('renderiza "Emitir Factura Electrónica (F8)" cuando la factura es electrónica', () => {
    useSalesPageMock.mockReturnValue(buildCtx({ isElectronic: true }));
    renderSalesPage();

    expect(screen.getByText("Emitir Factura Electrónica (F8)")).toBeTruthy();
  });

  it("usa ZHBtn variant=cta y no queda un <button> local con className sf-bottombar__emit", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.className).toContain("zh-btn");
    expect(btn.className).toContain("zh-btn--cta");
    expect(container.querySelector(".sf-bottombar__emit")).toBeNull();
  });

  it("click mantiene el handler de emisión (ctx.openIssueFlow)", () => {
    const openIssueFlow = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ openIssueFlow }));
    renderSalesPage();

    screen.getByText("Emitir Factura (F8)").click();

    expect(openIssueFlow).toHaveBeenCalledTimes(1);
  });

  it("disabled sigue funcionando cuando canEmit es false", () => {
    const openIssueFlow = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({ canEmit: false, openIssueFlow }),
    );
    renderSalesPage();

    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.disabled).toBe(true);
    btn.click();
    expect(openIssueFlow).not.toHaveBeenCalled();
  });

  it("habilitado (canEmit=true) permite el click", () => {
    const openIssueFlow = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({ canEmit: true, openIssueFlow }),
    );
    renderSalesPage();

    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.disabled).toBe(false);
  });

  it("no toca el selector de método de pago (ZHToggleTile sigue presente para métodos disponibles)", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        paymentMethods: [
          {
            id: "pm-cash",
            code: "01",
            name: "Efectivo",
            isActive: true,
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            detailType: "None",
          },
        ],
      }),
    );
    renderSalesPage();

    const tile = screen.getByText("Efectivo").closest("button")!;
    expect(tile.className).toContain("zh-toggle-tile");
  });

  it("no hay estilos inline en el botón de emisión", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.getAttribute("style")).toBeNull();
    btn.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
