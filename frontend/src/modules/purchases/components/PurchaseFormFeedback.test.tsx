// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { PurchaseLineCard } from "../pages/PurchasesPage";
import { lineCardContext, manualLineWithoutCommercialData, missingItemReadiness, unlinkedXmlLine } from "../pages/purchaseLineCard.fixture";
import { PurchaseFormFeedback } from "./PurchaseFormFeedback";
import { PurchaseRetentionDraft } from "./PurchaseRetentionDraft";

type Ctx = Parameters<typeof PurchaseFormFeedback>[0]["ctx"];
function context(overrides: Partial<Ctx> = {}): Ctx {
  const lines = [{ ...unlinkedXmlLine, _key: 1 }, { ...unlinkedXmlLine, _key: 2 }];
  return {
    lines, editing: { status: "Draft" }, errors: {}, saveError: "", saveErrorDetails: [],
    formWatch: { supplierId: "s", docTypeCode: "01", invoiceNumber: "001-001-1", issueDate: "2026-10-23", globalWarehouseId: "wh-1" },
    lineReadinessByKey: { 1: missingItemReadiness, 2: missingItemReadiness },
    pendingReceptionItems: ["A", "B"], hasLineReadinessBlockers: true, hasLineSaveBlockers: false,
    readOnly: false, canUseSriDocTypes: true, sriDocTypesUnavailable: false,
    isDuplicateAccessKeyBlocking: false, isSupplierInactiveBlocking: false,
    duplicateAccessKeyTitle: "No se puede crear esta compra.", duplicateAccessKeyDetail: "Clave duplicada.",
    lineReadinessBlockedTitle: "Puede guardar el borrador, pero debe resolver las líneas marcadas antes de confirmar.",
    confirmBlockedTitle: "No se puede confirmar la compra: hay líneas por resolver.",
    scheduleBlocksConfirm: false, hasUnsavedFormChanges: false,
    documentState: { kind: "draftIncomplete", reasons: [{ code: "linesToResolve", count: 2 }] },
    retentionIntent: { appliesRetention: false, emissionPointId: "", issueDate: "2026-10-23" },
    whPreview: null, whPreviewError: null, retention: null, vatRatesMap: { "4": 15 }, iceRatesMap: {},
    fieldDisabled: false, warehouses: [], matchingKey: null, requestLeave: vi.fn(),
    ...overrides,
  } as unknown as Ctx;
}
function show(ctx: Ctx, children?: React.ReactNode) {
  return render(<MemoryRouter><I18nProvider><PurchaseFormFeedback ctx={ctx} />{children}</I18nProvider></MemoryRouter>);
}
beforeEach(() => {
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => { callback(0); return 1; });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe("P1 - one form summary", () => {
  it("distinguishes a saveable draft and navigates to every pending line's real tag", () => {
    const ctx = context();
    show(ctx, <>{ctx.lines.map((line, index) => <PurchaseLineCard key={line._key} line={line} idx={index} ctx={ctx} />)}</>);
    expect(screen.getByText("Puede guardar · no confirmar")).toBeTruthy();
    expect(screen.queryByText(ctx.lineReadinessBlockedTitle)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Incidencias (2)" }));
    const targets = within(document.getElementById(screen.getByRole("button", { name: "Incidencias (2)" }).getAttribute("aria-controls")!)!).getAllByRole("button");
    expect(targets).toHaveLength(2);
    targets.forEach((target, index) => {
      fireEvent.click(target);
      expect(document.activeElement?.closest(".pdl-line")?.id).toBe(`purchase-line-${index + 1}`);
      expect(document.activeElement?.closest(".zh-line-issues")).toBeTruthy();
    });
  });
  it("does not claim a draft is saveable when an existing save blocker is present", () => {
    show(context({ hasLineSaveBlockers: true }));
    expect(screen.getByText("No permite guardar")).toBeTruthy();
    expect(screen.queryByText("Puede guardar · no confirmar")).toBeNull();
  });
  it("renders a duplicate-SRI rejection once with its corrective action", () => {
    show(context({ isDuplicateAccessKeyBlocking: true, saveError: "No se puede crear esta compra.",
      saveErrorDetails: ["Clave duplicada."], duplicateAccessKey: { purchaseId: "existing" } as Ctx["duplicateAccessKey"] }));
    expect(screen.getAllByText("No se puede crear esta compra.")).toHaveLength(1);
    expect(screen.getAllByText("Clave duplicada.")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Ver compra existente" })).toBeTruthy();
  });
  it("keeps a general server rejection once and deduplicates its detail", () => {
    show(context({ saveError: "Rechazo del servidor", saveErrorDetails: ["Rechazo del servidor", "Motivo público", "Motivo público"] }));
    expect(screen.getAllByText("Rechazo del servidor")).toHaveLength(1);
    expect(screen.getAllByText("Motivo público")).toHaveLength(1);
  });
  it("unifies read-only status, date and reason, without offering to save pending historical lines", () => {
    show(context({ readOnly: true, editing: { status: "Cancelled", cancelledAt: "2026-10-23T16:00:00Z", cancelReason: "Documento duplicado" } as Ctx["editing"] }));
    expect(screen.getAllByText("Anulada")).toHaveLength(1);
    expect(screen.getByText("Solo lectura")).toBeTruthy();
    expect(screen.getByText(/23\/10\/2026.*Documento duplicado/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Incidencias/ })).toBeNull();
    expect(screen.queryByText("Puede guardar · no confirmar")).toBeNull();
  });
  it("leaves field errors only at the P0 field location", () => {
    const ctx = context({ errors: { lines: [{ quantity: { type: "server", message: "Cantidad rechazada" } }] } });
    show(ctx, <PurchaseLineCard line={ctx.lines[0]} idx={0} ctx={ctx} />);
    expect(screen.getAllByText("Cantidad rechazada")).toHaveLength(1);
    const field = screen.getByLabelText("Cantidad a registrar");
    expect(document.getElementById(field.getAttribute("aria-describedby")!)?.textContent).toBe("Cantidad rechazada");
  });
  it("follows the existing document state: ready draft, new purchase and unsaved local changes", () => {
    const ready = { lines: [], lineReadinessByKey: {}, pendingReceptionItems: [], hasLineReadinessBlockers: false };
    show(context({ ...ready, lines: [{ ...unlinkedXmlLine, _key: 1, purchaseReceptionLineId: null, itemId: "item-1" }] as Ctx["lines"],
      documentState: { kind: "draftReady", reasons: [] } }));
    expect(screen.getByText("Listo para confirmar")).toBeTruthy();
    expect(screen.queryByText("Cambios sin guardar")).toBeNull();
    cleanup();
    show(context({ ...ready, lines: [{ ...unlinkedXmlLine, _key: 1, purchaseReceptionLineId: null, itemId: "item-1" }] as Ctx["lines"],
      hasUnsavedFormChanges: true, documentState: { kind: "draftIncomplete", reasons: [{ code: "unsavedChanges" }] } }));
    expect(screen.getByText("Puede guardar · no confirmar")).toBeTruthy();
    expect(screen.getByText("Cambios sin guardar")).toBeTruthy();
    cleanup();
    show(context({ ...ready, editing: null, lines: [{ ...unlinkedXmlLine, _key: 1, purchaseReceptionLineId: null, itemId: "item-1" }] as Ctx["lines"],
      documentState: { kind: "new", reasons: [] } }));
    expect(screen.getByText("Guarde el borrador para poder confirmarlo.")).toBeTruthy();
    expect(screen.queryByText("Listo para confirmar")).toBeNull();
  });
  it("navigates a section to its invalid control before earlier valid controls", () => {
    show(context({ errors: { invoiceNumber: { type: "server", message: "Número rechazado" } } as Ctx["errors"] }), <div id="purchase-document" tabIndex={-1}>
      <select aria-label="Tipo"><option>01</option></select><input aria-label="Número" aria-invalid="true" />
    </div>);
    fireEvent.click(screen.getByRole("button", { name: /Incidencias/ }));
    fireEvent.click(screen.getByRole("button", { name: "Revisar documento" }));
    expect(document.activeElement).toBe(screen.getByLabelText("Número"));
  });
  it("presents a normal withholding skip reason as information, without a banner", () => {
    const ctx = context({ whPreview: { lines: [], totalRetained: 0, skipReason: "No aplica" } as unknown as Ctx["whPreview"], canApplyRetention: false });
    const { container } = render(<I18nProvider><PurchaseRetentionDraft ctx={ctx} /></I18nProvider>);
    expect(screen.getAllByText("No aplica")).toHaveLength(1);
    expect(container.querySelector(".zh-page-notice")).toBeNull();
  });
  it("keeps a base-unit equivalence as information next to the presentation, outside the issue tag", () => {
    const line = { ...manualLineWithoutCommercialData, _key: 1, itemId: "item-1", context: lineCardContext,
      packagingLevelId: "box-12", conversionFactor: 12, quantity: 1 / 12, quantityInBaseUom: 1, baseUnitPrice: 0.5, unitPrice: 6, vatCode: "4" };
    const ctx = context({ lines: [line], lineReadinessByKey: {}, pendingReceptionItems: [], hasLineReadinessBlockers: false });
    show(ctx, <PurchaseLineCard line={line} idx={0} ctx={ctx} />);
    expect(screen.getAllByText(/^No es una cantidad exacta de/)).toHaveLength(1);
    expect(document.querySelector(".zh-line-issues")?.textContent ?? "").not.toContain("Equivalencia informativa");
  });
});
