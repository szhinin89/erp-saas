// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataSupplierListActions } from "./MasterDataSupplierListActions";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/** BP-CONSOLIDATION-01 Etapa 3 — «Gestionar» abre la ficha; en el menú solo quedan asignar el otro rol
 * y activar/desactivar. Editar y Config SRI viven en la ficha unificada. */
const partner = {id: "supplier-42", legalName: "Proveedor Uno", identificationNumber: "0991234567001", isActive: true, isCustomer: false, canAssignAsCustomer: true} as BusinessPartnerSummaryDto;
const MORE = /^Más acciones:/;
function Location() { const l = useLocation(); return <><output>{l.pathname}</output><output data-testid="state">{JSON.stringify(l.state)}</output></>; }
function setup(overrides = {}) {
  const props = {partner, canUpdate: true, canDisable: true, saving: false,
    onAddAsCustomer: vi.fn(), onActivate: vi.fn(), onDisable: vi.fn(), ...overrides};
  const result = render(<I18nProvider><MemoryRouter><MasterDataSupplierListActions {...props}/><Location/><button>Outside</button></MemoryRouter></I18nProvider>);
  const open = () => fireEvent.click(screen.getByRole("button", {name: MORE}));
  const itemNames = () => screen.getAllByRole("menuitem").map((i) => i.textContent);
  return {...result, props, open, itemNames};
}
afterEach(cleanup);
describe("supplier row actions", () => {
  it("shows Gestionar and More (no Ver, Editar or Config SRI); Gestionar opens the unified record", () => {
    setup();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getAllByRole("button")).toHaveLength(3);
    expect(screen.queryByRole("button", {name: /^Ver:/})).toBeNull();
    expect(screen.queryByRole("button", {name: /Editar|SRI/})).toBeNull();
    fireEvent.click(screen.getByRole("button", {name: /^Gestionar: Proveedor Uno \(0991234567001\)$/}));
    expect(screen.getByText("/masterdata/business-partners/supplier-42")).toBeTruthy();
    // El listado de origen queda como destino de «Volver» en la ficha.
    expect(screen.getByTestId("state").textContent).toBe('{"from":"/suppliers"}');
  });
  it("menu holds only assign-as-customer and deactivate, in that order, with no edit/SRI items", () => {
    const {open, itemNames} = setup(); open();
    expect(itemNames()).toEqual(["Asignar como cliente", "Desactivar proveedor"]);
  });
  it("opens a named portal outside its row; arrows, Home, End, Escape and outside click work", () => {
    const {container, open} = setup();
    const trigger = screen.getByRole("button", {name: MORE});
    fireEvent.keyDown(trigger, {key: "ArrowDown"});
    const menu = screen.getByRole("menu", {name: /Proveedor Uno.*0991234567001/});
    expect(container.contains(menu)).toBe(false);
    const items = screen.getAllByRole("menuitem");
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowDown"});
    expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "Home"});
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowUp"});
    expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "End"});
    expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "Escape"});
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
    open(); fireEvent.pointerDown(screen.getByText("Outside"));
    expect(screen.queryByRole("menu")).toBeNull();
    open(); fireEvent.keyDown(screen.getAllByRole("menuitem")[0], {key: "Tab"});
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it.each([
    ["Asignar como cliente", "onAddAsCustomer"],
    ["Desactivar proveedor", "onDisable"],
  ] as const)("%s retains its callback and closes", (name, callback) => {
    const {props, open} = setup(); open();
    fireEvent.click(screen.getByRole("menuitem", {name}));
    expect(props[callback]).toHaveBeenCalledExactlyOnceWith(partner.id);
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it("has no menu without permissions, but Gestionar stays available", () => {
    setup({canUpdate: false, canDisable: false});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
    expect(screen.getByRole("button", {name: /^Gestionar:/})).toBeTruthy();
  });
  it("update only on an already-customer active supplier: nothing left for the menu (edit moved to the record)", () => {
    setup({canDisable: false, partner: {...partner, isCustomer: true, canAssignAsCustomer: false}});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("disable permission alone exposes only deactivate for an active supplier", () => {
    const {open, itemNames} = setup({canUpdate: false}); open();
    expect(itemNames()).toEqual(["Desactivar proveedor"]);
  });
  it("inactive supplier activation requires update and keeps the original callback", () => {
    const {props, open} = setup({partner: {...partner, isActive: false, canAssignAsCustomer: false}, canDisable: false});open();
    expect(screen.queryByRole("menuitem", {name: "Desactivar proveedor"})).toBeNull();
    fireEvent.click(screen.getByRole("menuitem", {name: "Activar proveedor"}));
    expect(props.onActivate).toHaveBeenCalledExactlyOnceWith(partner.id);
  });
  it("does not expose activation with disable permission only", () => {
    setup({partner: {...partner, isActive: false}, canUpdate: false});
    expect(screen.queryByRole("button", {name: MORE})).toBeNull();
  });
  it("retains saving disables for assignment and status", () => {
    const {props, open} = setup({saving: true});open();
    const assign = screen.getByRole("menuitem", {name: "Asignar como cliente"}) as HTMLButtonElement;
    const deactivate = screen.getByRole("menuitem", {name: "Desactivar proveedor"}) as HTMLButtonElement;
    expect(assign.disabled).toBe(true); expect(deactivate.disabled).toBe(true);
    fireEvent.click(assign);fireEvent.click(deactivate);
    expect(props.onAddAsCustomer).not.toHaveBeenCalled();expect(props.onDisable).not.toHaveBeenCalled();
  });
  // FUNC-PARTNER-ROLE-01 — la acción respeta canAssignAsCustomer del backend.
  it("hides assign-as-customer when the supplier already has the customer role", () => {
    const {open, itemNames} = setup({partner: {...partner, isCustomer: true, canAssignAsCustomer: false}}); open();
    expect(itemNames()).toEqual(["Desactivar proveedor"]);
  });
  it("hides assign-as-customer for an inactive supplier but keeps activation", () => {
    const {open} = setup({partner: {...partner, isActive: false, canAssignAsCustomer: false}}); open();
    expect(screen.queryByRole("menuitem", {name: "Asignar como cliente"})).toBeNull();
    expect(screen.getByRole("menuitem", {name: "Activar proveedor"})).toBeTruthy();
  });
  it("hides assign-as-customer without update permission even if assignable", () => {
    const {open} = setup({canUpdate: false}); open();
    expect(screen.queryByRole("menuitem", {name: "Asignar como cliente"})).toBeNull();
  });
  it("hides assign-as-customer without its callback", () => {
    const {open, itemNames} = setup({onAddAsCustomer: undefined}); open();
    expect(itemNames()).toEqual(["Desactivar proveedor"]);
  });
});
