// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, within } from "@testing-library/react";
import { SalesEmissionConfigSection } from "./SalesEmissionConfigSection";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";

// SALES-POS-EMISSION-PANEL-SIMPLIFICATION-01: el panel principal de /sales ya no expande Sucursal
// / Caja / Punto de emisión / Tipo Emisión / Tipo Documento / Forma Pago SRI por Defecto — solo
// una tarjeta compacta "Configuración de venta" con un botón que abre el detalle completo en un
// modal. El modal lee/escribe el mismo ctx.formWatch/setValue que antes usaba el panel inline —
// sin segunda fuente de verdad.
//
// SALES-POS-SILENT-OK-STATUS-AND-ALERTS-01: patrón "silencioso cuando todo está bien" — un estado
// "ready" no muestra ningún badge/aviso ("Lista"/"Emisión OK"), solo el resumen; "review"/
// "incomplete" sí muestran un aviso amarillo/rojo ("Revisar configuración"/"Falta configuración")
// con la causa, porque ahí sí hay algo que revisar o resolver.

afterEach(() => cleanup());

const BASE_SESSION = {
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
};
/** POS-EMISSION-VISIBILITY-01: la Forma de pago SRI solo existe en emisión electrónica. */
const ELECTRONIC = {
  myCashSession: { ...BASE_SESSION, emissionType: "Electronic" },
} as unknown as Partial<SalesPageContext>;
const UNMAPPED_PM = {
  id: "pm-1",
  code: "OTRO",
  name: "Otro",
  isActive: true,
  requiresReference: false,
  isCreditAllowed: false,
  sortOrder: 1,
  detailType: "None",
  affectsPhysicalCash: false,
  accountSource: "PaymentMethodAccount",
  accountingAccountId: null,
  sriPaymentMethodCode: null,
};

function buildCtx(overrides: Partial<SalesPageContext> = {}): SalesPageContext {
  const base = {
    formWatch: { docTypeCode: "01", sriPaymentMethodCode: "01", customerId: "cust-1" },
    setValue: vi.fn(),
    readOnly: false,
    fieldDisabled: false,
    editing: null,
    lines: [],
    selectedWarehouseId: "wh-1",
    payments: [],
    paymentMethods: [],
    hasCashSession: true,
    myCashSession: BASE_SESSION,
    branchName: "Sucursal Principal",
    sriDocTypes: [{ code: "01", name: "Factura" }],
    sriPaymentMethods: [
      { code: "01", name: "Sin utilización del sistema financiero" },
      { code: "20", name: "Otros con utilización del sistema financiero" },
    ],
  };
  return withPosDerivedCtx({ ...base, ...overrides });
}

describe("SalesEmissionConfigSection — tarjeta compacta (panel principal)", () => {
  it('renderiza la tarjeta "Configuración de venta"', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.getByText("Configuración de venta")).toBeTruthy();
  });

  it("no muestra los campos completos de emisión directamente en el panel (sin abrir el modal)", () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.queryByText("Tipo Documento")).toBeNull();
    expect(screen.queryByText("Forma Pago SRI por Defecto")).toBeNull();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it('muestra el resumen compacto "Factura · Caja Principal · Sucursal Principal"', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.getByText("Factura · Caja Principal · Sucursal Principal")).toBeTruthy();
  });

  it('muestra el texto corto "Emisión física · Punto 001"', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.getByText("Emisión física · Punto 001")).toBeTruthy();
  });

  it('estado OK (ready): no muestra badge "LISTA" ni ningún aviso de "Emisión OK" — solo resumen + botón', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.queryByText("Lista")).toBeNull();
    expect(screen.queryByText("LISTA")).toBeNull();
    expect(screen.queryByText(/emisión ok/i)).toBeNull();
    expect(screen.queryByText("Falta configuración")).toBeNull();
    expect(screen.queryByText("Revisar configuración")).toBeNull();
    expect(screen.getByText("Factura · Caja Principal · Sucursal Principal")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Configuración" })).toBeTruthy();
  });

  it('estado OK (ready): no muestra ningún badge verde en la tarjeta', () => {
    const { container } = render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(container.querySelector(".badge--success")).toBeNull();
  });

  it('estado incompleto: aviso rojo "Falta configuración" cuando falta un dato BLOQUEANTE (caja abierta sin tipo de emisión)', () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          myCashSession: { ...BASE_SESSION, emissionType: null },
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    expect(screen.getByText("Falta configuración")).toBeTruthy();
    expect(screen.getByText(/Tipo de emisión/)).toBeTruthy();
  });

  // POS-CONFIG-STATUS-SEVERITY-01: "sin caja abierta" tiene su propio aviso (CashSessionNotice) y
  // su propio bloqueante en ctx.emitBlockers — la tarjeta de configuración ya no lo duplica.
  it("sin caja abierta: la tarjeta no repite el aviso (lo muestra CashSessionNotice)", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({ hasCashSession: false, myCashSession: null })}
      />,
    );
    expect(screen.queryByText("Falta configuración")).toBeNull();
  });

  it('estado advertencia (electrónica): aviso amarillo "Revisar configuración" cuando no hay Forma Pago SRI por defecto y aún no hay cobros (no bloquea)', () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          ...ELECTRONIC,
          formWatch: { docTypeCode: "01", sriPaymentMethodCode: "", customerId: "cust-1" },
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    expect(screen.getByText("Revisar configuración")).toBeTruthy();
    expect(screen.getByText("No hay Forma Pago SRI por defecto configurada.")).toBeTruthy();
  });

  it("electrónica: una forma de cobro que usa el default SRI es solo informativa — la tarjeta no alarma", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          ...ELECTRONIC,
          paymentMethods: [UNMAPPED_PM],
          payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    expect(screen.queryByText("Revisar configuración")).toBeNull();
    expect(screen.queryByText("Falta configuración")).toBeNull();
  });

  it("física: nunca muestra avisos de Forma de pago SRI, aunque no haya default ni mapeo", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          formWatch: { docTypeCode: "01", sriPaymentMethodCode: "", customerId: "cust-1" },
          paymentMethods: [UNMAPPED_PM],
          payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    expect(screen.queryByText("Revisar configuración")).toBeNull();
    expect(screen.queryByText("Falta configuración")).toBeNull();
    expect(screen.queryByText(/Forma Pago SRI/)).toBeNull();
  });

  it("no bloquea/alarma mientras la sesión de caja todavía se está verificando (hasCashSession null)", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({ hasCashSession: null, myCashSession: undefined })}
      />,
    );
    expect(screen.getByText("Cargando…")).toBeTruthy();
    expect(screen.queryByText("Falta configuración")).toBeNull();
    expect(screen.queryByText("Revisar configuración")).toBeNull();
  });

  // SALES-POS-EMISSION-CONFIG-DUPLICATED-ACTIONS-01: un solo botón — no hay un modo "ver" y un
  // modo "editar" distintos, así que ya no hay dos botones que mostrar/ocultar según
  // fieldDisabled; el modal decide qué queda editable con ctx.readOnly/ctx.fieldDisabled.
  it('muestra un único botón "Configuración" (bloqueado o editable)', () => {
    const { rerender } = render(
      <SalesEmissionConfigSection ctx={buildCtx({ fieldDisabled: true })} />,
    );
    expect(screen.getAllByRole("button", { name: "Configuración" })).toHaveLength(1);

    rerender(<SalesEmissionConfigSection ctx={buildCtx({ fieldDisabled: false })} />);
    expect(screen.getAllByRole("button", { name: "Configuración" })).toHaveLength(1);
  });

  it('no muestra "Ver detalle" ni "Cambiar configuración"', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    expect(screen.queryByRole("button", { name: "Ver detalle" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Cambiar configuración" })).toBeNull();
  });
});

describe("SalesEmissionConfigSection — modal de detalle", () => {
  it("física: el modal NO muestra Forma Pago SRI por Defecto (solo existe en el XML electrónico)", () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Tipo Emisión:")).toBeTruthy();
    expect(within(dialog).queryByText("Forma Pago SRI por Defecto")).toBeNull();
  });

  it('"Configuración" abre el modal con los datos completos de emisión (electrónica)', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx(ELECTRONIC)} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Sucursal:")).toBeTruthy();
    expect(within(dialog).getByText("Caja:")).toBeTruthy();
    expect(within(dialog).getByText("Punto:")).toBeTruthy();
    expect(within(dialog).getByText("Tipo Emisión:")).toBeTruthy();
    expect(within(dialog).getByText("Tipo Documento")).toBeTruthy();
    expect(within(dialog).getByText("Forma Pago SRI por Defecto")).toBeTruthy();
  });

  it('Tipo Documento sigue mostrando "01 — Factura" dentro del modal', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    expect(screen.getByDisplayValue("01 — Factura")).toBeTruthy();
  });

  it("cambiar Tipo Documento desde el modal actualiza el mismo form (ctx.setValue)", () => {
    const setValue = vi.fn();
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          setValue,
          sriDocTypes: [
            { code: "01", name: "Factura" },
            { code: "04", name: "Nota de Crédito" },
          ],
        })}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const select = screen.getByDisplayValue("01 — Factura");
    fireEvent.change(select, { target: { value: "04" } });

    expect(setValue).toHaveBeenCalledWith("docTypeCode", "04");
  });

  it("cambiar Forma Pago SRI por Defecto desde el modal actualiza el mismo form (ctx.setValue)", () => {
    const setValue = vi.fn();
    render(<SalesEmissionConfigSection ctx={buildCtx({ ...ELECTRONIC, setValue })} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const select = screen.getByDisplayValue("01 — Sin utilización del sistema financiero");
    fireEvent.change(select, { target: { value: "20" } });

    expect(setValue).toHaveBeenCalledWith("sriPaymentMethodCode", "20");
  });

  it("con el formulario editable, el modal permite cambiar los selects (no disabled)", () => {
    render(<SalesEmissionConfigSection ctx={buildCtx({ ...ELECTRONIC, fieldDisabled: false })} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const docTypeSelect = screen.getByDisplayValue(
      "01 — Factura",
    ) as HTMLSelectElement;
    const sriSelect = screen.getByDisplayValue(
      "01 — Sin utilización del sistema financiero",
    ) as HTMLSelectElement;
    expect(docTypeSelect.disabled).toBe(false);
    expect(sriSelect.disabled).toBe(false);
  });

  it("con el formulario bloqueado (fieldDisabled), el mismo botón abre el modal en solo lectura", () => {
    render(<SalesEmissionConfigSection ctx={buildCtx({ ...ELECTRONIC, fieldDisabled: true })} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    expect(screen.getByRole("dialog")).toBeTruthy();
    const docTypeSelect = screen.getByDisplayValue(
      "01 — Factura",
    ) as HTMLSelectElement;
    const sriSelect = screen.getByDisplayValue(
      "01 — Sin utilización del sistema financiero",
    ) as HTMLSelectElement;
    expect(docTypeSelect.disabled).toBe(true);
    expect(sriSelect.disabled).toBe(true);
  });

  it("cuando falta un dato bloqueante, el modal lista los faltantes", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          myCashSession: { ...BASE_SESSION, emissionType: null },
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Falta para poder emitir/)).toBeTruthy();
    expect(within(dialog).getByText(/Tipo de emisión/)).toBeTruthy();
  });

  it("cuando hay un fallback en uso (electrónica), el modal muestra la causa como información", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          ...ELECTRONIC,
          paymentMethods: [UNMAPPED_PM],
          payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
        } as unknown as Partial<SalesPageContext>)}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const dialog = screen.getByRole("dialog");
    expect(
      within(dialog).getByText(
        "Alguna forma de cobro no tiene mapeo SRI propio y usa la Forma Pago SRI por defecto.",
      ),
    ).toBeTruthy();
  });

  it("cuando todo está OK, el modal no muestra ningún badge de estado — solo los datos", () => {
    const { container } = render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const dialog = screen.getByRole("dialog");
    expect(within(dialog).queryByText(/^Lista$/)).toBeNull();
    // El único badge que sí puede aparecer es el de Tipo de Emisión (dato informativo, no de
    // estado de configuración) — no debe existir ningún .badge--success de estado "Lista".
    expect(container.querySelectorAll(".sf-config-modal__status").length).toBe(0);
  });

  it("no introduce estilos inline", () => {
    const { container } = render(<SalesEmissionConfigSection ctx={buildCtx()} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));
    // El modal se porta vía React normal dentro del mismo árbol (ZHModal no usa portal a otro
    // nodo), así que basta con el container de render — se excluye <body> a propósito: ZHModal
    // fija `document.body.style.overflow` mientras está abierto (bloquear scroll de fondo), que
    // no es un estilo inline de este componente.
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
