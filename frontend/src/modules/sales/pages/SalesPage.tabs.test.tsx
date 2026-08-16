// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";

// ── Mocks de componentes pesados: esta suite prueba únicamente la
// migración de los tabs locales sf-tabs/sf-tab a ZHTabBar (SALES-DS-TABS-02),
// no el resto de la maquinaria ya existente del formulario de Ventas. ──
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

function buildCtx(
  overrides: Partial<SalesPageContext> & {
    listItems?: SalesPageContext["listItems"];
  } = {},
): SalesPageContext {
  const setTab = vi.fn();
  const resetForm = vi.fn();

  const base = {
    tab: "nuevo",
    setTab,
    listItems: [],
    listLoading: false,
    listSearch: "",
    setListSearch: vi.fn(),
    saving: false,
    saveError: null,
    setSaveError: vi.fn(),
    editing: null,
    hasInsufficientStock: false,

    form: {},
    register: vi.fn(),
    control: {},
    errors: {},
    formWatch: {
      docTypeCode: "",
      sriPaymentMethodCode: "",
      customerId: "",
    },
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
    summary: {
      subtotal: 0,
      discount: 0,
      netSubtotal: 0,
      vat: 0,
      ice: 0,
      total: 0,
      taxBreakdown: [],
    },
    grandTotal: 0,
    totalDiscount: 0,
    taxBreakdown: [],
    isElectronic: true,
    selectedPt: null,

    fetchList: vi.fn(),
    resetForm,
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

describe("SalesPage — tabs (ZHTabBar, SALES-DS-TABS-02)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('renderiza el tab "Nueva Factura" (form) y "Historial"', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText("Nueva Factura")).not.toBeNull();
    expect(screen.getByText("Historial")).not.toBeNull();
  });

  it('"Nueva/Editar Factura" aparece como tab activo (aria-selected=true)', () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const formTab = screen.getByText("Nueva Factura").closest("button");
    expect(formTab?.getAttribute("aria-selected")).toBe("true");

    const historyTab = screen.getByText("Historial").closest("button");
    expect(historyTab?.getAttribute("aria-selected")).toBe("false");
  });

  it('click en "Nueva/Editar Factura" no navega ni dispara acción (inert)', () => {
    const setTab = vi.fn();
    const resetForm = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ setTab, resetForm }));
    renderSalesPage();

    screen.getByText("Nueva Factura").click();

    expect(setTab).not.toHaveBeenCalled();
    expect(resetForm).not.toHaveBeenCalled();
  });

  it('click en "Historial" mantiene la navegación actual (resetForm + setTab("listado"))', () => {
    const setTab = vi.fn();
    const resetForm = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ setTab, resetForm }));
    renderSalesPage();

    screen.getByText("Historial").click();

    expect(resetForm).toHaveBeenCalledTimes(1);
    expect(setTab).toHaveBeenCalledWith("listado");
  });

  it('editando una factura, el tab activo muestra "Editar Factura"', () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        editing: {
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
        },
      }),
    );
    renderSalesPage();

    expect(screen.getByText("Editar Factura")).not.toBeNull();
    expect(screen.queryByText("Nueva Factura")).toBeNull();
  });

  it("no rompe el render principal de SalesPage (form visible)", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    expect(container.querySelector(".sf-layout")).not.toBeNull();
    expect(container.querySelector(".sf-sidebar")).not.toBeNull();
  });

  it("no hay estilos inline en la barra de tabs", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    const tabBar = container.querySelector(".prd-tabs");
    expect(tabBar).not.toBeNull();
    tabBar?.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
    expect(tabBar?.getAttribute("style")).toBeNull();
  });
});
