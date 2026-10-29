// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { CreateSupplierModal } from "./CreateSupplierModal";

const calls = vi.hoisted(() => ({ create: vi.fn(), assign: vi.fn(), success: vi.fn() }));
vi.mock("../../masterData/facades/businessPartnerRegistrationFacade", async importOriginal => ({
  ...await importOriginal<typeof import("../../masterData/facades/businessPartnerRegistrationFacade")>(),
  businessPartnerRegistrationFacade: { createBusinessPartner: calls.create, assignRole: calls.assign },
}));
vi.mock("../../../lib/messages", () => ({ message: { success: calls.success } }));
vi.mock("../../masterData/api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [{ code: "04", name: "RUC" }], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [{ code: "04", name: "RUC" }], loading: false }),
}));
vi.mock("../../masterData/api/useLegalEntityTypes", () => ({
  useLegalEntityTypes: () => ({ options: [{ code: 1, name: "Persona natural" }], loading: false }),
}));
vi.mock("../../masterData/api/useSriSupplierTypes", () => ({
  useSriSupplierTypes: () => ({ options: [{ code: "01", name: "Bienes" }], loading: false }),
}));

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  calls.create.mockResolvedValue({ id: "supplier-new" });
  calls.assign.mockResolvedValue(undefined);
});
afterEach(cleanup);

function setup(tradeName: string | null = "Comercial XML") {
  const props = { open: true, supplierRuc: "1790016919001", supplierName: "Proveedor XML",
    supplierTradeName: tradeName, onClose: vi.fn(), onCreated: vi.fn() };
  render(<I18nProvider><CreateSupplierModal {...props} /></I18nProvider>);
  return props;
}
const chooseType = () => fireEvent.change(screen.getByRole("combobox", { name: /Tipo de Proveedor/i }), { target: { value: "01" } });
const createButton = () => screen.getByRole("button", { name: /Crear Proveedor/i });

describe("Crear proveedor desde recepcion: composicion modal oficial", () => {
  it("conserva la precarga y todos los campos sin card ni fieldset interiores y con footer asociado", () => {
    const props = setup();
    const dialog = screen.getByRole("dialog", { name: "Crear proveedor" });
    const form = within(dialog).getByRole("form", { name: "Datos del proveedor" }) as HTMLFormElement;
    expect((screen.getByRole("textbox", { name: /Número de identificación/ }) as HTMLInputElement).value).toBe(props.supplierRuc);
    expect((screen.getByRole("textbox", { name: /Razón social/ }) as HTMLInputElement).value).toBe(props.supplierName);
    expect((screen.getByRole("textbox", { name: "Nombre comercial" }) as HTMLInputElement).value).toBe(props.supplierTradeName);
    expect(within(form).getAllByRole("textbox")).toHaveLength(4);
    expect(within(form).getAllByRole("combobox")).toHaveLength(3);
    expect(form.querySelectorAll(".zh-grid--2")).toHaveLength(3);
    expect(dialog.querySelector(".pg-section, .md-partner-fieldset, .prd-wiz-footer")).toBeNull();
    const footer = dialog.querySelector(".zh-modal-footer") as HTMLElement;
    const button = within(footer).getByRole("button", { name: /Crear Proveedor/i }) as HTMLButtonElement;
    expect(button.form).toBe(form);
    expect(form.contains(button)).toBe(false);
    fireEvent.click(within(footer).getByRole("button", { name: "Cancelar" }));
    expect(props.onClose).toHaveBeenCalledOnce();
    expect(calls.create).not.toHaveBeenCalled();
  });

  it("preserva validaciones de RUC y tipo de proveedor sin llamar al alta", async () => {
    setup(null);
    expect((screen.getByRole("textbox", { name: "Nombre comercial" }) as HTMLInputElement).value).toBe("");
    fireEvent.change(screen.getByRole("textbox", { name: /Número de identificación/ }), { target: { value: "123" } });
    fireEvent.click(createButton());
    expect(await screen.findByText("RUC inválido según las reglas del SRI.")).toBeTruthy();
    expect(screen.getByText("El tipo de proveedor es obligatorio.")).toBeTruthy();
    expect(calls.create).not.toHaveBeenCalled();
  });

  it("crea con los datos precargados y asigna el mismo rol y configuracion de proveedor", async () => {
    const props = setup();
    chooseType();
    fireEvent.click(createButton());
    await waitFor(() => expect(props.onCreated).toHaveBeenCalledWith("supplier-new"));
    expect(calls.create).toHaveBeenCalledWith({ identificationType: "04", identificationNumber: props.supplierRuc,
      legalEntityTypeCode: undefined, legalName: props.supplierName, tradeName: props.supplierTradeName, countryCode: "EC" });
    expect(calls.assign).toHaveBeenCalledWith("supplier-new", { roleType: 2, supplierConfig: { refundProviderTypeCode: "01" } });
  });

  it("mantiene los valores y el mensaje del servidor cuando falla el alta", async () => {
    const props = setup();
    calls.create.mockRejectedValue(new Error("Proveedor rechazado"));
    chooseType();
    fireEvent.click(createButton());
    expect(await screen.findByText("Proveedor rechazado")).toBeTruthy();
    expect((screen.getByRole("textbox", { name: /Razón social/ }) as HTMLInputElement).value).toBe(props.supplierName);
    expect(calls.assign).not.toHaveBeenCalled();
    expect(props.onCreated).not.toHaveBeenCalled();
  });
});
