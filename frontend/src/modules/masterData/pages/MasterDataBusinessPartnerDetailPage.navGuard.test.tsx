// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { renderBpDetail } from "./bpDetailTestRender";
import {
  ALL_PERMS,
  CUSTOMER_ROLE,
  PERM,
  SUPPLIER_ROLE,
  makeBp,
  permsOf,
} from "./bpDetailTestFixtures";

/**
 * BP-NAV-GUARD-01 — `useBlocker` + `message.confirm` sobre un router de datos: menú/rutas,
 * Atrás/Adelante, «Volver», una sola confirmación por navegación, cada sección de la ficha,
 * `?tab=` sin bloqueo, permisos y cambio de empresa.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    updateBusinessPartner: vi.fn(),
    getSalesSettings: vi.fn(),
    getPurchaseSettings: vi.fn(),
    getRetentionDefaults: vi.fn(),
    getExpenseSubcategories: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({
  paymentTermService: {
    list: vi.fn().mockResolvedValue([
      { id: "pt1", code: "CONT", name: "Contado", summary: "0 días", isActive: true },
    ]),
  },
}));
vi.mock("../../branches/facades/geographyLookupFacade", () => ({
  geographyLookupFacade: {
    provinces: vi.fn().mockResolvedValue([]),
    cantons: vi.fn().mockResolvedValue([]),
    parishes: vi.fn().mockResolvedValue([]),
  },
}));
const NO_OPTIONS = { options: [], loading: false, error: null };
vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [{ code: "05", name: "Cédula" }], loading: false }),
  getSriIdTypeName: (code: string) => code,
}));
vi.mock("../api/useLegalEntityTypes", () => ({
  useLegalEntityTypes: () => ({ options: [], loading: false }),
}));
vi.mock("../api/useSriSupplierTypes", () => ({
  useSriSupplierTypes: () => ({ options: [{ code: "01", name: "Persona Natural" }], loading: false }),
}));
vi.mock("../api/useSriPaymentMethods", () => ({ useSriPaymentMethods: () => NO_OPTIONS }));
vi.mock("../api/useSriTaxSupportCodes", () => ({ useSriTaxSupportCodes: () => NO_OPTIONS }));
vi.mock("../api/useSriRetentionCodes", () => ({ useSriRetentionCodes: () => NO_OPTIONS }));
vi.mock("../api/useClassificationCatalogs", () => ({
  useCustomerCategories: () => NO_OPTIONS,
  useCustomerSegments: () => NO_OPTIONS,
  useCustomerCreditRatings: () => NO_OPTIONS,
  useLoyaltyTiers: () => NO_OPTIONS,
  useCustomerInvoiceFormats: () => NO_OPTIONS,
  useCustomerClassifications: () => NO_OPTIONS,
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const path = () => screen.getByTestId("pathname").textContent;
const sectionOf = (heading: string) =>
  screen.getByRole("heading", { name: heading }).parentElement as HTMLElement;
const allow = (perms: string[]) =>
  vi.mocked(usePermissionsUi).mockReturnValue(permsOf(perms) as unknown as ReturnType<typeof usePermissionsUi>);

async function renderLoaded(search: string, history = {}) {
  const view = renderBpDetail(search, history);
  await screen.findByRole("tablist");
  return view;
}
const go = (router: ReturnType<typeof renderBpDetail>["router"], to: string | number) =>
  act(async () => {
    if (typeof to === "number") await router.navigate(to);
    else await router.navigate(to);
  });
const editIdentity = async () =>
  fireEvent.change(await screen.findByLabelText(/Razón social/), { target: { value: "Cliente Dos" } });

beforeEach(() => {
  vi.clearAllMocks();
  allow(ALL_PERMS);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
    mode: "Unconfigured", items: [], availableSubcategoryIds: [], catalog: null,
  } as never);
  vi.mocked(businessPartnerFacade.updateBusinessPartner).mockResolvedValue(undefined as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => cleanup());

describe("BP-NAV-GUARD-01 — navegación a otra ruta", () => {
  it("sin cambios navega sin confirmación", async () => {
    const { router } = await renderLoaded("?tab=general");
    await go(router, "/otra");
    expect(path()).toBe("/otra");
    expect(message.confirm).not.toHaveBeenCalled();
  });

  it("con cambios confirma una vez; al cancelar se queda con la edición intacta y puede reintentar", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    const { router } = await renderLoaded("?tab=general");
    await editIdentity();
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.title)).toBe("¿Descartar cambios?");
    expect(String(options.message)).toMatch(/cambios sin guardar/i);
    expect(path()).toMatch(/^\/(customers|suppliers)\/bp-1$/);
    expect((screen.getByLabelText(/Razón social/) as HTMLInputElement).value).toBe("Cliente Dos");

    await go(router, "/otra");
    await waitFor(() => expect(path()).toBe("/otra"));
    expect(message.confirm).toHaveBeenCalledTimes(2);
  });

  it("dos intentos de navegación mientras se confirma generan una sola confirmación", async () => {
    let answer: (v: boolean) => void = () => {};
    vi.mocked(message.confirm).mockReturnValue(new Promise<boolean>((res) => { answer = res; }));
    const { router } = await renderLoaded("?tab=general");
    await editIdentity();
    await go(router, "/a");
    await go(router, "/b");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await act(async () => answer(true));
    await waitFor(() => expect(path()).not.toContain("/customers/bp-1"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });

  it("«Volver» con cambios pide una sola confirmación (la del bloqueador)", async () => {
    await renderLoaded("?tab=general", { before: ["/anterior"] });
    await editIdentity();
    fireEvent.click(screen.getByRole("button", { name: /Volver/ }));
    await waitFor(() => expect(path()).toBe("/customers"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });
});

describe("BP-NAV-GUARD-01 — Atrás, Adelante y pestañas", () => {
  it("Atrás con cambios pide confirmar: cancelar permanece; aceptar vuelve", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    const { router } = await renderLoaded("?tab=general", { before: ["/anterior"] });
    await editIdentity();
    await go(router, -1);
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(path()).toMatch(/^\/(customers|suppliers)\/bp-1$/);
    await go(router, -1);
    await waitFor(() => expect(path()).toBe("/anterior"));
  });

  it("Adelante con cambios también pide confirmar", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    const { router } = await renderLoaded("?tab=general", { after: ["/siguiente"] });
    await editIdentity();
    await go(router, 1);
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(path()).toMatch(/^\/(customers|suppliers)\/bp-1$/);
  });

  it("cambiar de pestaña, ?tab= y Atrás entre pestañas nunca piden confirmación", async () => {
    const { router } = await renderLoaded("?tab=general");
    await editIdentity();
    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=contacts"));
    await go(router, -1);
    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=general"));
    expect(message.confirm).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/Razón social/) as HTMLInputElement).value).toBe("Cliente Dos");
  });
});

describe("BP-NAV-GUARD-01 — cada sección de la ficha protege sus cambios", () => {
  const changeIn = async (heading: string, label: string | RegExp, value: string) => {
    const section = await screen.findByRole("heading", { name: heading });
    // El encabezado aparece antes que el formulario (carga asíncrona): se espera al control.
    const control = await within(section.parentElement!).findByLabelText(label);
    if (control.tagName === "SELECT")
      await waitFor(() => expect(control.querySelector(`option[value="${value}"]`)).toBeTruthy());
    fireEvent.change(control, { target: { value } });
  };
  it.each([
    ["Datos generales", "?tab=general", async () => editIdentity()],
    ["Clasificación del cliente", "?tab=classification", () => changeIn("Clasificación comercial", "Zona de ventas", "Norte")],
    ["Condición de ventas", "?tab=sales", () => changeIn("Condición predeterminada para ventas", "Condición predeterminada para ventas", "pt1")],
    ["Configuración SRI del proveedor", "?tab=sri", () => changeIn("Configuración SRI", "Tipo de Proveedor", "01")],
    ["Condición de compras", "?tab=purchases", () => changeIn("Condición predeterminada para compras/gastos", /Condición predeterminada para compras/, "pt1")],
  ])("%s: con cambios pendientes se pide confirmar al salir", async (_name, search, change) => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    const { router } = await renderLoaded(search);
    await change();
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(path()).toMatch(/^\/(customers|suppliers)\/bp-1$/);
  });

  it("tras guardar ya no hay cambios pendientes: navegar no pide confirmación", async () => {
    const { router } = await renderLoaded("?tab=general");
    await editIdentity();
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    await waitFor(() => expect(vi.mocked(businessPartnerFacade.getBusinessPartner).mock.calls.length).toBeGreaterThanOrEqual(2));
    await go(router, "/otra");
    await waitFor(() => expect(path()).toBe("/otra"));
    expect(message.confirm).not.toHaveBeenCalled();
  });
});

describe("BP-NAV-GUARD-01 — permisos", () => {
  it("con solo view no hay formularios editables y navegar no pide confirmación", async () => {
    allow([PERM.view]);
    const { router } = await renderLoaded("?tab=general");
    expect(screen.queryByRole("button", { name: "Guardar cambios" })).toBeNull();
    await go(router, "/otra");
    expect(path()).toBe("/otra");
    expect(message.confirm).not.toHaveBeenCalled();
  });
});

describe("BP-NAV-GUARD-01 — cambio de empresa", () => {
  const bump = () =>
    act(() => useAuthStore.setState({ companySessionVersion: useAuthStore.getState().companySessionVersion + 1 }));

  it("subir la sesión de empresa no es navegación: no confirma y conserva los cambios de identidad", async () => {
    await renderLoaded("?tab=general");
    await editIdentity();
    await bump();
    await waitFor(() => expect(vi.mocked(businessPartnerFacade.getBusinessPartner).mock.calls.length).toBeGreaterThanOrEqual(2));
    expect(message.confirm).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/Razón social/) as HTMLInputElement).value).toBe("Cliente Dos");
  });

  it("el flujo del selector de empresa (cambio de sesión y luego navegar al dashboard) confirma una sola vez", async () => {
    const { router } = await renderLoaded("?tab=general");
    await editIdentity();
    await bump();
    await go(router, "/dashboard");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
  });

  it("las secciones por empresa se reinician con el cambio de contexto y su aviso no queda colgado", async () => {
    const { router } = await renderLoaded("?tab=classification");
    const section = await screen.findByRole("heading", { name: "Clasificación comercial" });
    fireEvent.change(within(section.parentElement!).getByLabelText("Zona de ventas"), { target: { value: "Norte" } });
    await bump();
    await waitFor(() =>
      expect((within(sectionOf("Clasificación comercial")).getByLabelText("Zona de ventas") as HTMLInputElement).value).toBe("Sur"),
    );
    await go(router, "/dashboard");
    await waitFor(() => expect(path()).toBe("/dashboard"));
    expect(message.confirm).not.toHaveBeenCalled();
  });
});
