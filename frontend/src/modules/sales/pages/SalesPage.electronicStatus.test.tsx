// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { screen, cleanup } from "@testing-library/react";
import { renderSalesUi as render } from "../test/renderSalesUi";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";
import { resolveSalesEmissionType } from "../utils/salesEmissionType";
import type { SalesInvoiceDto, SalesListItemDto } from "../api/salesService";

// ── Mocks de componentes pesados: esta suite prueba únicamente la
// visibilidad condicional de las acciones de RIDE agregadas en SalesPage.tsx,
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

const handleViewRideMock = vi.fn();
const handleDownloadRideMock = vi.fn();
const handleRegenerateRideMock = vi.fn();

vi.mock("../../ride/facades/rideGenerationFacade", () => ({
  useRideActions: () => ({
    ridePending: false,
    handleViewRide: handleViewRideMock,
    handleDownloadRide: handleDownloadRideMock,
    handleRegenerateRide: handleRegenerateRideMock,
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

function buildInvoice(
  overrides: Partial<SalesInvoiceDto> = {},
): SalesInvoiceDto {
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
  overrides: Partial<SalesPageContext> & {
    listItems?: SalesListItemDto[];
  } = {},
): SalesPageContext {
  const base = {
    tab: "listado",
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

    isDraft: false,
    readOnly: true,
    fieldDisabled: true,
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

  return withPosDerivedCtx({ ...base, ...overrides });
}

describe("SalesPage - electronic invoice snapshot visibility", () => {
  afterEach(() => cleanup());

  function ctxFor(invoiceType: "Electronic" | "Physical", sessionType: "Electronic" | "Physical") {
    const editing = buildInvoice({
      status: "Authorized",
      emissionType: invoiceType,
      electronicStatus: invoiceType === "Electronic" ? "Authorized" : "None",
      accessKey: invoiceType === "Electronic" ? "0310202601179001234500110010010000000101234567818" : null,
    });
    const myCashSession = { emissionType: sessionType } as unknown as SalesPageContext["myCashSession"];
    return buildCtx({
      tab: "nuevo",
      isDraft: false,
      readOnly: true,
      editing,
      myCashSession,
      isElectronic: resolveSalesEmissionType(editing, myCashSession) === "Electronic",
    });
  }

  it("factura ELECTRÓNICA histórica abierta desde una caja FÍSICA conserva RIDE, diagnóstico y clave de acceso", () => {
    useSalesPageMock.mockReturnValue(ctxFor("Electronic", "Physical"));
    renderSalesPage();
    expect(screen.getByText("Ver RIDE")).toBeTruthy();
    expect(screen.getByText("Descargar RIDE")).toBeTruthy();
    expect(screen.getByText("Regenerar RIDE")).toBeTruthy();
    expect(screen.getByTitle("Ver diagnóstico SRI")).toBeTruthy();
    expect(screen.getByText("Clave de Acceso SRI")).toBeTruthy();
  });

});

// SALES-ELECTRONIC-STATUS-CLARITY-01 — el estado comercial («Confirmada») y el estado SRI real se
// muestran por separado; «Autorizada por SRI» solo cuando el documento electrónico lo está.
describe("SalesPage — claridad entre estado de la venta y estado SRI", () => {
  afterEach(() => cleanup());

  const listRow = (
    id: string,
    electronicStatus: string,
    emissionType = "Electronic",
  ): SalesListItemDto => ({
    id,
    invoiceNumber: `001-001-${id}`,
    issueDate: "2026-07-01",
    customerId: "c1",
    customerName: "A",
    status: "Authorized",
    lineCount: 1,
    grandTotal: 10,
    createdAt: "",
    emissionType,
    electronicStatus,
  });

  it("listado: columnas separadas «Estado venta» / «Estado SRI» con el estado electrónico real", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        tab: "listado",
        listItems: [
          listRow("000000015", "Draft"),
          listRow("000000016", "Signed"),
          listRow("000000017", "Authorized"),
          listRow("000000018", "None", "Physical"),
        ],
      }),
    );
    renderSalesPage();

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toContain("Estado venta");
    expect(headers).toContain("Estado SRI");

    const commercial = screen.getAllByTestId("sales-commercial-status").map((b) => b.textContent);
    expect(commercial).toEqual(["Confirmada", "Confirmada", "Confirmada", "Confirmada"]);

    const electronic = screen.getAllByTestId("sales-electronic-status");
    expect(electronic.map((b) => b.textContent)).toEqual([
      "Sin emitir",
      "Firmada, sin autorizar",
      "Autorizada por SRI",
      "No aplica",
    ]);
    expect(electronic[0].getAttribute("title")).toBe(
      "Aún no tiene XML firmado ni autorización del SRI.",
    );
    // La autorización SRI aparece una sola vez (la única fila realmente autorizada).
    expect(screen.getAllByText("Autorizada por SRI")).toHaveLength(1);
    // La etiqueta comercial antigua «Autorizada» ya no se usa.
    expect(screen.queryByText("Autorizada")).toBeNull();
  });

  it("detalle: venta Confirmada con documento electrónico en Draft → «Sin emitir», nunca «Autorizada por SRI»", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        tab: "nuevo",
        isDraft: false,
        readOnly: true,
        editing: buildInvoice({ status: "Authorized", electronicStatus: "Draft" }),
      }),
    );
    renderSalesPage();

    expect(screen.getByTestId("sales-commercial-status").textContent).toBe("Confirmada");
    expect(screen.getByTestId("sales-electronic-status").textContent).toBe("Sin emitir");
    expect(screen.queryByText("Autorizada por SRI")).toBeNull();
  });

  it("detalle: documento electrónico autorizado → «Autorizada por SRI»", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        tab: "nuevo",
        isDraft: false,
        readOnly: true,
        editing: buildInvoice({ status: "Authorized", electronicStatus: "Authorized" }),
      }),
    );
    renderSalesPage();

    expect(screen.getByTestId("sales-electronic-status").textContent).toBe("Autorizada por SRI");
  });

  it("detalle de una venta física: muestra el estado de la venta y ningún estado SRI", () => {
    useSalesPageMock.mockReturnValue(
      buildCtx({
        tab: "nuevo",
        isDraft: false,
        readOnly: true,
        isElectronic: false,
        editing: buildInvoice({
          status: "Authorized",
          emissionType: "Physical",
          electronicStatus: "None",
        }),
      }),
    );
    renderSalesPage();

    expect(screen.getByTestId("sales-commercial-status").textContent).toBe("Confirmada");
    expect(screen.queryByTestId("sales-electronic-status")).toBeNull();
  });
});
