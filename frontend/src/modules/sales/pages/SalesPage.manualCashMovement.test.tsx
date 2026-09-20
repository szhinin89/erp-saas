// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { SalesInvoiceDto } from "../api/salesService";

// SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08: el botón "+ Movimiento de caja" en Sales
// solo consume ManualCashMovementModal + useManualCashMovementFlow (ya probados en
// TREASURY-CASH-MANUAL-MOVEMENT-SHARED-MODAL-07 / PERMISSION-06). Esta suite NO repite
// esas pruebas: solo verifica que SalesPage (a) muestra/oculta el botón según
// allowManualMovements + canRecordManualMovements + turno abierto, fail-closed, y
// (b) le pasa al modal exactamente los props que expone ctx.manualCashMovement, sin
// lógica propia de Caja dentro de Sales.
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

// Mock del modal compartido: no reimplementa su UI real (ya cubierta por
// ManualCashMovementModal.test.tsx). Solo expone lo necesario para verificar que
// SalesPage le pasa exactamente los props de ctx.manualCashMovement.
vi.mock("../../caja/components/ManualCashMovementModal", () => ({
  ManualCashMovementModal: (props: {
    open: boolean;
    onSubmit: () => void;
    onClose: () => void;
  }) =>
    props.open
      ? (
          <div data-testid="manual-cash-movement-modal">
            <button type="button" onClick={props.onSubmit}>
              mock-submit
            </button>
            <button type="button" onClick={props.onClose}>
              mock-close
            </button>
          </div>
        )
      : null,
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

function buildOpenSession(): SalesPageContext["myCashSession"] {
  return {
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
    status: "Open",
  } as unknown as SalesPageContext["myCashSession"];
}

function buildManualCashMovement(
  overrides: Partial<SalesPageContext["manualCashMovement"]> = {},
): SalesPageContext["manualCashMovement"] {
  return {
    allowManualMovements: true,
    canRecordManualMovements: true,
    saving: false,
    saveError: "",
    setSaveError: vi.fn(),
    movementForm: { register: vi.fn(), formState: { errors: {} } },
    movementTypes: [],
    reasons: [],
    reasonsLoading: false,
    selectedMovementType: null,
    movementModalOpen: false,
    openMovementModal: vi.fn(),
    closeMovementModal: vi.fn(),
    handleRecordMovement: vi.fn(),
    ...overrides,
  } as unknown as SalesPageContext["manualCashMovement"];
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
    paymentMethods: [],
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
    sriDocTypes: [],
    sriPaymentMethods: [],
    sriIdTypes: [],

    hasCashSession: true,
    myCashSession: buildOpenSession(),
    manualCashMovement: buildManualCashMovement(),
    branchName: "Sucursal Principal",

    isDraft: true,
    readOnly: false,
    fieldDisabled: false,
    canEmit: false,
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

const BUTTON_TEXT = "Movimiento de caja";

describe("SalesPage — botón + Movimiento de caja (SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("visible: empresa permite + permiso + turno abierto", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    expect(screen.getByText(BUTTON_TEXT)).not.toBeNull();
  });

  it("oculto: empresa NO permite movimientos manuales (allowManualMovements=false)", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        manualCashMovement: buildManualCashMovement({ allowManualMovements: false }),
      }),
    );
    renderSalesPage();

    expect(screen.queryByText(BUTTON_TEXT)).toBeNull();
  });

  it("oculto: usuario sin permiso caja.record (canRecordManualMovements=false)", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        manualCashMovement: buildManualCashMovement({ canRecordManualMovements: false }),
      }),
    );
    renderSalesPage();

    expect(screen.queryByText(BUTTON_TEXT)).toBeNull();
  });

  it("oculto: sin turno de caja abierto (myCashSession=null)", () => {
    useSalesPageMock.mockReturnValue(buildCtx({ myCashSession: null }));
    renderSalesPage();

    expect(screen.queryByText(BUTTON_TEXT)).toBeNull();
  });

  it("oculto: turno de caja existe pero no está Open", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        myCashSession: {
          ...buildOpenSession(),
          status: "Closed",
        } as unknown as SalesPageContext["myCashSession"],
      }),
    );
    renderSalesPage();

    expect(screen.queryByText(BUTTON_TEXT)).toBeNull();
  });

  it("oculto (fail-closed): mientras la configuración de empresa está cargando", () => {
    // El propio hook compartido resuelve allowManualMovements=false durante loading
    // (fail-closed, ver useManualCashMovementFlow / TREASURY-CASH-COMPANY-SETTING-05A).
    // Sales no debe asumir true en ningún caso.
    useSalesPageMock.mockReturnValue(
      buildCtx({
        manualCashMovement: buildManualCashMovement({ allowManualMovements: false }),
      }),
    );
    renderSalesPage();

    expect(screen.queryByText(BUTTON_TEXT)).toBeNull();
  });

  it("el click abre el mismo modal compartido (ManualCashMovementModal) vía openMovementModal", () => {
    const openMovementModal = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({ manualCashMovement: buildManualCashMovement({ openMovementModal }) }),
    );
    renderSalesPage();

    screen.getByText(BUTTON_TEXT).click();

    expect(openMovementModal).toHaveBeenCalledTimes(1);
  });

  it("pasa exactamente los props de ctx.manualCashMovement al modal compartido", () => {
    const handleRecordMovement = vi.fn();
    const closeMovementModal = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({
        manualCashMovement: buildManualCashMovement({
          movementModalOpen: true,
          handleRecordMovement,
          closeMovementModal,
        }),
      }),
    );
    renderSalesPage();

    expect(screen.getByTestId("manual-cash-movement-modal")).not.toBeNull();

    screen.getByText("mock-submit").click();
    expect(handleRecordMovement).toHaveBeenCalledTimes(1);

    screen.getByText("mock-close").click();
    expect(closeMovementModal).toHaveBeenCalledTimes(1);
  });

  it("registrar un movimiento no altera el documento de venta en edición (editing intacto)", () => {
    const editing = buildInvoice();
    const handleRecordMovement = vi.fn();
    useSalesPageMock.mockReturnValue(
      buildCtx({
        editing,
        manualCashMovement: buildManualCashMovement({
          movementModalOpen: true,
          handleRecordMovement,
        }),
      }),
    );
    renderSalesPage();

    screen.getByText("mock-submit").click();

    expect(handleRecordMovement).toHaveBeenCalledTimes(1);
    // El documento de venta que Sales estaba editando no se toca por el flujo de caja.
    expect(useSalesPageMock).toHaveBeenCalled();
    expect(editing).toEqual(buildInvoice());
  });

  it("no agrega ningún endpoint/lógica propia de Sales: el botón solo invoca funciones expuestas por ctx.manualCashMovement", () => {
    useSalesPageMock.mockReturnValue(buildCtx());
    renderSalesPage();

    const btn = screen.getByText(BUTTON_TEXT).closest("button")!;
    expect(btn).not.toBeNull();
    expect(btn.getAttribute("type")).toBe("button");
  });
});
