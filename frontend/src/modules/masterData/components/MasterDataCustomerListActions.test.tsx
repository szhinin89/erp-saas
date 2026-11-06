// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataCustomerListActions } from "./MasterDataCustomerListActions";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/** UIUX-GLOBAL-ACTIONS-CLIENTS-01 — Ver + Editar visibles; Empresa, Clasificación, Agregar
 * como proveedor y Activar/Desactivar en «Más acciones» con las condiciones previas. */
const partner = {id: "customer-7", legalName: "Cliente Siete", identificationNumber: "0990000007001",
  isActive: true, isSupplier: false, canAssignAsSupplier: true} as BusinessPartnerSummaryDto;
const MORE = /^Más acciones: Cliente Siete \(0990000007001\)$/;
function Location() { return <output>{useLocation().pathname}</output>; }
function setup(overrides = {}) {
  const props = {partner, canUpdate: true, canDisable: true, canConfigure: true, saving: false,
    onEdit: vi.fn(), onSettings: vi.fn(), onCustomerConfig: vi.fn(), onAddAsSupplier: vi.fn(),
    onActivate: vi.fn(), onDisable: vi.fn(), ...overrides};
  const result = render(<I18nProvider><MemoryRouter><MasterDataCustomerListActions {...props}/><Location/><button>Outside</button></MemoryRouter></I18nProvider>);
  const open = () => fireEvent.click(screen.getByRole("button", {name: MORE}));
  const itemNames = () => screen.getAllByRole("menuitem").map((i) => i.textContent);
  return {...result, props, open, itemNames};
}
const callbacks = ["onEdit", "onSettings", "onCustomerConfig", "onAddAsSupplier", "onActivate", "onDisable"] as const;
afterEach(cleanup);

describe("customer row actions — visible actions", () => {
  it("shows View, Edit and More with customer-specific accessible names; View keeps the detail route", () => {
    setup();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getAllByRole("button").map((b) => b.getAttribute("aria-label"))).toEqual([
      "Ver: Cliente Siete (0990000007001)", "Editar: Cliente Siete (0990000007001)",
      "Más acciones: Cliente Siete (0990000007001)", null]);
    fireEvent.click(screen.getByRole("button", {name: /^Ver: Cliente Siete/}));
    expect(screen.getByText("/masterdata/business-partners/customer-7")).toBeTruthy();
  });
  it("Edit stays outside the menu and keeps its callback", () => {
    const {props} = setup();
    fireEvent.click(screen.getByRole("button", {name: /^Editar: Cliente Siete/}));
    expect(props.onEdit).toHaveBeenCalledExactlyOnceWith(partner);
    expect(screen.queryByRole("menu")).toBeNull();
  });
});

describe("customer row actions — menu content and permissions", () => {
  it("full permissions on an active customer: identity header and items in the approved order", () => {
    const {container, open, itemNames} = setup(); open();
    const menu = screen.getByRole("menu", {name: MORE});
    expect(container.contains(menu)).toBe(false);
    expect(menu.querySelector(".md-customer-actions-menu__identity")?.textContent).toBe("Cliente Siete (0990000007001)");
    expect(itemNames()).toEqual(["Empresa", "Clasificación", "Agregar como proveedor", "Desactivar cliente"]);
  });
  it.each([
    ["Empresa", "onSettings", partner],
    ["Clasificación", "onCustomerConfig", partner],
    ["Agregar como proveedor", "onAddAsSupplier", partner.id],
    ["Desactivar cliente", "onDisable", partner.id],
  ] as const)("%s keeps its callback once and closes the menu", (name, callback, arg) => {
    const {props, open} = setup(); open();
    fireEvent.click(screen.getByRole("menuitem", {name}));
    expect(props[callback]).toHaveBeenCalledExactlyOnceWith(arg);
    for (const other of callbacks.filter((c) => c !== callback)) expect(props[other]).not.toHaveBeenCalled();
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it("only configure-company: no Edit, menu with Empresa only, and its callback works", () => {
    const {props, open, itemNames} = setup({canUpdate: false, canDisable: false}); open();
    expect(screen.queryByRole("button", {name: /^Editar:/})).toBeNull();
    expect(itemNames()).toEqual(["Empresa"]);
    fireEvent.click(screen.getByRole("menuitem", {name: "Empresa"}));
    expect(props.onSettings).toHaveBeenCalledExactlyOnceWith(partner);
  });
  it("only configure-company without the settings callback: no menu", () => {
    setup({canUpdate: false, canDisable: false, onSettings: undefined});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("no permissions: only View, no menu, no callback reachable", () => {
    const {props} = setup({canUpdate: false, canDisable: false, canConfigure: false});
    expect(screen.getAllByRole("button").map((b) => b.textContent)).toEqual(["Ver", "Outside"]);
    for (const c of callbacks) expect(props[c]).not.toHaveBeenCalled();
  });
  it("update without secondary callbacks on an active customer and no disable: Edit visible, menu omitted", () => {
    setup({canDisable: false, canConfigure: false, onCustomerConfig: undefined, onAddAsSupplier: undefined});
    expect(screen.getByRole("button", {name: /^Editar:/})).toBeTruthy();
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("disable only: active shows only Deactivate; inactive has no menu (activation needs update)", () => {
    const {open, itemNames, unmount} = setup({canUpdate: false, canConfigure: false}); open();
    expect(itemNames()).toEqual(["Desactivar cliente"]);
    unmount();
    setup({canUpdate: false, canConfigure: false, partner: {...partner, isActive: false, canAssignAsSupplier: false}});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("inactive customer with update: Activate replaces Deactivate and keeps its callback", () => {
    const {props, open, itemNames} = setup({partner: {...partner, isActive: false, canAssignAsSupplier: false}}); open();
    expect(itemNames()).toEqual(["Empresa", "Clasificación", "Activar cliente"]);
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
  it("saving disables assign/status (no request), not Empresa/Clasificación/Edit", () => {
    const {props, open} = setup({saving: true}); open();
    const assign = screen.getByRole("menuitem", {name: "Agregar como proveedor"}) as HTMLButtonElement;
    const deactivate = screen.getByRole("menuitem", {name: "Desactivar cliente"}) as HTMLButtonElement;
    expect(assign.disabled).toBe(true); expect(deactivate.disabled).toBe(true);
    fireEvent.click(assign); fireEvent.click(deactivate);
    expect(props.onAddAsSupplier).not.toHaveBeenCalled(); expect(props.onDisable).not.toHaveBeenCalled();
    expect((screen.getByRole("menuitem", {name: "Empresa"}) as HTMLButtonElement).disabled).toBe(false);
    expect((screen.getByRole("menuitem", {name: "Clasificación"}) as HTMLButtonElement).disabled).toBe(false);
    expect((screen.getByRole("button", {name: /^Editar:/}) as HTMLButtonElement).disabled).toBe(false);
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
    fireEvent.keyDown(items[1], {key: "End"}); expect(document.activeElement).toBe(items[3]);
    fireEvent.keyDown(items[3], {key: "ArrowDown"}); expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowUp"}); expect(document.activeElement).toBe(items[3]);
    fireEvent.keyDown(items[3], {key: "Home"}); expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "Escape"});
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
    const items = screen.getAllByRole("menuitem");
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "End"});
    expect(document.activeElement).toBe(items[1]);
  });
  it("returns focus to the row trigger after the modal opened by an item closes", async () => {
    function Harness() {
      const [modal, setModal] = useState(false);
      return (<>
        <MasterDataCustomerListActions partner={partner} canUpdate canDisable canConfigure saving={false}
          onEdit={vi.fn()} onSettings={() => setModal(true)} onActivate={vi.fn()} onDisable={vi.fn()} />
        {modal && <div role="dialog" aria-modal="true"><button onClick={() => setModal(false)}>Cerrar modal</button></div>}
      </>);
    }
    render(<I18nProvider><MemoryRouter><Harness/></MemoryRouter></I18nProvider>);
    const trigger = screen.getByRole("button", {name: MORE});
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole("menuitem", {name: "Empresa"}));
    const close = await screen.findByRole("button", {name: "Cerrar modal"});
    close.focus();
    fireEvent.click(close);
    await waitFor(() => expect(document.activeElement).toBe(trigger));
  });
  it("does not steal focus if the user already moved it after the modal closed", async () => {
    function Harness() {
      const [modal, setModal] = useState(false);
      return (<>
        <MasterDataCustomerListActions partner={partner} canUpdate canDisable canConfigure saving={false}
          onEdit={vi.fn()} onSettings={() => setModal(true)} onActivate={vi.fn()} onDisable={vi.fn()} />
        <input aria-label="Buscar" />
        {modal && <div role="dialog" aria-modal="true"><button onClick={() => { setModal(false); (document.querySelector("input") as HTMLInputElement).focus(); }}>Cerrar modal</button></div>}
      </>);
    }
    render(<I18nProvider><MemoryRouter><Harness/></MemoryRouter></I18nProvider>);
    fireEvent.click(screen.getByRole("button", {name: MORE}));
    fireEvent.click(screen.getByRole("menuitem", {name: "Empresa"}));
    fireEvent.click(await screen.findByRole("button", {name: "Cerrar modal"}));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(document.activeElement).toBe(screen.getByRole("textbox", {name: "Buscar"}));
  });
});
