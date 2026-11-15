// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataCustomerListActions } from "./MasterDataCustomerListActions";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/** BP-CONSOLIDATION-01 Etapa 3 — «Gestionar» abre la ficha unificada; Editar, Empresa y Clasificación
 * viven allí. En «Más acciones» solo quedan Agregar como proveedor y Activar/Desactivar con las
 * condiciones previas. */
const partner = {id: "customer-7", legalName: "Cliente Siete", identificationNumber: "0990000007001",
  isActive: true, isSupplier: false, canAssignAsSupplier: true} as BusinessPartnerSummaryDto;
const MORE = /^Más acciones: Cliente Siete \(0990000007001\)$/;
function Location() { const l = useLocation(); return <><output>{l.pathname}</output><output data-testid="state">{JSON.stringify(l.state)}</output></>; }
function setup(overrides = {}) {
  const props = {partner, canUpdate: true, canDisable: true, saving: false,
    onAddAsSupplier: vi.fn(), onActivate: vi.fn(), onDisable: vi.fn(), ...overrides};
  const result = render(<I18nProvider><MemoryRouter><MasterDataCustomerListActions {...props}/><Location/><button>Outside</button></MemoryRouter></I18nProvider>);
  const open = () => fireEvent.click(screen.getByRole("button", {name: MORE}));
  const itemNames = () => screen.getAllByRole("menuitem").map((i) => i.textContent);
  return {...result, props, open, itemNames};
}
const callbacks = ["onAddAsSupplier", "onActivate", "onDisable"] as const;
afterEach(cleanup);

describe("customer row actions — visible actions", () => {
  it("shows Gestionar and More with customer-specific accessible names; Gestionar opens the unified record", () => {
    setup();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getAllByRole("button").map((b) => b.getAttribute("aria-label"))).toEqual([
      "Gestionar: Cliente Siete (0990000007001)",
      "Más acciones: Cliente Siete (0990000007001)", null]);
    expect(screen.queryByRole("button", {name: /^Ver:|^Editar:/})).toBeNull();
    fireEvent.click(screen.getByRole("button", {name: /^Gestionar: Cliente Siete/}));
    expect(screen.getByText("/masterdata/business-partners/customer-7")).toBeTruthy();
    // El listado de origen queda como destino de «Volver» en la ficha.
    expect(screen.getByTestId("state").textContent).toBe('{"from":"/customers"}');
  });
});

describe("customer row actions — menu content and permissions", () => {
  it("full permissions on an active customer: identity header and only assign/status items", () => {
    const {container, open, itemNames} = setup(); open();
    const menu = screen.getByRole("menu", {name: MORE});
    expect(container.contains(menu)).toBe(false);
    expect(menu.querySelector(".md-customer-actions-menu__identity")?.textContent).toBe("Cliente Siete (0990000007001)");
    expect(itemNames()).toEqual(["Agregar como proveedor", "Desactivar cliente"]);
    for (const retired of ["Empresa", "Clasificación", "Editar"])
      expect(screen.queryByRole("menuitem", {name: retired})).toBeNull();
  });
  it.each([
    ["Agregar como proveedor", "onAddAsSupplier"],
    ["Desactivar cliente", "onDisable"],
  ] as const)("%s keeps its callback once and closes the menu", (name, callback) => {
    const {props, open} = setup(); open();
    fireEvent.click(screen.getByRole("menuitem", {name}));
    expect(props[callback]).toHaveBeenCalledExactlyOnceWith(partner.id);
    for (const other of callbacks.filter((c) => c !== callback)) expect(props[other]).not.toHaveBeenCalled();
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it("no permissions: only Gestionar, no menu, no callback reachable", () => {
    const {props} = setup({canUpdate: false, canDisable: false});
    expect(screen.getAllByRole("button").map((b) => b.textContent)).toEqual(["Gestionar", "Outside"]);
    for (const c of callbacks) expect(props[c]).not.toHaveBeenCalled();
  });
  it("update without secondary actions on an active customer and no disable: menu omitted", () => {
    setup({canDisable: false, onAddAsSupplier: undefined});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("disable only: active shows only Deactivate; inactive has no menu (activation needs update)", () => {
    const {open, itemNames, unmount} = setup({canUpdate: false}); open();
    expect(itemNames()).toEqual(["Desactivar cliente"]);
    unmount();
    setup({canUpdate: false, partner: {...partner, isActive: false, canAssignAsSupplier: false}});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("inactive customer with update: Activate replaces Deactivate and keeps its callback", () => {
    const {props, open, itemNames} = setup({partner: {...partner, isActive: false, canAssignAsSupplier: false}}); open();
    expect(itemNames()).toEqual(["Activar cliente"]);
    fireEvent.click(screen.getByRole("menuitem", {name: "Activar cliente"}));
    expect(props.onActivate).toHaveBeenCalledExactlyOnceWith(partner.id);
    expect(props.onDisable).not.toHaveBeenCalled();
  });
  it.each([
    ["backend flag false (already supplier)", {partner: {...partner, isSupplier: true, canAssignAsSupplier: false}}],
    ["no update permission", {canUpdate: false}],
    ["no callback", {onAddAsSupplier: undefined}],
  ])("hides Agregar como proveedor: %s", (_case, overrides) => {
    const {open} = setup(overrides); open();
    expect(screen.queryByRole("menuitem", {name: "Agregar como proveedor"})).toBeNull();
  });
  it("does not infer eligibility: isSupplier=false with canAssignAsSupplier=false stays hidden", () => {
    const {open} = setup({partner: {...partner, isSupplier: false, canAssignAsSupplier: false}}); open();
    expect(screen.queryByRole("menuitem", {name: "Agregar como proveedor"})).toBeNull();
  });
  it("saving disables assign/status (no request)", () => {
    const {props, open} = setup({saving: true}); open();
    const assign = screen.getByRole("menuitem", {name: "Agregar como proveedor"}) as HTMLButtonElement;
    const deactivate = screen.getByRole("menuitem", {name: "Desactivar cliente"}) as HTMLButtonElement;
    expect(assign.disabled).toBe(true); expect(deactivate.disabled).toBe(true);
    fireEvent.click(assign); fireEvent.click(deactivate);
    expect(props.onAddAsSupplier).not.toHaveBeenCalled(); expect(props.onDisable).not.toHaveBeenCalled();
    expect((screen.getByRole("button", {name: /^Gestionar:/}) as HTMLButtonElement).disabled).toBe(false);
  });
});

describe("customer row actions — keyboard and focus", () => {
  it("arrows, Home, End, Escape, Tab and outside click", () => {
    const {open} = setup();
    const trigger = screen.getByRole("button", {name: MORE});
    expect(trigger.getAttribute("aria-haspopup")).toBe("menu");
    fireEvent.keyDown(trigger, {key: "ArrowDown"});
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    const items = screen.getAllByRole("menuitem");
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowDown"}); expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "ArrowDown"}); expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowUp"}); expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "Home"}); expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "End"}); expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "Escape"});
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
    open(); fireEvent.pointerDown(screen.getByText("Outside"));
    expect(screen.queryByRole("menu")).toBeNull();
    open(); fireEvent.keyDown(screen.getAllByRole("menuitem")[0], {key: "Tab"});
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });
  it("arrow navigation skips disabled items while saving", () => {
    setup({saving: true});
    fireEvent.keyDown(screen.getByRole("button", {name: MORE}), {key: "ArrowDown"});
    // Con `saving` todas las acciones del menú están deshabilitadas: no hay foco que mover.
    expect(screen.getAllByRole("menuitem").every((i) => (i as HTMLButtonElement).disabled)).toBe(true);
  });
  // «Activar/Desactivar» abre una confirmación modal desde la página: el foco vuelve a la fila al cerrarla.
  it("returns focus to the row trigger after the modal opened by an item closes", async () => {
    function Harness() {
      const [modal, setModal] = useState(false);
      return (<>
        <MasterDataCustomerListActions partner={partner} canUpdate canDisable saving={false}
          onDisable={() => setModal(true)} onActivate={vi.fn()} />
        {modal && <div role="dialog" aria-modal="true"><button onClick={() => setModal(false)}>Cerrar modal</button></div>}
      </>);
    }
    render(<I18nProvider><MemoryRouter><Harness/></MemoryRouter></I18nProvider>);
    const trigger = screen.getByRole("button", {name: MORE});
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole("menuitem", {name: "Desactivar cliente"}));
    const close = await screen.findByRole("button", {name: "Cerrar modal"});
    close.focus();
    fireEvent.click(close);
    await waitFor(() => expect(document.activeElement).toBe(trigger));
  });
  it("does not steal focus if the user already moved it after the modal closed", async () => {
    function Harness() {
      const [modal, setModal] = useState(false);
      return (<>
        <MasterDataCustomerListActions partner={partner} canUpdate canDisable saving={false}
          onDisable={() => setModal(true)} onActivate={vi.fn()} />
        <input aria-label="Buscar" />
        {modal && <div role="dialog" aria-modal="true"><button onClick={() => { setModal(false); (document.querySelector("input") as HTMLInputElement).focus(); }}>Cerrar modal</button></div>}
      </>);
    }
    render(<I18nProvider><MemoryRouter><Harness/></MemoryRouter></I18nProvider>);
    fireEvent.click(screen.getByRole("button", {name: MORE}));
    fireEvent.click(screen.getByRole("menuitem", {name: "Desactivar cliente"}));
    fireEvent.click(await screen.findByRole("button", {name: "Cerrar modal"}));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(document.activeElement).toBe(screen.getByRole("textbox", {name: "Buscar"}));
  });
});
