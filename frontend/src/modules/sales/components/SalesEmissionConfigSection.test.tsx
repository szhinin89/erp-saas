// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, within } from "@testing-library/react";
import { SalesEmissionConfigSection } from "./SalesEmissionConfigSection";
import type { SalesPageContext } from "../hooks/useSalesPage";

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
    myCashSession: {
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
    },
    branchName: "Sucursal Principal",
    sriDocTypes: [{ code: "01", name: "Factura" }],
    sriPaymentMethods: [
      { code: "01", name: "Sin utilización del sistema financiero" },
      { code: "20", name: "Otros con utilización del sistema financiero" },
    ],
  };
  return { ...base, ...overrides } as unknown as SalesPageContext;
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

  it('estado incompleto: aviso rojo "Falta configuración" cuando falta un dato requerido (sin caja abierta)', () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({ hasCashSession: false, myCashSession: null })}
      />,
    );
    expect(screen.getByText("Falta configuración")).toBeTruthy();
    expect(
      screen.getByText(/Caja abierta \(Sucursal \/ Caja \/ Punto de emisión\)/),
    ).toBeTruthy();
  });

  it('estado advertencia: aviso amarillo "Revisar configuración" con la causa, cuando una forma de cobro usada cae al default SRI (no bloquea)', () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          paymentMethods: [
            {
              id: "pm-1",
              code: "OTRO",
              name: "Otro",
              isActive: true,
              requiresReference: false,
              isCreditAllowed: false,
              sortOrder: 1,
              detailType: "None",
              sriPaymentMethodCode: null,
            },
          ],
          payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
        })}
      />,
    );
    expect(screen.getByText("Revisar configuración")).toBeTruthy();
    expect(
      screen.getByText(
        "Alguna forma de cobro no tiene mapeo SRI propio y usa la Forma Pago SRI por defecto.",
      ),
    ).toBeTruthy();
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
  it('"Configuración" abre el modal con los datos completos de emisión', () => {
    render(<SalesEmissionConfigSection ctx={buildCtx()} />);
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
    render(<SalesEmissionConfigSection ctx={buildCtx({ setValue })} />);
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const select = screen.getByDisplayValue("01 — Sin utilización del sistema financiero");
    fireEvent.change(select, { target: { value: "20" } });

    expect(setValue).toHaveBeenCalledWith("sriPaymentMethodCode", "20");
  });

  it("con el formulario editable, el modal permite cambiar los selects (no disabled)", () => {
    render(<SalesEmissionConfigSection ctx={buildCtx({ fieldDisabled: false })} />);
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
    render(<SalesEmissionConfigSection ctx={buildCtx({ fieldDisabled: true })} />);
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
        ctx={buildCtx({ hasCashSession: false, myCashSession: null })}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Configuración" }));

    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Falta para poder emitir/)).toBeTruthy();
    expect(within(dialog).getByText(/Caja abierta/)).toBeTruthy();
  });

  it("cuando hay una advertencia (fallback en uso), el modal muestra la causa", () => {
    render(
      <SalesEmissionConfigSection
        ctx={buildCtx({
          paymentMethods: [
            {
              id: "pm-1",
              code: "OTRO",
              name: "Otro",
              isActive: true,
              requiresReference: false,
              isCreditAllowed: false,
              sortOrder: 1,
              detailType: "None",
              sriPaymentMethodCode: null,
            },
          ],
          payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
        })}
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
