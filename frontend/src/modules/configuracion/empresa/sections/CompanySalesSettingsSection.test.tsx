// @vitest-environment jsdom
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ensureLocaleLoaded } from "../../../../i18n/dictionaries";
import { useAuthStore } from "../../../../store/authStore";
import { useSessionStore } from "../../../../store/sessionStore";
import { CompanySalesSettingsSection } from "./CompanySalesSettingsSection";

// SALES-PREFERENCES-INTEGRITY-01 — un único PUT transaccional; «No configurado» → null;
// la lista solo viaja si cambió y el usuario tiene pricing.update; advertencias visibles.

const TERM = "11111111-1111-4111-8111-111111111111";
const GENERAL = "22222222-2222-4222-8222-222222222222";
const MAYORISTA = "33333333-3333-4333-8333-333333333333";

const getCompanyInvoiceDefaults = vi.fn();
const upsertCompanyInvoiceDefaults = vi.fn();
vi.mock("../api/orgConfigService", () => ({
  orgConfigService: {
    getCompanyInvoiceDefaults: () => getCompanyInvoiceDefaults(),
    upsertCompanyInvoiceDefaults: (body: unknown) => upsertCompanyInvoiceDefaults(body),
  },
}));
vi.mock("../../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: async () => [
      { code: "01", name: "Factura" },
      { code: "07", name: "Comprobante de retención" },
    ],
    paymentMethods: async () => [{ code: "20", name: "Otros con utilización del sistema financiero" }],
  },
}));
vi.mock("../../../masterData/facades/paymentTermLookupFacade", () => ({
  paymentTermLookupFacade: { list: async () => [{ id: TERM, name: "Contado", isActive: true }] },
}));
vi.mock("../../../pricing/facades/priceListLookupFacade", () => ({
  priceListLookupFacade: {
    list: async () => [
      { id: GENERAL, code: "GEN", name: "General", isDefault: true },
      { id: MAYORISTA, code: "MAY", name: "Mayorista", isDefault: false },
    ],
  },
}));

const BASE_PERMISSIONS = ["settings.company.view", "erp.companies.update"];

function stored(overrides: Record<string, unknown> = {}) {
  return {
    defaultDocTypeCode: "01",
    defaultSriPaymentMethodCode: "20",
    defaultPaymentTermId: TERM,
    defaultPriceListId: GENERAL,
    configurationWarnings: [],
    allowedDocTypeCodes: ["01"],
    ...overrides,
  };
}

function signIn(permissions: string[]) {
  useAuthStore.setState({ user: { userId: "u", fullName: "U", username: "u", role: "User" } as never, isAuthenticated: true });
  useSessionStore.setState({ isLoaded: true, authorization: { roles: ["User"], permissions } });
}

async function openEditor(permissions: string[]) {
  signIn(permissions);
  render(
    <I18nProvider>
      <CompanySalesSettingsSection />
    </I18nProvider>,
  );
  fireEvent.click(await screen.findByRole("button", { name: /Editar/ }));
  await screen.findByText("Factura", { exact: false, selector: "option" });
  // Orden en el formulario: lista de precios, tipo de documento, forma de pago SRI, condición de pago.
  const [priceList, docType, paymentMethod, paymentTerm] = screen.getAllByRole("combobox") as HTMLSelectElement[];
  return { priceList, docType, paymentMethod, paymentTerm };
}

async function save() {
  fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));
  await waitFor(() => expect(upsertCompanyInvoiceDefaults).toHaveBeenCalledTimes(1));
  return upsertCompanyInvoiceDefaults.mock.calls[0][0];
}

beforeAll(async () => { await ensureLocaleLoaded("es"); });
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  useAuthStore.setState({ user: null, isAuthenticated: false });
  useSessionStore.setState({ isLoaded: false, authorization: null });
});

describe("CompanySalesSettingsSection — integridad de preferencias de ventas", () => {
  it("«No configurado» en los cuatro selectores envía null en un único PUT y quita la lista", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    upsertCompanyInvoiceDefaults.mockResolvedValue(stored());
    const fields = await openEditor([...BASE_PERMISSIONS, "pricing.update"]);

    for (const select of Object.values(fields)) fireEvent.change(select, { target: { value: "" } });

    expect(await save()).toEqual({
      defaultDocTypeCode: null,
      defaultSriPaymentMethodCode: null,
      defaultPaymentTermId: null,
      defaultPriceList: { priceListId: null },
    });
  });

  it("reasignar la lista viaja en el mismo PUT que las demás preferencias", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    upsertCompanyInvoiceDefaults.mockResolvedValue(stored({ defaultPriceListId: MAYORISTA }));
    const { priceList } = await openEditor([...BASE_PERMISSIONS, "pricing.update"]);

    fireEvent.change(priceList, { target: { value: MAYORISTA } });

    expect(await save()).toEqual({
      defaultDocTypeCode: "01",
      defaultSriPaymentMethodCode: "20",
      defaultPaymentTermId: TERM,
      defaultPriceList: { priceListId: MAYORISTA },
    });
  });

  it("el selector de tipo de documento solo ofrece los tipos que publica el backend", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    const { docType } = await openEditor([...BASE_PERMISSIONS, "pricing.update"]);

    const options = Array.from(docType.options).map((o) => o.value);
    expect(options).toEqual(["", "01"]);
  });

  it("sin cambio de lista no envía defaultPriceList", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    upsertCompanyInvoiceDefaults.mockResolvedValue(stored());
    const { paymentTerm } = await openEditor([...BASE_PERMISSIONS, "pricing.update"]);

    fireEvent.change(paymentTerm, { target: { value: "" } });

    const payload = await save();
    expect(payload).not.toHaveProperty("defaultPriceList");
    expect(payload.defaultPaymentTermId).toBeNull();
  });

  it("sin pricing.update la lista queda bloqueada y nunca se envía", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    upsertCompanyInvoiceDefaults.mockResolvedValue(stored());
    const { priceList, docType } = await openEditor(BASE_PERMISSIONS);

    expect(priceList.disabled).toBe(true);
    expect(screen.getByText(/requiere permiso para editar Pricing/)).toBeTruthy();
    fireEvent.change(docType, { target: { value: "" } });

    const payload = await save();
    expect(payload).not.toHaveProperty("defaultPriceList");
    expect(payload.defaultDocTypeCode).toBeNull();
  });

  it("si el PUT falla el modal sigue abierto con el error y no hay escrituras adicionales", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(stored());
    upsertCompanyInvoiceDefaults.mockRejectedValue(new Error("boom"));
    const { priceList } = await openEditor([...BASE_PERMISSIONS, "pricing.update"]);

    fireEvent.change(priceList, { target: { value: MAYORISTA } });
    await save();

    expect(await screen.findByRole("button", { name: /Guardar/ })).toBeTruthy();
    expect(upsertCompanyInvoiceDefaults).toHaveBeenCalledTimes(1);
  });

  it("muestra las advertencias de configuraciones antiguas inválidas", async () => {
    getCompanyInvoiceDefaults.mockResolvedValue(
      stored({ defaultDocTypeCode: "07", configurationWarnings: ["El tipo de documento predeterminado debe ser Factura (01)."] }),
    );
    signIn(BASE_PERMISSIONS);
    render(
      <I18nProvider>
        <CompanySalesSettingsSection />
      </I18nProvider>,
    );

    expect(await screen.findByText(/ya no son válidas/)).toBeTruthy();
    expect(screen.getByText(/debe ser Factura \(01\)/)).toBeTruthy();
  });
});
