// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, cleanup } from "@testing-library/react";
import { renderSalesUi as render } from "../test/renderSalesUi";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";

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
    sriAvailability: "Unknown",
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

  return withPosDerivedCtx({ ...base, ...overrides });
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
          paymentSchedule: [],
          isPaymentScheduleManual: false,
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

// POS-VIEWPORT-LAYOUT-01 — estructura del layout de caja: la venta ocupa el viewport (opt-in del
// shell) y el panel izquierdo separa CONTEXTO (cede altura) de CHECKOUT (siempre visible).
describe("SalesPage — layout de caja (POS-VIEWPORT-LAYOUT-01)", () => {
  afterEach(() => cleanup());

  it("en Nueva Factura la raíz declara el opt-in de altura fija del shell", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ tab: "nuevo" }));
    const { container } = renderSalesPage();
    const root = container.querySelector(".sales-page-root");
    expect(root?.classList.contains("shell-fill-viewport")).toBe(true);
    expect(root?.classList.contains("sales-page-root--pos")).toBe(true);
  });

  it("en el Historial (listado) la página conserva el scroll normal", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ tab: "listado" }));
    const { container } = renderSalesPage();
    expect(container.querySelector(".sales-page-root")?.classList.contains("shell-fill-viewport")).toBe(false);
  });

  it("Cliente, total y cobro viven en el sidebar; tabs y contexto en el header", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ tab: "nuevo" }));
    const { container } = renderSalesPage();
    const sidebar = container.querySelector(".sf-sidebar");
    expect(container.querySelector(".sf-ophead .prd-tabs")).toBeTruthy();
    expect(sidebar?.querySelector(".sf-sidebar__section--customer")).toBeTruthy();
    expect(sidebar?.querySelector(".sf-total-box")).toBeTruthy();
    expect(sidebar?.textContent).toContain("Formas de Cobro");
    expect(sidebar?.querySelector(".sf-tax-table")).toBeTruthy();
    expect(sidebar?.querySelector(".prd-tabs")).toBeNull();
    expect(screen.getByRole("button", { name: /Emitir/ })).toBeTruthy();
  });

  it("contador discreto de líneas en la barra inferior (singular / plural)", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ tab: "nuevo", lines: [{ _key: 1 }] as never }));
    const { rerender } = renderSalesPage();
    expect(screen.getByText("1 producto")).toBeTruthy();
    useSalesPageMock.mockReturnValue(
      buildCtx({ tab: "nuevo", lines: [{ _key: 1 }, { _key: 2 }, { _key: 3 }] as never }),
    );
    rerender(
      <MemoryRouter>
        <SalesPage />
      </MemoryRouter>,
    );
    expect(screen.getByText("3 productos")).toBeTruthy();
  });

  it("sin líneas no muestra contador", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ tab: "nuevo", lines: [] as never }));
    renderSalesPage();
    expect(screen.queryByText(/\d+ productos?$/)).toBeNull();
  });
});
