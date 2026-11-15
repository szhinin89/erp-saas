// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  screen,
  waitFor,
} from "@testing-library/react";
import { useAuthStore } from "../../../store/authStore";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { renderBpDetail } from "./bpDetailTestRender";
import {
  ALL_PERMS,
  CONTACT,
  CUSTOMER_ROLE,
  LOCATION,
  PERM,
  SUPPLIER_ROLE,
  makeBp,
  permsOf,
} from "./bpDetailTestFixtures";

/**
 * BP-CONSOLIDATION-01 Etapa 2 — ficha unificada: pestañas por rol, enlaces profundos (?tab=),
 * accesibilidad del tablist, solo lectura por permisos, cambio de empresa y cambios sin guardar.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    revokeRole: vi.fn(),
    updateBusinessPartner: vi.fn(),
    getSalesSettings: vi.fn(),
    getPurchaseSettings: vi.fn(),
    getRetentionDefaults: vi.fn(),
    getExpenseSubcategories: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({
  paymentTermService: { list: vi.fn().mockResolvedValue([]) },
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
  getSriIdTypeName: (code: string) => (code === "05" ? "Cédula" : code),
}));
vi.mock("../api/useLegalEntityTypes", () => ({
  useLegalEntityTypes: () => ({ options: [], loading: false }),
}));
vi.mock("../api/useSriSupplierTypes", () => ({ useSriSupplierTypes: () => NO_OPTIONS }));
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

const renderPage = (search = "", history = {}) => renderBpDetail(search, history);

const tabNames = () =>
  screen.getAllByRole("tab").map((t) => (t.textContent ?? "").replace(/\d+$/, "").trim());
const isSelected = (name: RegExp) =>
  screen.getByRole("tab", { name }).getAttribute("aria-selected") === "true";
const loaded = () => screen.findByRole("tablist");

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue(
    permsOf(ALL_PERMS) as unknown as ReturnType<typeof usePermissionsUi>,
  );
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE]));
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([LOCATION]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([CONTACT]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
    mode: "Unconfigured",
    items: [],
    availableSubcategoryIds: [],
    catalog: null,
  } as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => cleanup());

describe("ficha unificada — pestañas por rol", () => {
  it("cliente: Resumen, Datos generales, Cliente, Ubicaciones, Contactos y Roles (sin Proveedor)", async () => {
    renderPage();
    await loaded();
    expect(tabNames()).toEqual(["Resumen", "Datos generales", "Cliente", "Ubicaciones", "Contactos", "Roles"]);
  });

  it("con ambos roles activos aparecen Proveedor y Cliente", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
    renderPage();
    await loaded();
    expect(tabNames()).toEqual([
      "Resumen", "Datos generales", "Proveedor", "Cliente", "Ubicaciones", "Contactos", "Roles",
    ]);
  });

  it("un rol Supplier revocado no genera la pestaña Proveedor", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(
      makeBp([CUSTOMER_ROLE, { ...SUPPLIER_ROLE, isActive: false }]),
    );
    renderPage();
    await loaded();
    expect(tabNames()).not.toContain("Proveedor");
  });

  it("al revocar el rol Cliente su pestaña desaparece y la ficha sigue usable", async () => {
    vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);
    renderPage("?tab=roles");
    await loaded();
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(
      makeBp([{ ...CUSTOMER_ROLE, isActive: false }]),
    );
    fireEvent.click(await screen.findByRole("button", { name: "Revocar" }));
    await waitFor(() => expect(tabNames()).not.toContain("Cliente"));
    expect(isSelected(/^Roles/)).toBe(true);
  });
});

describe("ficha unificada — navegación y enlaces profundos", () => {
  it("sin ?tab= abre Resumen", async () => {
    renderPage();
    await loaded();
    expect(isSelected(/^Resumen/)).toBe(true);
  });

  it.each([
    ["customer", /^Cliente/],
    ["locations", /^Ubicaciones/],
    ["general", /^Datos generales/],
  ])("?tab=%s abre su pestaña", async (tab, name) => {
    renderPage(`?tab=${tab}`);
    await loaded();
    expect(isSelected(name)).toBe(true);
  });

  it.each(["supplier", "inexistente"])(
    "?tab=%s (inexistente o de rol no activo) cae en Resumen y se normaliza la URL",
    async (tab) => {
      renderPage(`?tab=${tab}`);
      await loaded();
      expect(isSelected(/^Resumen/)).toBe(true);
      await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=summary"));
    },
  );

  it("elegir una pestaña actualiza ?tab= y muestra solo su panel", async () => {
    renderPage();
    await loaded();
    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=contacts"));
    expect(screen.getByText("Ana Perez")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Ubicaciones" })).toBeNull();
  });

  it("mientras carga no normaliza el ?tab= solicitado", async () => {
    let resolveBp: (v: ReturnType<typeof makeBp>) => void = () => {};
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockReturnValue(
      new Promise((res) => { resolveBp = res; }),
    );
    renderPage("?tab=customer");
    expect(screen.getByTestId("search").textContent).toBe("?tab=customer");
    await act(async () => resolveBp(makeBp([CUSTOMER_ROLE])));
    await loaded();
    expect(isSelected(/^Cliente/)).toBe(true);
    expect(screen.getByTestId("search").textContent).toBe("?tab=customer");
  });
});

describe("ficha unificada — «Volver» al listado de origen", () => {
  const back = () => fireEvent.click(screen.getByRole("button", { name: /Volver/ }));

  it("tras recorrer varias pestañas vuelve al listado de origen, no a la pestaña anterior", async () => {
    renderPage("", { before: ["/suppliers"], state: { from: "/suppliers" } });
    await loaded();
    for (const name of [/^Datos generales/, /^Ubicaciones/, /^Contactos/])
      fireEvent.click(screen.getByRole("tab", { name }));
    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=contacts"));
    back();
    await waitFor(() => expect(screen.getByTestId("pathname").textContent).toBe("/suppliers"));
  });

  it("el origen se conserva al normalizar un ?tab= inválido", async () => {
    renderPage("?tab=supplier", { state: { from: "/customers" } });
    await loaded();
    await waitFor(() => expect(screen.getByTestId("search").textContent).toBe("?tab=summary"));
    back();
    await waitFor(() => expect(screen.getByTestId("pathname").textContent).toBe("/customers"));
  });

  it("sin origen (enlace directo) vuelve al listado del rol: Clientes si es cliente", async () => {
    renderPage("?tab=roles");
    await loaded();
    back();
    await waitFor(() => expect(screen.getByTestId("pathname").textContent).toBe("/customers"));
  });

  it("sin origen y solo proveedor activo vuelve a Proveedores", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([SUPPLIER_ROLE]));
    renderPage();
    await loaded();
    back();
    await waitFor(() => expect(screen.getByTestId("pathname").textContent).toBe("/suppliers"));
  });
});

describe("ficha unificada — accesibilidad", () => {
  it("el tablist tiene nombre y cada pestaña controla un tabpanel enlazado; solo el activo es visible", async () => {
    renderPage("?tab=customer");
    await loaded();
    expect(screen.getByRole("tablist").getAttribute("aria-label")).toBe("Secciones de la ficha");
    const active = screen.getByRole("tab", { name: /^Cliente/ });
    const panel = screen.getByRole("tabpanel");
    expect(active.getAttribute("aria-controls")).toBe(panel.id);
    expect(panel.getAttribute("aria-labelledby")).toBe(active.id);
    expect(panel.hasAttribute("hidden")).toBe(false);
  });

  it("cada pestaña apunta a un tabpanel real, aunque no se haya visitado, y su contenido se monta al visitarla", async () => {
    renderPage("?tab=locations");
    await loaded();
    const tabs = screen.getAllByRole("tab");
    for (const tab of tabs) {
      const panel = document.getElementById(tab.getAttribute("aria-controls") ?? "");
      expect(panel, `panel de ${tab.id}`).not.toBeNull();
      expect(panel!.getAttribute("aria-labelledby")).toBe(tab.id);
    }
    const panelOf = (id: string) => document.getElementById(`bp-panel-${id}`)!;
    expect(panelOf("contacts").childElementCount).toBe(0); // sin visitar: vacío y oculto
    expect(panelOf("contacts").hasAttribute("hidden")).toBe(true);

    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    expect(panelOf("contacts").childElementCount).toBeGreaterThan(0);
    // lo ya visitado se conserva, oculto, con su contenido
    expect(panelOf("locations").hasAttribute("hidden")).toBe(true);
    expect(panelOf("locations").childElementCount).toBeGreaterThan(0);
  });
});

describe("ficha unificada — nombres accesibles", () => {
  it("los botones de alta no incluyen el texto del icono (aria-hidden): «Nueva ubicación», «Nuevo contacto»", async () => {
    renderPage("?tab=locations");
    await loaded();
    expect(screen.getByRole("button", { name: "Nueva ubicación" })).toBeTruthy();
    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    expect(screen.getByRole("button", { name: "Nuevo contacto" })).toBeTruthy();
  });

  it("el filtro de ubicaciones tiene nombre accesible", async () => {
    renderPage("?tab=locations");
    await loaded();
    expect(screen.getByRole("combobox", { name: "Filtrar ubicaciones por estado" })).toBeTruthy();
  });
});

describe("ficha unificada — permisos y solo lectura", () => {
  beforeEach(() => {
    vi.mocked(usePermissionsUi).mockReturnValue(
      permsOf([PERM.view]) as unknown as ReturnType<typeof usePermissionsUi>,
    );
  });

  it("solo con view: Datos generales en solo lectura, sin formulario ni «Guardar cambios»", async () => {
    renderPage("?tab=general");
    await loaded();
    expect(screen.getByText("Tipo de entidad legal")).toBeTruthy();
    expect(screen.getAllByText("Cliente Uno").length).toBeGreaterThan(1);
    expect(screen.queryByRole("button", { name: "Guardar cambios" })).toBeNull();
    expect(screen.queryByRole("textbox", { name: /Razón social/ })).toBeNull();
  });

  it("solo con view: la pestaña Cliente se muestra en solo lectura, sin acciones de guardado", async () => {
    renderPage("?tab=customer");
    await loaded();
    expect(await screen.findByText(/Solo lectura/)).toBeTruthy();
    await waitFor(() => expect(screen.getByLabelText("Condición predeterminada para ventas")).toBeTruthy());
    expect((screen.getByLabelText("Condición predeterminada para ventas") as HTMLSelectElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: "Guardar" })).toBeNull();
  });

  it("solo con view: Ubicaciones y Contactos no ofrecen altas ni acciones", async () => {
    renderPage("?tab=locations");
    await loaded();
    expect((screen.getByRole("button", { name: /Nueva ubicación/ }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: "Editar" })).toBeNull();
  });
});

describe("ficha unificada — cambio de empresa", () => {
  it("al cambiar de empresa se relee la ficha y se reconsultan las secciones por empresa", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
    renderPage("?tab=supplier");
    await loaded();
    await waitFor(() => expect(businessPartnerFacade.getPurchaseSettings).toHaveBeenCalledTimes(1));
    const bpCalls = vi.mocked(businessPartnerFacade.getBusinessPartner).mock.calls.length;

    act(() => useAuthStore.setState({ companySessionVersion: useAuthStore.getState().companySessionVersion + 1 }));

    await waitFor(() =>
      expect(vi.mocked(businessPartnerFacade.getBusinessPartner).mock.calls.length).toBeGreaterThan(bpCalls),
    );
    await waitFor(() => expect(businessPartnerFacade.getPurchaseSettings).toHaveBeenCalledTimes(2));
    expect(vi.mocked(businessPartnerFacade.getRetentionDefaults).mock.calls.length).toBeGreaterThanOrEqual(2);
  });
});

describe("ficha unificada — cambios sin guardar", () => {
  async function editLegalName() {
    renderPage("?tab=general");
    const input = await screen.findByLabelText(/Razón social/);
    fireEvent.change(input, { target: { value: "Cliente Dos" } });
    return input as HTMLInputElement;
  }

  it("la edición se conserva al cambiar de pestaña y volver", async () => {
    await editLegalName();
    fireEvent.click(screen.getByRole("tab", { name: /^Contactos/ }));
    fireEvent.click(screen.getByRole("tab", { name: /^Datos generales/ }));
    expect((screen.getByLabelText(/Razón social/) as HTMLInputElement).value).toBe("Cliente Dos");
  });

  it("con cambios sin guardar el navegador pide confirmar al salir; sin cambios no", async () => {
    renderPage("?tab=general");
    await screen.findByLabelText(/Razón social/);
    const clean = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);

    fireEvent.change(screen.getByLabelText(/Razón social/), { target: { value: "Cliente Dos" } });
    await waitFor(() => {
      const dirty = new Event("beforeunload", { cancelable: true });
      window.dispatchEvent(dirty);
      expect(dirty.defaultPrevented).toBe(true);
    });
  });
});
