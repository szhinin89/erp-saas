// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";

// ── Mocks de componentes pesados, salvo CustomerPicker: esta suite prueba
// específicamente la integración real SalesPage → CustomerPicker →
// ZHPickerSelectedValue de la acción "Editar datos" (SALES-DS-CUSTOMER-SELECTED-08). ──
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
    getBusinessPartner: vi.fn().mockResolvedValue({
      id: "cust-1",
      identificationNumber: "1710034065",
      tradeName: "",
      legalName: "Juan Pérez",
      isActive: true,
    }),
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
    editing: null,
    hasInsufficientStock: false,

    form: {},
    register: vi.fn(),
    control: {},
    errors: {},
    formWatch: {
      docTypeCode: "",
      sriPaymentMethodCode: "",
      customerId: "cust-1",
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

    customerProfile: {
      name: "Juan Pérez",
      taxId: "1710034065",
      identificationType: "05",
      address: "Av. Siempre Viva 742",
      email: "juan@example.com",
      phone: "0999999999",
      installments: 1,
      daysBetweenInstallments: 0,
    },
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

describe("SalesPage — cliente seleccionado + Editar datos integrado (SALES-DS-CUSTOMER-SELECTED-08)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("el cliente seleccionado se muestra vía ZHPickerSelectedValue", async () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const title = await screen.findByText("Juan Pérez");
    expect(title.closest(".zh-picker-selected-value")).toBeTruthy();
  });

  it('"Cambiar cliente" sigue funcionando (dispara handleCustomerChange(null))', async () => {
    const handleCustomerChange = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ handleCustomerChange }));
    renderSalesPage();

    const btn = await screen.findByTitle("Cambiar cliente");
    fireEvent.click(btn);

    expect(handleCustomerChange).toHaveBeenCalledWith(null);
  });

  it('"Editar datos" se muestra como acción integrada del valor seleccionado', async () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const btn = await screen.findByTitle("Editar datos");
    expect(btn.closest(".zh-picker-selected-value__actions")).toBeTruthy();
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "edit",
    );
  });

  it('click en "Editar datos" invoca el mismo handler anterior (openEditCustomerModal)', async () => {
    const openEditCustomerModal = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ openEditCustomerModal }));
    renderSalesPage();

    const btn = await screen.findByTitle("Editar datos");
    fireEvent.click(btn);

    expect(openEditCustomerModal).toHaveBeenCalledTimes(1);
  });

  it('no queda ningún link/botón "Editar datos" suelto fuera de ZHPickerSelectedValue', async () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    await screen.findByText("Juan Pérez");
    const matches = screen.getAllByTitle("Editar datos");
    expect(matches).toHaveLength(1);
    expect(
      container.querySelector(".sales-form-customer-profile .zh-inline-action"),
    ).toBeNull();
  });

  it("no hay estilos inline en la tarjeta de cliente seleccionado", async () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    const { container } = renderSalesPage();

    await screen.findByText("Juan Pérez");
    const card = container.querySelector(".zh-picker-selected-value")!;
    card.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
    expect(card.getAttribute("style")).toBeNull();
  });

  it("no muestra Editar datos cuando no hay customerProfile", async () => {
    useSalesPageMock.mockReturnValue(buildCtx({ customerProfile: null }));
    renderSalesPage();

    await screen.findByText("Juan Pérez");
    expect(screen.queryByTitle("Editar datos")).toBeNull();
  });
});
