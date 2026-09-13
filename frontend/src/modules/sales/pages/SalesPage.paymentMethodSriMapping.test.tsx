// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto, PaymentMethodDto } from "../api/salesService";

// SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: la sección "Formas de Cobro" deriva automáticamente
// el código SRI (formaPago) de cada forma de cobro seleccionada desde PaymentMethod.sriPaymentMethodCode
// (mapeo real, nunca hardcodeado) — el cajero nunca necesita adivinar ni editar manualmente el
// select "Forma Pago SRI" de cabecera para que el XML salga correcto.
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
    paymentSchedule: [],
    isPaymentScheduleManual: false,
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
    code: "EFECTIVO",
    name: "Efectivo",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: false,
    sortOrder: 1,
    detailType: "None",
    sriPaymentMethodCode: "01",
    ...overrides,
  };
}

const CASH = buildPaymentMethod();
const TRANSFER = buildPaymentMethod({
  id: "pm-transfer",
  code: "TRANSFERENCIA",
  name: "Transferencia",
  requiresReference: true,
  detailType: "Transfer",
  sriPaymentMethodCode: "20",
});
const UNMAPPED = buildPaymentMethod({
  id: "pm-other",
  code: "OTRO",
  name: "Otro",
  sriPaymentMethodCode: null,
});

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
    paymentMethods: [CASH, TRANSFER, UNMAPPED],
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
    sriPaymentMethods: [
      { code: "01", name: "Sin utilización del sistema financiero" },
      { code: "20", name: "Otros con utilización del sistema financiero" },
    ],
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

  return { ...base, ...overrides } as unknown as SalesPageContext;
}

describe("SalesPage — mapeo automático PaymentMethod → Forma Pago SRI (SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("al agregar Efectivo con monto, muestra el SRI 01 derivado del mapeo", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [{ _key: 1, paymentMethodId: CASH.id, amount: 10, reference: null }],
      }),
    );
    renderSalesPage();

    expect(
      screen.getByText((_, el) => el?.textContent === "SRI 01 — Sin utilización del sistema financiero"),
    ).toBeTruthy();
  });

  it("al agregar Transferencia con monto, muestra el SRI 20 derivado del mapeo", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [{ _key: 1, paymentMethodId: TRANSFER.id, amount: 20, reference: null }],
      }),
    );
    renderSalesPage();

    expect(
      screen.getByText((_, el) => el?.textContent === "SRI 20 — Otros con utilización del sistema financiero"),
    ).toBeTruthy();
  });

  it("al cambiar de método de cobro, el resumen SRI mostrado cambia acorde", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [{ _key: 1, paymentMethodId: CASH.id, amount: 10, reference: null }],
      }),
    );
    const view = render(
      <MemoryRouter>
        <SalesPage />
      </MemoryRouter>,
    );
    expect(screen.getAllByText(/SRI 01/).length).toBeGreaterThan(0);

    useSalesPageMock.mockReturnValue(
      buildCtx({
        payments: [{ _key: 1, paymentMethodId: TRANSFER.id, amount: 10, reference: null }],
      }),
    );
    view.rerender(
      <MemoryRouter>
        <SalesPage />
      </MemoryRouter>,
    );
    expect(screen.getAllByText(/SRI 20/).length).toBeGreaterThan(0);
  });

  it("si la forma de cobro no tiene mapeo ni el header trae default de empresa, muestra advertencia", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        formWatch: {
          docTypeCode: "01",
          sriPaymentMethodCode: "",
          customerId: "cust-1",
        } as unknown as SalesPageContext["formWatch"],
        payments: [{ _key: 1, paymentMethodId: UNMAPPED.id, amount: 5, reference: null }],
      }),
    );
    renderSalesPage();

    expect(screen.getByText(/Sin forma de pago SRI configurada/)).toBeTruthy();
  });

  it("no fuerza al cajero a editar manualmente el select de cabecera Forma Pago SRI", () => {
    const setValue = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({
        setValue,
        payments: [{ _key: 1, paymentMethodId: CASH.id, amount: 10, reference: null }],
      }),
    );
    renderSalesPage();

    // El badge se calcula en el render, sin disparar ninguna escritura automática sobre
    // "sriPaymentMethodCode" — el select de cabecera queda intacto como modo avanzado opcional.
    expect(setValue).not.toHaveBeenCalledWith(
      "sriPaymentMethodCode",
      expect.anything(),
    );
  });
});

describe("SalesPage — label de cabecera aclara que Forma Pago SRI es solo el default/respaldo (SALES-SRI-PAYMENT-FALLBACK-LABEL-01)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  // SALES-POS-EMISSION-PANEL-SIMPLIFICATION-01: Tipo Documento / Forma Pago SRI por Defecto ya
  // no viven expandidos en el panel principal — se movieron al modal de detalle de
  // "Configuración de venta" (mismo form/ctx, sin segunda fuente de verdad). Los tests abren ese
  // modal ("Configuración" — único botón, ver SALES-POS-EMISSION-CONFIG-DUPLICATED-ACTIONS-01)
  // antes de buscar el label/select.
  function openConfigModal() {
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));
  }

  it("muestra el label actualizado 'Forma Pago SRI por Defecto' dentro del modal de configuración", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();
    openConfigModal();

    expect(screen.getByText("Forma Pago SRI por Defecto")).toBeTruthy();
    // El label anterior ("Forma Pago SRI" a secas, sin "por Defecto") ya no debe existir suelto.
    expect(screen.queryByText("Forma Pago SRI")).toBeNull();
  });

  it("expone un ícono de ayuda junto al label con el tooltip esperado", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();
    openConfigModal();

    const helpIcon = screen.getByLabelText("Forma Pago SRI por defecto");
    expect(helpIcon).toBeTruthy();
  });

  it("el tooltip explica que el default solo aplica sin mapeo SRI en la forma de cobro", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();
    openConfigModal();

    const helpIcon = screen.getByLabelText("Forma Pago SRI por defecto");
    fireEvent.mouseEnter(helpIcon);

    expect(
      screen.getByText(
        "Se usa solo si una forma de cobro no tiene mapeo SRI configurado.",
      ),
    ).toBeTruthy();
  });

  it("no cambia el comportamiento del select: sigue permitiendo elegir y disparar setValue", () => {
    const setValue = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ setValue }));
    renderSalesPage();
    openConfigModal();

    const select = screen.getByDisplayValue("01 — Sin utilización del sistema financiero");
    fireEvent.change(select, { target: { value: "20" } });

    expect(setValue).toHaveBeenCalledWith("sriPaymentMethodCode", "20");
  });
});
