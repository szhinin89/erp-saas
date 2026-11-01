// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, cleanup } from "@testing-library/react";
import { renderSalesUi as render } from "../test/renderSalesUi";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";
import type { SalesInvoiceDto } from "../api/salesService";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";

// Salud técnica SRI en /sales: el indicador visible es el componente compartido
// ZHSriServiceHealth (monitor central, Recepción/Autorización + ambiente) — puramente
// informativo, nunca bloquea la captura de productos ni la emisión (ver useSalesPage.ts:
// refreshSriConnectivity solo advierte con message.warning, no cambia canEmit).
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

type SriServiceHealthDto = NonNullable<
  ReturnType<typeof useElectronicInvoicingStatusStore.getState>["health"]
>;

function setHealth(overrides: Partial<SriServiceHealthDto> = {}) {
  const health: SriServiceHealthDto = {
    environment: "Test",
    environmentName: "Pruebas",
    reception: {
      state: "Available",
      reason: "RecentResponses",
      evidence: "Call",
      lastObservedAtUtc: "2026-11-01T15:00:00Z",
    },
    authorization: {
      state: "Unverified",
      reason: "NoRecentEvidence",
      evidence: "None",
      lastObservedAtUtc: null,
    },
    ...overrides,
  };
  // healthLoadedAt vigente → refreshHealth() es no-op (sin red en el test).
  useElectronicInvoicingStatusStore.setState({ health, healthLoadedAt: Date.now() });
}

describe("SalesPage — salud técnica SRI (ZHSriServiceHealth)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setHealth();
  });

  afterEach(() => {
    cleanup();
    useElectronicInvoicingStatusStore.getState().clear();
  });

  it("muestra ambiente y estado de Recepción/Autorización por separado", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText("Pruebas")).toBeTruthy();
    expect(screen.getByTestId("sri-health-reception").textContent).toBe("Recepción: Disponible");
    expect(screen.getByTestId("sri-health-authorization").textContent).toBe(
      "Autorización: Sin verificar",
    );
  });

  it("incluye motivo, última observación y la aclaración fiscal en el tooltip", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const title = screen.getByTestId("sri-health-reception").getAttribute("title")!;
    expect(title).toContain("El SRI respondió correctamente");
    expect(title).toContain("Última observación:");
    expect(title).toContain("no indica si un comprobante fue autorizado");
  });

  it("muestra un único indicador SRI (sin el badge básico anterior)", () => {
    setHealth({
      reception: {
        state: "Intermittent",
        reason: "ProbeUnreachable",
        evidence: "Probe",
        lastObservedAtUtc: "2026-11-01T15:00:00Z",
      },
    });
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByTestId("sri-health-reception").textContent).toBe("Recepción: Intermitente");
    expect(screen.queryByText("SRI no disponible")).toBeNull();
    expect(screen.queryByText("SRI disponible")).toBeNull();
    expect(screen.queryByText("SRI no verificado")).toBeNull();
  });

  it("no muestra el indicador cuando la factura no es electrónica (Physical no necesita SRI)", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ isElectronic: false }));
    renderSalesPage();

    expect(screen.queryByTestId("sri-health-reception")).toBeNull();
    expect(screen.queryByTestId("sri-health-authorization")).toBeNull();
  });

  it("SRI no disponible NO deshabilita el botón Emitir (solo advierte, no bloquea) — canEmit sigue siendo la única autoridad", () => {
    setHealth({
      reception: {
        state: "Unavailable",
        reason: "ConsecutiveTransportFailures",
        evidence: "Call",
        lastObservedAtUtc: "2026-11-01T15:00:00Z",
      },
    });
    const openIssueFlow = vi.fn();
    useSalesPageMock.mockReturnValue(buildCtx({ canEmit: true, openIssueFlow }));
    renderSalesPage();

    const btn = screen
      .getByText("Emitir Factura Electrónica (F8)")
      .closest("button")!;
    expect(btn.disabled).toBe(false);
    btn.click();
    expect(openIssueFlow).toHaveBeenCalledTimes(1);
  });

  it("no hay estilos inline en el indicador de salud SRI", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const bar = screen.getByTestId("sri-health-reception").closest(".sf-bottombar__sri");
    expect(bar).not.toBeNull();
    bar!.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
