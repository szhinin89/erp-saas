// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

// Nueva compra — Fase A (reapertura visual puntual de Compras V1): distribución de la cabecera,
// disclosures oficiales (ZHBtn expanded) y barra del detalle. No valida CSS/layout (jsdom): solo
// estructura, handlers, estados disabled y atributos de accesibilidad conservados.

const state = vi.hoisted(() => ({ ctx: {} as Record<string, unknown> }));

vi.mock("../hooks/usePurchasesPage", () => ({ usePurchasesPage: () => state.ctx }));
vi.mock("../../../templates/ErpPageTemplate", () => ({
  ErpPageTemplate: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
vi.mock("../components/PurchaseFormFeedback", () => ({ PurchaseFormFeedback: () => null }));
vi.mock("../components/PurchaseSummaryPanel", () => ({ PurchaseSummaryPanel: () => null }));
vi.mock("../components/ResolvePendingProductsModal", () => ({ ResolvePendingProductsModal: () => null }));
vi.mock("../components/DistributeCostModal", () => ({ DistributeCostModal: () => null }));
vi.mock("../components/PurchaseLineBreakdownModal", () => ({ PurchaseLineBreakdownModal: () => null }));
vi.mock("../components/PurchaseConfirmChecklist", () => ({ PurchaseConfirmChecklist: () => null }));
vi.mock("../components/PurchaseRetentionDraft", () => ({ PurchaseRetentionDraft: () => null }));
vi.mock("../../../components/items/ItemEditorModal/ItemEditorModal", () => ({ ItemEditorModal: () => null }));
vi.mock("../../masterData/facades/supplierPickerFacade", () => ({
  SupplierSearchSelect: () => <input aria-label="supplier-search" />,
}));
vi.mock("../../retentions/facades/purchaseRetentionFacade", () => ({
  RetentionAnnulmentPanel: () => null,
  RetentionElectronicStatusBadge: () => null,
  RetentionSriServiceHealth: () => null,
}));
vi.mock("../../items/facades/itemDetailNavigationFacade", () => ({ useOpenItemDetail: () => vi.fn() }));

import { PurchasesPage } from "./PurchasesPage";

type Overrides = Record<string, unknown>;

function buildCtx(overrides: Overrides = {}, formOverrides: Overrides = {}) {
  const ctx: Record<string, unknown> = {
    tab: "nuevo",
    editing: null,
    readOnly: false,
    fieldDisabled: false,
    saving: false,
    saveError: "",
    saveErrorDetails: [],
    errors: {},
    lines: [],
    xmlLinesSummary: { total: 0, warnings: 0 },
    vatRatesMap: {},
    iceRatesMap: {},
    modalConfirm: false,
    modalDistributeCost: false,
    modalPendingProducts: false,
    modalCancelReason: false,
    modalDiscardDraft: false,
    pendingLeave: false,
    sriAnnulmentReason: null,
    breakdownSourceId: null,
    retentionIntent: { appliesRetention: false },
    whPreview: null,
    retention: null,
    formWatch: {
      supplierId: "",
      docTypeCode: "01",
      invoiceNumber: "001-001-000000123",
      issueDate: "2026-10-01",
      globalWarehouseId: "",
      accessKey: "",
      authorizationNumber: "",
      authorizationDate: "",
      notes: "",
      sriPaymentMethodCode: "",
      taxSupportCode: "",
      paymentTermId: "",
      ...formOverrides,
    },
    supplierProfile: null,
    isSupplierInactiveBlocking: false,
    supplierInactiveMessage: "",
    supplierInactiveDetail: "",
    handleSupplierChange: vi.fn(),
    sriDocTypes: [{ code: "01", name: "Factura" }],
    sriDocTypesLoaded: true,
    sriDocTypesUnavailable: false,
    canUseSriDocTypes: true,
    sriPaymentMethods: [],
    sriTaxSupports: [],
    paymentTermsList: [],
    warehouses: [
      { id: "wh-1", code: "B01", name: "Bodega principal" },
      { id: "wh-2", code: "B02", name: "Bodega norte" },
    ],
    register: () => ({ name: "field", onChange: vi.fn(), onBlur: vi.fn(), ref: vi.fn() }),
    setValue: vi.fn(),
    handlePaymentTermChange: vi.fn(),
    applyGlobalWarehouse: vi.fn(),
    showElectronic: false,
    setShowElectronic: vi.fn(),
    showNotes: false,
    setShowNotes: vi.fn(),
    globalSearchRef: { current: null },
    globalQuery: "",
    setGlobalQuery: vi.fn(),
    globalOpen: false,
    setGlobalOpen: vi.fn(),
    handleGlobalKeyDown: vi.fn(),
    globalResults: [],
    globalFocusIdx: 0,
    setGlobalFocusIdx: vi.fn(),
    globalFilter: "all",
    setGlobalFilter: vi.fn(),
    globalSearchStatus: "idle",
    retryGlobalSearch: vi.fn(),
    itemTypeOptions: [{ id: "t1", name: "Bien" }],
    setModalDistributeCost: vi.fn(),
    distributeCostDisabledReason: undefined,
    pendingProductSources: [],
    pendingReceptionItems: [],
    addLine: vi.fn(),
    addLineWithItem: vi.fn(),
    requestLeave: vi.fn(),
    resetForm: vi.fn(),
    setTab: vi.fn(),
    handleSave: vi.fn(),
    hasLineSaveBlockers: false,
    isDuplicateAccessKeyBlocking: false,
    summaryTotals: {},
    // PaymentScheduleSection
    hasPersistedSchedule: false,
    ptLoaded: true,
    isDraft: true,
    ptInstallments: 1,
    ptDaysBetween: 0,
    ptRows: [],
    ptRowsSum: 0,
    ptMismatch: false,
    localTotal: 0,
    regenerateSchedule: vi.fn(),
    addInstallment: vi.fn(),
    removeInstallment: vi.fn(),
    setPtInstallments: vi.fn(),
    setPtDaysBetween: vi.fn(),
    updateScheduleRow: vi.fn(),
    ...overrides,
  };
  return ctx;
}

function renderPage(overrides: Overrides = {}, formOverrides: Overrides = {}) {
  state.ctx = buildCtx(overrides, formOverrides);
  const view = render(
    <MemoryRouter>
      <I18nProvider>
        <PurchasesPage />
      </I18nProvider>
    </MemoryRouter>,
  );
  return { ...view, ctx: state.ctx as Record<string, ReturnType<typeof vi.fn>> & Record<string, unknown> };
}

// El select de bodega se identifica por sus opciones (el label también contiene el ícono de ayuda).
function warehouseSelect(scope: HTMLElement) {
  const found = within(scope).getAllByRole("combobox").find((el) =>
    Array.from((el as HTMLSelectElement).options).some((o) => o.value === "wh-2"));
  expect(found, "select de Bodega destino dentro de «Datos del documento»").toBeDefined();
  return found as HTMLSelectElement;
}

beforeEach(() => setPrecisionPolicyForTests(TEST_PRECISION_POLICY));
afterEach(cleanup);

describe("Nueva compra — cabecera (Fase A)", () => {
  it("no muestra la tarjeta «Logística y totales» y conserva los ids de foco", () => {
    const { container } = renderPage();
    expect(screen.queryByText("Logística y totales")).toBeNull();
    expect(container.querySelector(".pf-header-cards-row--mid")).toBeNull();
    expect(container.querySelector(".pf-header-cards-row--purchase")?.children).toHaveLength(3);
    for (const id of ["purchase-supplier", "purchase-document", "purchase-lines", "purchase-schedule"]) {
      expect(container.querySelector(`#${id}`), id).not.toBeNull();
    }
  });

  it("«Bodega destino» vive en «Datos del documento» y conserva su handler", () => {
    const { container, ctx } = renderPage();
    const documentCard = container.querySelector<HTMLElement>("#purchase-document")!;
    const warehouse = warehouseSelect(documentCard);
    fireEvent.change(warehouse, { target: { value: "wh-2" } });
    expect(ctx.applyGlobalWarehouse).toHaveBeenCalledWith("wh-2");
    expect(warehouse.disabled).toBe(false);
  });

  it("«Bodega destino» sigue deshabilitada en modo lectura (fieldDisabled)", () => {
    const { container } = renderPage({ fieldDisabled: true, readOnly: true });
    const warehouse = warehouseSelect(container.querySelector<HTMLElement>("#purchase-document")!);
    expect(warehouse.disabled).toBe(true);
  });
});

describe("Nueva compra — disclosures (Fase A)", () => {
  it("usa ZHBtn expanded con aria-expanded/aria-controls y alterna con clic", () => {
    const { ctx } = renderPage();
    const electronic = screen.getByRole("button", { name: /Información electrónica/i });
    const notes = screen.getByRole("button", { name: /Observaciones/i });
    expect(electronic.getAttribute("aria-expanded")).toBe("false");
    expect(electronic.getAttribute("aria-controls")).toBe("purchase-electronic-panel");
    expect(notes.getAttribute("aria-expanded")).toBe("false");
    expect(notes.getAttribute("aria-controls")).toBe("purchase-notes-panel");
    fireEvent.click(electronic);
    fireEvent.click(notes);
    expect(ctx.setShowElectronic).toHaveBeenCalledTimes(1);
    expect(ctx.setShowNotes).toHaveBeenCalledTimes(1);
    // Alternancia funcional (v => !v), idéntica a la previa.
    const toggle = (ctx.setShowElectronic as ReturnType<typeof vi.fn>).mock.calls[0][0] as (v: boolean) => boolean;
    expect(toggle(false)).toBe(true);
    expect(toggle(true)).toBe(false);
  });

  it("muestra los paneles con su id cuando están abiertos y oculta los indicadores", () => {
    const { container } = renderPage(
      { showElectronic: true, showNotes: true },
      { accessKey: "1".repeat(49), notes: "nota" },
    );
    expect(screen.getByRole("button", { name: /Información electrónica/i }).getAttribute("aria-expanded")).toBe("true");
    expect(container.querySelector("#purchase-electronic-panel")).not.toBeNull();
    expect(container.querySelector("#purchase-notes-panel")).not.toBeNull();
    expect(screen.queryByText("Con datos")).toBeNull();
    expect(screen.queryByText("Con notas")).toBeNull();
  });

  it("conserva los indicadores «Con datos» / «Con notas» con los paneles cerrados", () => {
    renderPage({}, { accessKey: "1".repeat(49), notes: "nota" });
    expect(screen.getByText("Con datos")).toBeTruthy();
    expect(screen.getByText("Con notas")).toBeTruthy();
  });

  it("no queda ningún control de disclosure local (pf-collapsible__toggle)", () => {
    const { container } = renderPage();
    expect(container.querySelector(".pf-collapsible__toggle")).toBeNull();
  });
});

describe("Nueva compra — barra del detalle de productos (Fase A)", () => {
  it("separa el título de la barra de herramientas y conserva handlers", () => {
    const { container, ctx } = renderPage();
    const topbar = container.querySelector<HTMLElement>(".pdl-topbar")!;
    const toolbar = topbar.querySelector<HTMLElement>(".pdl-toolbar")!;
    expect(topbar.querySelector(".pdl-topbar__title")?.parentElement).toBe(topbar);
    expect(toolbar.querySelector(".pdl-search__wrap")).not.toBeNull();
    expect(toolbar.querySelector("select.pdl-type-filter-select")).not.toBeNull();
    const distribute = within(toolbar).getByRole("button", { name: /Distribuir flete/i });
    fireEvent.click(distribute);
    expect(ctx.setModalDistributeCost).toHaveBeenCalledWith(true);
  });

  it("muestra «Resolver productos pendientes» solo con pendientes y editable", () => {
    const { container, rerender } = renderPage({ pendingProductSources: [{}, {}] });
    expect(container.querySelector(".pdl-toolbar__actions")?.textContent).toMatch(/pendientes|2/i);
    cleanup();
    const second = renderPage({ pendingProductSources: [{}, {}], fieldDisabled: true });
    expect(second.container.querySelector(".pdl-toolbar__actions")?.textContent).not.toMatch(/pendientes/i);
    void rerender;
  });

  it("conserva el estado disabled y el motivo de «Distribuir flete/gasto»", () => {
    const { container } = renderPage({ distributeCostDisabledReason: "Guarde la compra primero." });
    const toolbar = container.querySelector<HTMLElement>(".pdl-toolbar")!;
    const distribute = within(toolbar).getByRole("button", { name: /Distribuir flete/i }) as HTMLButtonElement;
    expect(distribute.disabled).toBe(true);
    const reason = screen.getByRole("button", { name: /Ver motivo/i });
    expect(reason.closest(".pdl-toolbar")).toBeNull();
    fireEvent.click(reason);
    expect(screen.getByRole("status").textContent).toContain("Guarde la compra primero.");
  });
});
