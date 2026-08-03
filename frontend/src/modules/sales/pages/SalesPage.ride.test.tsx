// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { SalesPageContext } from "../hooks/useSalesPage";
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

vi.mock("../hooks/useRideActions", () => ({
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

  return { ...base, ...overrides } as unknown as SalesPageContext;
}

describe("SalesPage — acciones de RIDE", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  describe("grilla (listado)", () => {
    it('muestra "Ver RIDE" solo en la fila de una factura Authorized', () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "listado",
          listItems: [
            {
              id: "inv-authorized",
              invoiceNumber: "001-001-000000001",
              issueDate: "2026-07-01",
              customerId: "c1",
              customerName: "A",
              status: "Authorized",
              lineCount: 1,
              grandTotal: 10,
              createdAt: "",
            },
            {
              id: "inv-draft",
              invoiceNumber: "001-001-000000002",
              issueDate: "2026-07-01",
              customerId: "c2",
              customerName: "B",
              status: "Draft",
              lineCount: 1,
              grandTotal: 10,
              createdAt: "",
            },
          ],
        }),
      );

      renderSalesPage();

      const rideButtons = screen.getAllByTitle("Ver RIDE");
      expect(rideButtons).toHaveLength(1);
    });

    it("no muestra ninguna acción de RIDE cuando no hay facturas Authorized", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "listado",
          listItems: [
            {
              id: "inv-cancelled",
              invoiceNumber: "001-001-000000003",
              issueDate: "2026-07-01",
              customerId: "c3",
              customerName: "C",
              status: "Cancelled",
              lineCount: 1,
              grandTotal: 10,
              createdAt: "",
            },
          ],
        }),
      );

      renderSalesPage();

      expect(screen.queryByTitle("Ver RIDE")).toBeNull();
    });
  });

  describe("detalle (bottom-bar)", () => {
    it("muestra Ver/Descargar/Regenerar RIDE cuando la factura editada está Authorized", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "nuevo",
          editing: buildInvoice({
            status: "Authorized",
            electronicStatus: "Authorized",
          }),
        }),
      );

      renderSalesPage();

      expect(screen.getByText("Ver RIDE")).not.toBeNull();
      expect(screen.getByText("Descargar RIDE")).not.toBeNull();
      expect(screen.getByText("Regenerar RIDE")).not.toBeNull();
    });

    it("no muestra ninguna acción de RIDE mientras la factura está en Draft", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "nuevo",
          isDraft: true,
          readOnly: false,
          fieldDisabled: false,
          editing: buildInvoice({ status: "Draft", electronicStatus: "None" }),
        }),
      );

      renderSalesPage();

      expect(screen.queryByText("Ver RIDE")).toBeNull();
      expect(screen.queryByText("Descargar RIDE")).toBeNull();
      expect(screen.queryByText("Regenerar RIDE")).toBeNull();
    });

    it("no muestra ninguna acción de RIDE para una factura sin editar (nueva)", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "nuevo",
          isDraft: true,
          readOnly: false,
          fieldDisabled: false,
          editing: null,
        }),
      );

      renderSalesPage();

      expect(screen.queryByText("Ver RIDE")).toBeNull();
    });

    it('el click en "Ver RIDE" invoca handleViewRide con el id de la factura', () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({
          tab: "nuevo",
          editing: buildInvoice({
            id: "inv-777",
            status: "Authorized",
            electronicStatus: "Authorized",
          }),
        }),
      );

      renderSalesPage();
      screen.getByText("Ver RIDE").click();

      expect(handleViewRideMock).toHaveBeenCalledWith("inv-777");
    });
  });

  describe("aviso de estado de caja", () => {
    it("con caja abierta y sin error: no muestra ningún aviso", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({ hasCashSession: true, cashSessionCheckError: null }),
      );

      renderSalesPage();

      expect(screen.queryByText(/No tiene una caja abierta/)).toBeNull();
      expect(screen.queryByText("Reintentar")).toBeNull();
    });

    it("sin caja abierta (confirmado por el backend): muestra el aviso real, sin botón Reintentar", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({ hasCashSession: false, cashSessionCheckError: null }),
      );

      renderSalesPage();

      expect(
        screen.getByText(
          "No tiene una caja abierta. Debe abrir una caja antes de autorizar facturas.",
        ),
      ).not.toBeNull();
      expect(screen.queryByText("Reintentar")).toBeNull();
    });

    it("error de permiso (403 sin scope): muestra el mensaje de permiso, nunca el de 'sin caja', y ofrece Reintentar", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({ hasCashSession: null, cashSessionCheckError: "permission" }),
      );

      renderSalesPage();

      expect(
        screen.getByText(
          "No se pudo verificar la caja abierta por falta de permiso para consultar caja.",
        ),
      ).not.toBeNull();
      expect(
        screen.queryByText(
          "No tiene una caja abierta. Debe abrir una caja antes de autorizar facturas.",
        ),
      ).toBeNull();
      expect(screen.getByText("Reintentar")).not.toBeNull();
    });

    it("error de contexto (empresa/sucursal): muestra el mensaje correspondiente", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({ hasCashSession: null, cashSessionCheckError: "context" }),
      );

      renderSalesPage();

      expect(
        screen.getByText(
          "No se pudo verificar la caja abierta por contexto incompleto de empresa/sucursal.",
        ),
      ).not.toBeNull();
    });

    it("error de servidor/red: muestra el mensaje de reintento/conexión", () => {
      useSalesPageMock.mockReturnValue(
        buildCtx({ hasCashSession: null, cashSessionCheckError: "server" }),
      );

      renderSalesPage();

      expect(
        screen.getByText(
          "No se pudo verificar la caja abierta. Reintente o revise conexión/servidor.",
        ),
      ).not.toBeNull();
    });

    it('"Reintentar" invoca refreshCashSession sin recargar la página', () => {
      const refreshCashSession = vi.fn();
      useSalesPageMock.mockReturnValue(
        buildCtx({
          hasCashSession: null,
          cashSessionCheckError: "server",
          refreshCashSession,
        }),
      );

      renderSalesPage();
      screen.getByText("Reintentar").click();

      expect(refreshCashSession).toHaveBeenCalledTimes(1);
    });
  });
});
