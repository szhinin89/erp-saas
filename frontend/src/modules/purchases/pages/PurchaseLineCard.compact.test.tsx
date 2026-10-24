// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { PurchaseLineReadiness } from "../utils/purchaseLineReadiness";
import { PurchaseLineCard } from "./PurchasesPage";
import {
  brokenDownLine,
  manualLineWithoutCommercialData,
  missingItemReadiness,
  missingWarehouseReadiness,
  unlinkedXmlLine,
  xmlIceEditedLine,
  xmlIrbpnrLine,
} from "./purchaseLineCard.fixture";

// PURCHASE-LINE-COMPACT-01 — cada dato en una sola ubicación visual, cada aviso una sola vez y
// el detalle detrás del disclosure "Detalle" (ZH-DS-DISCLOSURE-01).

type Ctx = Parameters<typeof PurchaseLineCard>[0]["ctx"];

function renderLine(line: PurchaseLineFormValues, readiness?: PurchaseLineReadiness, errors?: Ctx["errors"]) {
  const ctx = {
    fieldDisabled: false,
    errors,
    formWatch: { globalWarehouseId: "wh-1" },
    warehouses: [{ id: "wh-1", code: "B01", name: "Bodega principal" }],
    lineReadinessByKey: readiness ? { [line._key]: readiness } : {},
    vatRatesMap: { "0": 0, "4": 15 },
    iceRatesMap: { "3011": 10 },
    matchingKey: null,
    updateLine: vi.fn(),
    updateLineWarehouse: vi.fn(),
    duplicateLine: vi.fn(),
    removeLine: vi.fn(),
    fetchItemContext: vi.fn(),
    handleMatchItem: vi.fn(),
    handleUnmatchItem: vi.fn(),
    handleSaveSupplierPresentation: vi.fn(),
    setBreakdownSourceId: vi.fn(),
  } as unknown as Ctx;
  const utils = render(
    <MemoryRouter>
      <I18nProvider>
        <PurchaseLineCard line={line} idx={line._key - 1} ctx={ctx} />
      </I18nProvider>
    </MemoryRouter>,
  );
  return { ...utils, ctx };
}

const detailToggle = () => screen.getByRole("button", { name: "Detalle" });
const issuesTag = (container: HTMLElement) =>
  container.querySelector<HTMLButtonElement>(".zh-line-issues button");

beforeEach(() => setPrecisionPolicyForTests(TEST_PRECISION_POLICY));
afterEach(cleanup);

describe("PurchaseLineCard — layout compacto", () => {
  it("no repite cantidad/costo, margen ni código de proveedor fuera de la cabecera", () => {
    renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    expect(document.body.textContent).not.toMatch(/Se registrarán/);
    expect(document.body.textContent).not.toMatch(/Rentabilidad/);
    expect(screen.getAllByText("Cód. proveedor", { exact: false })).toHaveLength(1);
  });

  it("distingue subtotal de producto (cabecera) de la base imponible tributaria del XML", () => {
    renderLine(xmlIceEditedLine);
    expect(screen.getByText("Subtotal producto")).toBeTruthy();
    expect(screen.getByText("Base imp. IVA")).toBeTruthy();
    expect(screen.queryByText("BASE IMP.")).toBeNull();
    expect(screen.queryByText("Base imponible")).toBeNull();
  });

  it("colapsa y expande el detalle con el disclosure global, manteniendo cabecera y avisos", () => {
    renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    const toggle = detailToggle();
    expect(toggle.className).toContain("zh-btn--disclosure");
    expect(toggle.getAttribute("aria-expanded")).toBe("true");
    const panel = document.getElementById(toggle.getAttribute("aria-controls")!)!;
    expect(within(panel).getByText("Producto recibido (XML)")).toBeTruthy();
    expect(within(panel).getByText("Presentación e inventario")).toBeTruthy();
    expect(within(panel).getByText("Información comercial")).toBeTruthy();

    fireEvent.click(toggle);
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    expect(screen.queryByText("Producto recibido (XML)")).toBeNull();
    expect(screen.getByText("Subtotal producto")).toBeTruthy();
    expect(document.body.textContent).not.toMatch(/Detalle colapsado/);
  });

  it("compra manual: sin valores XML de relleno, sin ICE en cabecera y aviso de datos comerciales vacíos", () => {
    renderLine(manualLineWithoutCommercialData);
    expect(screen.getByText("Compra manual — no proviene de recepción electrónica.")).toBeTruthy();
    expect(screen.queryByText("Base imp. IVA")).toBeNull();
    expect(screen.queryByText(/^ICE/)).toBeNull();
    expect(screen.getByText("Sin datos comerciales para esta línea.")).toBeTruthy();
  });

  it("Detalle y estado viven en la cabecera; sin franja secundaria cuando no hay información adicional", () => {
    const { container } = renderLine(manualLineWithoutCommercialData);
    const summary = container.querySelector(".pdl-line__summary")!;
    expect(within(summary as HTMLElement).getByRole("button", { name: "Detalle" })).toBeTruthy();
    expect(container.querySelector(".pdl-line__status")).toBeNull();
  });

  it("sin producto vinculado: etiqueta bloqueante y Detalle en la cabecera", () => {
    const { container } = renderLine(unlinkedXmlLine, missingItemReadiness);
    expect(screen.getByText("Sin producto vinculado")).toBeTruthy();
    expect(issuesTag(container)!.textContent).toBe("Bloqueante: Sin producto");
    expect(within(container.querySelector(".pdl-line__summary") as HTMLElement).getByRole("button", { name: "Detalle" })).toBeTruthy();
    expect(screen.getByText("Cód. proveedor: 13563")).toBeTruthy();
  });

  it("línea desglosada: importes bloqueados y sin acción de recálculo desde XML", () => {
    renderLine(brokenDownLine);
    const inputs = screen.getAllByRole("textbox") as HTMLInputElement[];
    expect(inputs.length).toBeGreaterThan(0);
    inputs.forEach((input) => expect(input.disabled).toBe(true));
    expect(screen.queryByRole("button", { name: /Recalcular desde XML/ })).toBeNull();
    expect(detailToggle()).toBeTruthy();
  });

  it("IRBPNR sin ICE: muestra IRBPNR en cabecera y XML, omite ICE en cero", () => {
    renderLine(xmlIrbpnrLine);
    expect(screen.getAllByText(/IRBPNR/).length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText(/^ICE/)).toBeNull();
  });
});

// ZH-DS-LINE-ISSUES-01 — una sola etiqueta anclada por línea con la incidencia de mayor
// prioridad; el detalle lista cada incidencia una vez con sus acciones correctivas.
describe("PurchaseLineCard — etiqueta de incidencias", () => {
  it("muestra la incidencia de mayor prioridad y el número de incidencias adicionales", () => {
    const { container } = renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    const tag = issuesTag(container)!;
    expect(tag.textContent).toBe("Bloqueante: Falta bodega · +2");
    expect(tag.getAttribute("aria-expanded")).toBe("false");
    expect(tag.querySelector(".badge--error")).toBeTruthy();
    expect(container.querySelector(".pdl-line-card--has-issues")).toBeTruthy();
    // La franja inferior solo conserva información operativa (sin avisos).
    expect(container.querySelector(".pdl-line__status .badge")).toBeNull();
  });

  it("abre el detalle con todas las incidencias una vez y ejecuta la acción correctiva", () => {
    const { container, ctx } = renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    const tag = issuesTag(container)!;
    fireEvent.click(tag);
    expect(tag.getAttribute("aria-expanded")).toBe("true");
    const dialog = screen.getByRole("dialog", { name: "Incidencias de la línea (3)" });
    for (const label of ["Falta bodega", "Cantidad o costo distintos al documento del proveedor", "Sin precio de venta"])
      expect(within(dialog).getAllByText(label)).toHaveLength(1);
    fireEvent.click(within(dialog).getByRole("button", { name: "Recalcular desde XML" }));
    expect(ctx.updateLine).toHaveBeenCalledWith(xmlIceEditedLine._key, "quantity", 2);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(tag.getAttribute("aria-expanded")).toBe("false");
  });

  it("cierra el detalle con Escape", () => {
    const { container } = renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    fireEvent.click(issuesTag(container)!);
    expect(screen.getByRole("dialog")).toBeTruthy();
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("una sola incidencia: sin contador; línea bloqueada sin acción de recálculo", () => {
    const { container } = renderLine(brokenDownLine, missingItemReadiness);
    expect(issuesTag(container)!.textContent).toBe("Bloqueante: Sin producto");
    fireEvent.click(issuesTag(container)!);
    expect(within(screen.getByRole("dialog")).queryByRole("button")).toBeNull();
  });

  it("sin incidencias: sin etiqueta ni badge de éxito", () => {
    const { container } = renderLine(xmlIrbpnrLine);
    expect(issuesTag(container)).toBeNull();
    expect(container.querySelector(".pdl-line-card--has-issues")).toBeNull();
    expect(container.querySelector(".badge--success")?.textContent ?? "").not.toMatch(/Ítem/);
  });

  it("sigue visible con la línea colapsada", () => {
    const { container } = renderLine(xmlIceEditedLine, missingWarehouseReadiness);
    fireEvent.click(detailToggle());
    expect(detailToggle().getAttribute("aria-expanded")).toBe("false");
    expect(issuesTag(container)!.textContent).toMatch(/^Bloqueante: Falta bodega/);
  });
});

describe("P0 - field validation on purchase lines", () => {
  it("associates quantity, cost, warehouse and tax errors once, also when collapsed", () => {
    const { container } = renderLine(xmlIceEditedLine, undefined, { lines: [{
      quantity: { type: "too_small", message: "Quantity rejected" },
      unitPrice: { type: "server", message: "Cost rejected" },
      vatCode: { type: "server", message: "Tax rejected" },
      warehouseId: { type: "server", message: "Warehouse rejected" },
    }] });
    for (const label of ["Cantidad a registrar", "Costo unitario a registrar", "Bodega"]) {
      const control = screen.getByLabelText(label);
      expect(control.getAttribute("aria-invalid")).toBe("true");
      expect(document.getElementById(control.getAttribute("aria-describedby")!)?.textContent).toMatch(/rejected/);
    }
    fireEvent.click(detailToggle());
    for (const text of ["Quantity rejected", "Cost rejected", "Tax rejected", "Warehouse rejected"])
      expect(screen.getAllByText(text)).toHaveLength(1);
    expect(issuesTag(container)!.textContent).toContain("Campos por corregir");
    fireEvent.click(issuesTag(container)!);
    expect(screen.getAllByText("Quantity rejected")).toHaveLength(1);
  });
  it("retains the indication for collapsed discount/presentation errors without repeating their text", () => {
    const { container } = renderLine(xmlIceEditedLine, undefined, { lines: [{
      discountPct: { type: "server", message: "Discount rejected" },
      packagingLevelId: { type: "server", message: "Presentation rejected" },
    }] });
    expect(screen.getAllByText("Discount rejected")).toHaveLength(1);
    fireEvent.click(detailToggle());
    expect(screen.queryByText("Discount rejected")).toBeNull();
    fireEvent.click(issuesTag(container)!);
    expect(screen.getByRole("button", { name: "Ir al campo" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Ir al campo" }));
    expect(screen.getByText("Discount rejected")).toBeTruthy();
    expect(screen.getAllByText("Presentation rejected")).toHaveLength(1);
    expect(detailToggle().getAttribute("aria-expanded")).toBe("true");
  });
});
