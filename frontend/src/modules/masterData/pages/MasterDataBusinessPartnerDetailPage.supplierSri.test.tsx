// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { renderBpDetail } from "./bpDetailTestRender";
import { ALL_PERMS, SUPPLIER_ROLE, makeBp, permsOf } from "./bpDetailTestFixtures";

/**
 * Config SRI y retenciones predeterminadas del proveedor (migrado desde el modal «Config SRI» del
 * listado a la pestaña Proveedor de la ficha unificada): precarga, catálogo oficial sin texto libre,
 * código ya no vigente, PATCH del rol y lista dinámica de retenciones. Datos ficticios.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    updateSupplierConfig: vi.fn(),
    getPurchaseSettings: vi.fn(),
    getRetentionDefaults: vi.fn(),
    addRetentionDefault: vi.fn(),
    setRetentionDefaultState: vi.fn(),
    getExpenseSubcategories: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({ paymentTermService: { list: vi.fn().mockResolvedValue([]) } }));
vi.mock("../api/useSriSupplierTypes", () => ({
  useSriSupplierTypes: () => ({ options: [{ code: "01", name: "Persona Natural" }], loading: false }),
}));
vi.mock("../api/useSriPaymentMethods", () => ({
  useSriPaymentMethods: () => ({ options: [{ code: "01", name: "Sin sistema financiero" }], loading: false, error: null }),
}));
vi.mock("../api/useSriTaxSupportCodes", () => ({
  useSriTaxSupportCodes: () => ({
    options: [
      { code: "01", name: "Crédito Tributario" },
      { code: "06", name: "Inventario - Crédito Tributario para declaración de IVA" },
      { code: "07", name: "Inventario - Costo o Gasto para declaración de IR" },
    ],
    loading: false,
    error: null,
  }),
}));
vi.mock("../api/useSriRetentionCodes", () => ({
  useSriRetentionCodes: (taxType: "IVA" | "RENTA") => ({
    options:
      taxType === "IVA"
        ? [{ id: "src-725", taxType: "IVA", code: "725", name: "Retención en la fuente IVA 100%", percentage: 100, appliesTo: "SUPPLIER" }]
        : [{ id: "src-303", taxType: "RENTA", code: "303", name: "Honorarios profesionales", percentage: 10, appliesTo: "SUPPLIER" }],
    loading: false,
    error: null,
  }),
}));
vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [], loading: false }),
  useSriIdTypesByUsage: () => ({ options: [], loading: false }),
  getSriIdTypeName: (code: string) => code,
}));
vi.mock("../api/useLegalEntityTypes", () => ({ useLegalEntityTypes: () => ({ options: [], loading: false }) }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));

const ROLE = {
  ...SUPPLIER_ROLE,
  supplierConfig: {
    defaultTaxSupportCode: "01",
    defaultPaymentMethodCode: "01",
    refundProviderTypeCode: "01",
    isRetentionExempt: false,
    isRequiredToKeepAccounting: true,
  },
};
const RETENTION_303 = {
  id: "srd-1", businessPartnerId: "bp-1", sriRetentionCodeId: "src-303", taxType: "RENTA", code: "303",
  codeName: "Honorarios profesionales", percentage: 10, isCatalogCodeActive: true, isActive: true, displayOrder: 0,
};

// ZHToggle no asocia el label con el <button role="switch"> (son hermanos): se sube al contenedor.
function toggleByLabel(labelText: string): HTMLElement {
  const container = screen.getByText(labelText).closest(".zh-toggle");
  if (!container) throw new Error(`No se encontró el toggle "${labelText}"`);
  return within(container as HTMLElement).getByRole("switch");
}
// ZHField envuelve label y <select>; jsdom no siempre resuelve el nombre accesible: se sube al .zh-field.
function selectByFieldLabel(labelText: string): HTMLSelectElement {
  const container = screen.getByText(labelText).closest(".zh-field");
  if (!container) throw new Error(`No se encontró el campo "${labelText}"`);
  return within(container as HTMLElement).getByRole("combobox") as HTMLSelectElement;
}
const sriSection = () => screen.getByRole("heading", { name: "Configuración SRI" }).parentElement as HTMLElement;

async function open() {
  renderBpDetail("?tab=supplier");
  await screen.findByRole("heading", { name: "Configuración SRI" });
}
const loadRole = (role = ROLE) => vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([role]));

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue(permsOf(ALL_PERMS) as unknown as ReturnType<typeof usePermissionsUi>);
  loadRole();
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getPurchaseSettings).mockResolvedValue({ paymentTermId: null } as never);
  vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getExpenseSubcategories).mockResolvedValue({
    mode: "Unconfigured", items: [], availableSubcategoryIds: [], catalog: null,
  } as never);
  vi.mocked(businessPartnerFacade.updateSupplierConfig).mockResolvedValue(undefined as never);
});
afterEach(() => cleanup());

describe("Config SRI del proveedor", () => {
  it("precarga el toggle y los demás campos desde el SupplierRoleConfigDto existente", async () => {
    await open();
    expect(toggleByLabel("Obligado a llevar contabilidad").getAttribute("aria-checked")).toBe("true");
    expect(selectByFieldLabel("Sustento tributario predeterminado").value).toBe("01");
    expect(selectByFieldLabel("Tipo de Proveedor").value).toBe("01");
    expect(businessPartnerFacade.updateSupplierConfig).not.toHaveBeenCalled();
  });

  it("el sustento tributario es un <select> del catálogo oficial, no un campo de texto", async () => {
    await open();
    expect(selectByFieldLabel("Sustento tributario predeterminado")).toBeTruthy();
    expect(screen.queryByPlaceholderText("01")).toBeNull();
  });

  it("ofrece los códigos ATS de inventario del catálogo global", async () => {
    await open();
    const options = Array.from(selectByFieldLabel("Sustento tributario predeterminado").options);
    expect(options.find((x) => x.value === "06")?.textContent).toContain("Inventario - Crédito Tributario para declaración de IVA");
    expect(options.find((x) => x.value === "07")?.textContent).toContain("Inventario - Costo o Gasto para declaración de IR");
  });

  it("un código guardado que ya no existe en el catálogo se muestra como no vigente, sin descartarlo", async () => {
    loadRole({ ...ROLE, supplierConfig: { ...ROLE.supplierConfig, defaultTaxSupportCode: "99" } });
    await open();
    expect(selectByFieldLabel("Sustento tributario predeterminado").value).toBe("99");
    expect(
      await screen.findByText("Este código ya no está activo en el catálogo. Selecciona una opción válida."),
    ).toBeTruthy();
  });

  it("se puede desactivar el toggle y el PATCH incluye isRequiredToKeepAccounting", async () => {
    await open();
    const toggle = toggleByLabel("Obligado a llevar contabilidad");
    fireEvent.click(toggle);
    expect(toggle.getAttribute("aria-checked")).toBe("false");
    fireEvent.click(within(sriSection()).getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(businessPartnerFacade.updateSupplierConfig).toHaveBeenCalledWith(
        "bp-1",
        "role-s",
        expect.objectContaining({ isRequiredToKeepAccounting: false, defaultTaxSupportCode: "01" }),
      ),
    );
  });
});

describe("Retenciones predeterminadas dinámicas del proveedor", () => {
  it("lista, agrega y desactiva retenciones predeterminadas", async () => {
    vi.mocked(businessPartnerFacade.getRetentionDefaults).mockResolvedValue([RETENTION_303]);
    vi.mocked(businessPartnerFacade.addRetentionDefault).mockResolvedValue({
      ...RETENTION_303, id: "srd-2", sriRetentionCodeId: "src-725", taxType: "IVA", code: "725",
      codeName: "Retención en la fuente IVA 100%", percentage: 100, displayOrder: 1,
    });
    vi.mocked(businessPartnerFacade.setRetentionDefaultState).mockResolvedValue({ ...RETENTION_303, isActive: false });
    await open();
    await screen.findByText("Retenciones predeterminadas");
    await waitFor(() => expect(screen.getByText(/RENTA — 303 — Honorarios profesionales/)).toBeTruthy());

    // No quedan los dos selects fijos legacy.
    expect(screen.queryByText("Código ret. IVA predeterminado")).toBeNull();
    expect(screen.queryByText("Código ret. Renta predeterminado")).toBeNull();

    fireEvent.change(selectByFieldLabel("Tipo de impuesto"), { target: { value: "IVA" } });
    fireEvent.change(selectByFieldLabel("Código de retención SRI"), { target: { value: "src-725" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar retención" }));
    await waitFor(() =>
      expect(businessPartnerFacade.addRetentionDefault).toHaveBeenCalledWith("bp-1", { sriRetentionCodeId: "src-725" }),
    );

    const row303 = screen.getByText(/RENTA — 303 — Honorarios profesionales/).closest(".md-supplier-retention-row");
    if (!row303) throw new Error("No se encontró la fila de retención 303");
    fireEvent.click(within(row303 as HTMLElement).getByRole("switch"));
    await waitFor(() =>
      expect(businessPartnerFacade.setRetentionDefaultState).toHaveBeenCalledWith("bp-1", "srd-1", {
        isActive: false, displayOrder: 0,
      }),
    );
  });
});
