// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataSupplierListActions } from "./MasterDataSupplierListActions";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

const partner = {id: "supplier-42", legalName: "Proveedor Uno", identificationNumber: "0991234567001", isActive: true, isCustomer: false, canAssignAsCustomer: true} as BusinessPartnerSummaryDto;
function Location() { return <output>{useLocation().pathname}</output>; }
function setup(overrides = {}) {
  const props = {partner, canUpdate: true, canDisable: true, saving: false,
    onEdit: vi.fn(), onSupplierProfile: vi.fn(), onAddAsCustomer: vi.fn(), onActivate: vi.fn(), onDisable: vi.fn(), ...overrides};
  const result = render(<I18nProvider><MemoryRouter><MasterDataSupplierListActions {...props}/><Location/><button>Outside</button></MemoryRouter></I18nProvider>);
  const open = () => fireEvent.click(screen.getByRole("button", {name: /^M\u00e1s acciones:/}));
  return {...result, props, open};
}
afterEach(cleanup);
describe("supplier row actions", () => {
  it("shows only View and More; view preserves the detail route", () => {
    setup();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getAllByRole("button")).toHaveLength(3);
    fireEvent.click(screen.getByRole("button", {name: /^Ver: Proveedor Uno/}));
    expect(screen.getByText("/masterdata/business-partners/supplier-42")).toBeTruthy();
  });
  it("opens a named portal outside its row; arrows, Home, End, Escape and outside click work", () => {
    const {container, open} = setup();
    const trigger = screen.getByRole("button", {name: /^M\u00e1s acciones:/});
    fireEvent.keyDown(trigger, {key: "ArrowDown"});
    const menu = screen.getByRole("menu", {name: /Proveedor Uno.*0991234567001/});
    expect(container.contains(menu)).toBe(false);
    const items = screen.getAllByRole("menuitem");
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowDown"});
    expect(document.activeElement).toBe(items[1]);
    fireEvent.keyDown(items[1], {key: "End"});
    expect(document.activeElement).toBe(items[3]);
    fireEvent.keyDown(items[3], {key: "Home"});
    expect(document.activeElement).toBe(items[0]);
    fireEvent.keyDown(items[0], {key: "ArrowUp"});
    expect(document.activeElement).toBe(items[3]);
    fireEvent.keyDown(items[3], {key: "Escape"});
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
    open(); fireEvent.pointerDown(screen.getByText("Outside"));
    expect(screen.queryByRole("menu")).toBeNull();
    open(); fireEvent.keyDown(screen.getAllByRole("menuitem")[0], {key: "Tab"});
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it.each([
    ["Editar proveedor", "onEdit", true],
    ["Configuraci\u00f3n SRI", "onSupplierProfile", true],
    ["Asignar como cliente", "onAddAsCustomer", false],
    ["Desactivar proveedor", "onDisable", false],
  ] as const)("%s retains its callback and closes", (name, callback, fullPartner) => {
    const {props, open} = setup(); open();
    fireEvent.click(screen.getByRole("menuitem", {name}));
    expect(props[callback]).toHaveBeenCalledExactlyOnceWith(fullPartner ? partner : partner.id);
    expect(screen.queryByRole("menu")).toBeNull();
  });
  it("has no menu without permissions, even if callbacks exist", () => {
    setup({canUpdate: false, canDisable: false});
    expect(screen.queryByRole("button", {name: /^M\u00e1s acciones:/})).toBeNull();
  });
  it("disable permission alone exposes only deactivate for an active supplier", () => {
    const {open} = setup({canUpdate: false}); open();
    expect(screen.getAllByRole("menuitem")).toHaveLength(1);
    expect(screen.getByRole("menuitem", {name: "Desactivar proveedor"})).toBeTruthy();
  });
  it("inactive supplier activation requires update and keeps the original callback", () => {
    const {props, open} = setup({partner: {...partner, isActive: false}, canDisable: false});open();
    expect(screen.queryByRole("menuitem", {name: "Desactivar proveedor"})).toBeNull();
    fireEvent.click(screen.getByRole("menuitem", {name: "Activar proveedor"}));
    expect(props.onActivate).toHaveBeenCalledExactlyOnceWith(partner.id);
  });
  it("does not expose activation with disable permission only", () => {
    setup({partner: {...partner, isActive: false}, canUpdate: false});
    expect(screen.queryByRole("button", {name: /^M\u00e1s acciones:/})).toBeNull();
  });
  it("retains saving disables for assignment and status, without expanding them to edit/SRI", () => {
    const {props, open} = setup({saving: true});open();
    const assign = screen.getByRole("menuitem", {name: "Asignar como cliente"}) as HTMLButtonElement;
    const deactivate = screen.getByRole("menuitem", {name: "Desactivar proveedor"}) as HTMLButtonElement;
    expect(assign.disabled).toBe(true); expect(deactivate.disabled).toBe(true);
    fireEvent.click(assign);fireEvent.click(deactivate);
    expect(props.onAddAsCustomer).not.toHaveBeenCalled();expect(props.onDisable).not.toHaveBeenCalled();
    expect((screen.getByRole("menuitem", {name: "Editar proveedor"}) as HTMLButtonElement).disabled).toBe(false);
  });
  // FUNC-PARTNER-ROLE-01 — la acción respeta canAssignAsCustomer del backend.
  it("hides assign-as-customer when the supplier already has the customer role", () => {
    const {open} = setup({partner: {...partner, isCustomer: true, canAssignAsCustomer: false}}); open();
    expect(screen.queryByRole("menuitem", {name: "Asignar como cliente"})).toBeNull();
    expect(screen.getByRole("menuitem", {name: "Editar proveedor"})).toBeTruthy();
    expect(screen.getByRole("menuitem", {name: "Desactivar proveedor"})).toBeTruthy();
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
});
