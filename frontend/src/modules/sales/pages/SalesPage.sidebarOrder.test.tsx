// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto } from "../api/salesService";

// SALES-POS-SIDEBAR-SECTION-ORDER-01: reordenamiento puramente visual del panel izquierdo de
// /sales — "Configuración de venta" pasa a mostrarse antes que "Cliente" (contexto base de
// emisión primero), sin tocar lógica/estado/cálculo/validaciones ni el comportamiento de ninguna
// de las dos secciones. Esta suite no mockea CustomerPicker (a diferencia de otras suites de
// SalesPage) para poder verificar que sigue editable de verdad, no solo que el mock se llamó.
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
vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn().mockResolvedValue(null),
    searchCustomersForPicker: vi.fn().mockResolvedValue([]),
  },
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
    paymentSchedule: [],
    isPaymentScheduleManual: false,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    electronicIssueError: null,
    ...overrides,
  };
}

function buildCtx(overrides: Partial<SalesPageContext> = {}): SalesPageContext {
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
    formWatch: { docTypeCode: "01", sriPaymentMethodCode: "01", customerId: "cust-1" },
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
        sriPaymentMethodCode: "01",
      },
    ],
    paidTotal: 0,

    customerProfile: null,
    setCustomerProfile: vi.fn(),
    handleCustomerChange: vi.fn(),
    openNewCustomerModal: vi.fn(),
    openEditCustomerModal: vi.fn(),

    paymentTermsList: [],
    warehouses: [],
    selectedWarehouseId: "wh-1",
    setSelectedWarehouseId: vi.fn(),
    vatRatesMap: {},
    iceRatesMap: {},
    sriDocTypes: [{ code: "01", name: "Factura" }],
    sriPaymentMethods: [
      { code: "01", name: "Sin utilización del sistema financiero" },
    ],
    sriIdTypes: [],

    hasCashSession: true,
    myCashSession: {
      id: "cash-session-1",
      companyId: "company-1",
      branchId: "branch-1",
      userId: "user-1",
      cashRegisterId: "cr-1",
      cashRegisterCodeSnapshot: "CAJA-01",
      cashRegisterNameSnapshot: "Caja Principal",
      emissionPointId: "ep-1",
      emissionPointCodeSnapshot: "001",
      emissionType: "Physical",
      defaultWarehouseId: null,
      defaultCustomerId: null,
    },
    branchName: "Sucursal Principal",

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
    taxBreakdown: [{ rate: 15, label: "IVA 15%", base: 100, tax: 15 }],
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

describe("SalesPage — orden de secciones del sidebar (SALES-POS-SIDEBAR-SECTION-ORDER-01)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('"Configuración de venta" aparece antes que "Cliente" en el DOM', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    const headers = Array.from(
      container.querySelectorAll(".sf-sidebar__header"),
    ).map((el) => el.textContent?.trim());
    // El texto incluye el nombre del ícono Material Symbols como contenido literal (p. ej.
    // "apartmentConfiguración de venta") — se usa includes() en vez de startsWith() por eso.
    const configIdx = headers.findIndex((h) => h?.includes("Configuración de venta"));
    const clienteIdx = headers.findIndex((h) => h?.includes("Cliente"));

    expect(configIdx).toBeGreaterThanOrEqual(0);
    expect(clienteIdx).toBeGreaterThanOrEqual(0);
    expect(configIdx).toBeLessThan(clienteIdx);
  });

  it("Cliente sigue visible y editable (CustomerPicker real, sin disabled)", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText("Cliente")).toBeTruthy();
    const input = screen.getByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    ) as HTMLInputElement;
    expect(input.disabled).toBe(false);
  });

  it("Formas de Cobro siguen visibles fuera del modal", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText("Formas de Cobro")).toBeTruthy();
  });

  it('"Total a Cobrar" sigue visible', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText("Total a Cobrar")).toBeTruthy();
  });

  it('no aparece ningún badge "LISTA" en la tarjeta de configuración cuando todo está OK', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    expect(screen.queryByText("Lista")).toBeNull();
    expect(container.querySelector(".badge--success")).toBeNull();
  });

  it('el modal de "Configuración" sigue abriendo desde su nueva posición', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));
    expect(screen.getByRole("dialog")).toBeTruthy();
  });
});
