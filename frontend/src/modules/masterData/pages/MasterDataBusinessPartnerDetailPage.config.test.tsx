// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  cleanup,
  fireEvent,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
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
 * BP-CONSOLIDATION-01 Etapa 2 — edición de identidad (wizard embebido) y configuración de
 * Proveedor/Cliente dentro de la ficha: mismos contratos, guardados independientes y permisos reales.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    updateBusinessPartner: vi.fn(),
    updateSupplierConfig: vi.fn(),
    updateCustomerConfig: vi.fn(),
    getSalesSettings: vi.fn(),
    upsertSalesSettings: vi.fn(),
    getPurchaseSettings: vi.fn(),
    upsertPurchaseSettings: vi.fn(),
    getRetentionDefaults: vi.fn(),
    getExpenseSubcategories: vi.fn(),
    setExpenseSubcategories: vi.fn(),
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

const renderPage = (search: string) => renderBpDetail(search);
const openTab = (name: string) => fireEvent.click(screen.getByRole("tab", { name: new RegExp(`^${name}`) }));

function allow(perms: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue(
    permsOf(perms) as unknown as ReturnType<typeof usePermissionsUi>,
  );
}

/** Contenedor de una sección identificado por su encabezado h4. */
const sectionOf = (heading: string) =>
  screen.getByRole("heading", { name: heading }).parentElement as HTMLElement;

beforeEach(() => {
  vi.clearAllMocks();
  allow(ALL_PERMS);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE]));
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
    mode: "Unconfigured",
    items: [],
    availableSubcategoryIds: [],
    catalog: null,
  } as never);
  vi.mocked(businessPartnerFacade.updateBusinessPartner).mockResolvedValue(undefined as never);
  vi.mocked(businessPartnerFacade.updateSupplierConfig).mockResolvedValue(undefined as never);
  vi.mocked(businessPartnerFacade.updateCustomerConfig).mockResolvedValue(undefined as never);
  vi.mocked(businessPartnerFacade.upsertSalesSettings).mockResolvedValue(undefined as never);
  vi.mocked(businessPartnerFacade.upsertPurchaseSettings).mockResolvedValue(undefined as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => cleanup());

describe("ficha unificada — Datos generales (wizard embebido)", () => {
  it("edita la identidad con el mismo PUT, deja la identificación bloqueada y relee la ficha", async () => {
    renderPage("?tab=general");
    const name = (await screen.findByLabelText(/Razón social/)) as HTMLInputElement;
    expect(name.value).toBe("Cliente Uno");
    expect((screen.getByLabelText(/Número de identificación/) as HTMLInputElement).disabled).toBe(true);

    fireEvent.change(name, { target: { value: "Cliente Dos" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));

    await waitFor(() =>
      expect(businessPartnerFacade.updateBusinessPartner).toHaveBeenCalledWith(
        "bp-1",
        expect.objectContaining({ legalName: "Cliente Dos", tradeName: null, countryCode: "EC" }),
      ),
    );
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Datos actualizados correctamente."));
    expect(vi.mocked(businessPartnerFacade.getBusinessPartner).mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it("un socio con rol Proveedor se edita sin exigir «Tipo de Proveedor» (es de la configuración SRI)", async () => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([SUPPLIER_ROLE]));
    renderBpDetail("?tab=general", { context: "supplier" });
    fireEvent.change(await screen.findByLabelText(/Razón social/), { target: { value: "Proveedor Dos" } });
    expect(screen.queryByLabelText(/Tipo de Proveedor/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateBusinessPartner).toHaveBeenCalledWith(
        "bp-1",
        expect.objectContaining({ legalName: "Proveedor Dos" }),
      ),
    );
  });

  it("un fallo del servidor se informa en el formulario y no se anuncia como éxito", async () => {
    vi.mocked(businessPartnerFacade.updateBusinessPartner).mockRejectedValue(new Error("boom"));
    renderPage("?tab=general");
    fireEvent.change(await screen.findByLabelText(/Razón social/), { target: { value: "Otro" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));
    expect(await screen.findByText(/Error al guardar|boom/)).toBeTruthy();
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("ficha de cliente — pestañas Clasificación y Condiciones de ventas", () => {
  it("guarda la clasificación con el PATCH del rol y la condición de ventas por separado", async () => {
    renderPage("?tab=classification");
    const classification = await screen.findByRole("heading", { name: "Clasificación comercial" });
    expect((await within(classification.parentElement!).findByLabelText("Zona de ventas") as HTMLInputElement).value).toBe("Sur");

    fireEvent.change(within(sectionOf("Clasificación comercial")).getByLabelText("Zona de ventas"), {
      target: { value: "Norte" },
    });
    fireEvent.click(within(sectionOf("Clasificación comercial")).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith(
        "bp-1",
        "role-c",
        expect.objectContaining({ salesZone: "Norte" }),
      ),
    );
    expect(businessPartnerFacade.upsertSalesSettings).not.toHaveBeenCalled();

    openTab("Condiciones de ventas");
    const sales = sectionOf("Condición predeterminada para ventas");
    await waitFor(() =>
      expect(within(sales).getByLabelText("Condición predeterminada para ventas")).toBeTruthy(),
    );
    await waitFor(() =>
      expect((within(sales).getByRole("option", { name: /CONT/ }) as HTMLOptionElement).disabled).toBe(false),
    );
    fireEvent.change(within(sales).getByLabelText("Condición predeterminada para ventas"), {
      target: { value: "pt1" },
    });
    fireEvent.click(within(sales).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.upsertSalesSettings).toHaveBeenCalledWith("bp-1", { paymentTermId: "pt1" }),
    );
  });

  it("con update pero sin configure-company la clasificación se edita y la condición de ventas queda en solo lectura", async () => {
    allow([PERM.view, PERM.update]);
    renderPage("?tab=classification");
    const classification = await screen.findByRole("heading", { name: "Clasificación comercial" });
    expect(within(classification.parentElement!).getByRole("button", { name: "Guardar" })).toBeTruthy();
    openTab("Condiciones de ventas");
    const sales = sectionOf("Condición predeterminada para ventas");
    await waitFor(() =>
      expect((within(sales).getByLabelText("Condición predeterminada para ventas") as HTMLSelectElement).disabled).toBe(true),
    );
    expect(within(sales).queryByRole("button", { name: "Guardar" })).toBeNull();
  });
});

describe("ficha de proveedor — pestañas de configuración", () => {
  beforeEach(() => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([SUPPLIER_ROLE]));
  });

  it("guarda la configuración SRI con el PATCH del rol y no toca la condición de compras", async () => {
    renderPage("?tab=sri");
    const sri = await screen.findByRole("heading", { name: "Configuración SRI" });
    fireEvent.change(within(sri.parentElement!).getByLabelText("Tipo de Proveedor"), { target: { value: "01" } });
    fireEvent.click(within(sri.parentElement!).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateSupplierConfig).toHaveBeenCalledWith(
        "bp-1",
        "role-s",
        expect.objectContaining({ refundProviderTypeCode: "01" }),
      ),
    );
    expect(businessPartnerFacade.upsertPurchaseSettings).not.toHaveBeenCalled();
  });

  it("guarda la condición de compras de forma independiente", async () => {
    renderPage("?tab=purchases");
    const purchase = await screen.findByRole("heading", { name: "Condición predeterminada para compras/gastos" });
    const select = within(purchase.parentElement!).getByLabelText(/Condición predeterminada para compras/);
    await waitFor(() => expect(within(select).getByRole("option", { name: /CONT/ })).toBeTruthy());
    fireEvent.change(select, { target: { value: "pt1" } });
    fireEvent.click(within(purchase.parentElement!).getByRole("button", { name: "Guardar condición de compras/gastos" }));
    await waitFor(() =>
      expect(businessPartnerFacade.upsertPurchaseSettings).toHaveBeenCalledWith("bp-1", { paymentTermId: "pt1" }),
    );
    expect(businessPartnerFacade.updateSupplierConfig).not.toHaveBeenCalled();
  });

  it("con update pero sin configure-company: SRI editable; compras y retenciones en solo lectura", async () => {
    allow([PERM.view, PERM.update]);
    renderPage("?tab=sri");
    const sri = await screen.findByRole("heading", { name: "Configuración SRI" });
    expect(within(sri.parentElement!).getByRole("button", { name: "Guardar" })).toBeTruthy();
    openTab("Compras");
    const purchase = screen.getByRole("heading", { name: "Condición predeterminada para compras/gastos" }).parentElement!;
    expect((within(purchase).getByLabelText(/Condición predeterminada para compras/) as HTMLSelectElement).disabled).toBe(true);
    expect(within(purchase).queryByRole("button", { name: /Guardar condición/ })).toBeNull();
    openTab("Retenciones");
    expect(screen.queryByRole("button", { name: "Agregar retención" })).toBeNull();
  });

  const SUBCATEGORIES = {
    mode: "Restricted",
    items: [
      { expenseSubcategoryId: "s1", code: "AGUA", name: "Agua", categoryName: "Servicios", typeName: "Operativo", isActive: true, isSubcategoryActive: true },
    ],
    availableSubcategoryIds: ["s1"],
    catalog: [
      { expenseSubcategoryId: "s1", code: "AGUA", name: "Agua", categoryName: "Servicios", typeName: "Operativo" },
      { expenseSubcategoryId: "s2", code: "LUZ", name: "Luz", categoryName: "Servicios", typeName: "Operativo" },
    ],
  };

  it("subcategorías de gasto: sin configure-company (aunque tenga update) no hay vía de modificación", async () => {
    allow([PERM.view, PERM.update]);
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue(SUBCATEGORIES as never);
    renderPage("?tab=expenseSubcategories");
    expect(await screen.findByText(/Operativo › Servicios › AGUA — Agua/)).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
    expect(screen.queryByRole("button", { name: "Guardar subcategorías" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Quitar restricción" })).toBeNull();
    expect(businessPartnerFacade.setExpenseSubcategories).not.toHaveBeenCalled();
  });

  it("subcategorías de gasto: con configure-company se edita y se guarda con su propio PUT", async () => {
    allow([PERM.view, PERM.configure]);
    vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue(SUBCATEGORIES as never);
    vi.mocked(businessPartnerFacade.setExpenseSubcategories).mockResolvedValue({ ...SUBCATEGORIES, catalog: null } as never);
    renderPage("?tab=expenseSubcategories");
    fireEvent.click(await screen.findByLabelText("LUZ — Luz"));
    fireEvent.click(screen.getByRole("button", { name: "Guardar subcategorías" }));
    await waitFor(() =>
      expect(businessPartnerFacade.setExpenseSubcategories).toHaveBeenCalledWith("bp-1", {
        expenseSubcategoryIds: ["s1", "s2"],
      }),
    );
    expect(businessPartnerFacade.updateSupplierConfig).not.toHaveBeenCalled();
  });

  it("con configure-company pero sin update: SRI en solo lectura; compras editable", async () => {
    allow([PERM.view, PERM.configure]);
    renderPage("?tab=sri");
    const sri = await screen.findByRole("heading", { name: "Configuración SRI" });
    expect(within(sri.parentElement!).queryByRole("button", { name: "Guardar" })).toBeNull();
    expect((within(sri.parentElement!).getByLabelText("Tipo de Proveedor") as HTMLSelectElement).disabled).toBe(true);
    openTab("Compras");
    expect(screen.getByRole("button", { name: "Guardar condición de compras/gastos" })).toBeTruthy();
    openTab("Retenciones");
    expect(screen.getByRole("button", { name: "Agregar retención" })).toBeTruthy();
  });
});

describe("RUC compartido: Cliente y Proveedor sobre el mismo Business Partner no mezclan configuraciones (API simulada)", () => {
  beforeEach(() => {
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
    vi.mocked(businessPartnerFacade.updateCustomerConfig).mockResolvedValue(undefined as never);
    vi.mocked(businessPartnerFacade.updateSupplierConfig).mockResolvedValue(undefined as never);
  });

  it("en la ficha de cliente, guardar la clasificación usa solo el rol Cliente", async () => {
    renderBpDetail("?tab=classification", { context: "customer" });
    const classification = await screen.findByRole("heading", { name: "Clasificación comercial" });
    fireEvent.change(within(classification.parentElement!).getByLabelText("Zona de ventas"), { target: { value: "Norte" } });
    fireEvent.click(within(classification.parentElement!).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateCustomerConfig).toHaveBeenCalledWith("bp-1", "role-c", expect.objectContaining({ salesZone: "Norte" })),
    );
    expect(businessPartnerFacade.updateSupplierConfig).not.toHaveBeenCalled();
    expect(businessPartnerFacade.upsertPurchaseSettings).not.toHaveBeenCalled();
  });

  it("en la ficha de proveedor, guardar SRI usa solo el rol Proveedor", async () => {
    renderBpDetail("?tab=sri", { context: "supplier" });
    const sri = await screen.findByRole("heading", { name: "Configuración SRI" });
    fireEvent.change(within(sri.parentElement!).getByLabelText("Tipo de Proveedor"), { target: { value: "01" } });
    fireEvent.click(within(sri.parentElement!).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateSupplierConfig).toHaveBeenCalledWith("bp-1", "role-s", expect.objectContaining({ refundProviderTypeCode: "01" })),
    );
    expect(businessPartnerFacade.updateCustomerConfig).not.toHaveBeenCalled();
    expect(businessPartnerFacade.upsertSalesSettings).not.toHaveBeenCalled();
  });

  it("la ficha de cliente no consulta configuraciones de proveedor ni la de proveedor las de cliente", async () => {
    renderBpDetail("?tab=sales", { context: "customer" });
    await screen.findByRole("heading", { name: "Condición predeterminada para ventas" });
    expect(businessPartnerFacade.getPurchaseSettings).not.toHaveBeenCalled();
    expect(businessPartnerFacade.getRetentionDefaults).not.toHaveBeenCalled();
    expect(businessPartnerFacade.getExpenseSubcategories).not.toHaveBeenCalled();
    cleanup();
    vi.clearAllMocks();
    vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE, SUPPLIER_ROLE]));
    renderBpDetail("?tab=purchases", { context: "supplier" });
    await screen.findByRole("heading", { name: "Condición predeterminada para compras/gastos" });
    expect(businessPartnerFacade.getSalesSettings).not.toHaveBeenCalled();
  });
});
