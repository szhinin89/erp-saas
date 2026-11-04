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

vi.mock("../../ride/facades/rideGenerationFacade", () => ({
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

describe("SalesPage - discount consistency", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

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
    expect(container.querySelectorAll(".sales-tax-summary .zh-money-value")[0]?.textContent)
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

});
