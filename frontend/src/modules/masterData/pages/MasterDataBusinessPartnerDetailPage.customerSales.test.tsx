// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { renderBpDetail } from "./bpDetailTestRender";
import { ALL_PERMS, CUSTOMER_ROLE, makeBp, permsOf } from "./bpDetailTestFixtures";

/**
 * CUSTOMER-SALES-SETTINGS-UX-FIX-01 (migrado desde el modal «Empresa» del listado a la pestaña Cliente
 * de la ficha unificada): guarda y confirma sin reabrir nada; el error del servidor mantiene el
 * formulario; el valor persistido se muestra al releer; no hay envíos duplicados con un guardado pendiente.
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    getSalesSettings: vi.fn(),
    upsertSalesSettings: vi.fn(),
  },
}));
vi.mock("../api/paymentTermService", () => ({
  paymentTermService: {
    list: vi.fn().mockResolvedValue([
      { id: "term-30", code: "CRED001", name: "CRÉDITO DE 30 DÍAS", summary: "30 días", installments: 1,
        daysBetweenInstallments: 30, totalDays: 30, isActive: true },
    ]),
  },
}));
const NO_OPTIONS = { options: [], loading: false, error: null };
vi.mock("../api/useClassificationCatalogs", () => ({
  useCustomerCategories: () => NO_OPTIONS,
  useCustomerSegments: () => NO_OPTIONS,
  useCustomerCreditRatings: () => NO_OPTIONS,
  useLoyaltyTiers: () => NO_OPTIONS,
  useCustomerInvoiceFormats: () => NO_OPTIONS,
  useCustomerClassifications: () => NO_OPTIONS,
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

const settings = (paymentTermId: string | null) => ({
  id: "settings-1", businessPartnerId: "bp-1", paymentTermId, hasCustomConfiguration: true,
});
const HEADING = "Condición predeterminada para ventas";
const section = () => screen.getByRole("heading", { name: HEADING }).parentElement as HTMLElement;
const select = () => within(section()).getByRole("combobox") as HTMLSelectElement;
const saveButton = () => within(section()).getByRole("button", { name: "Guardar" }) as HTMLButtonElement;

async function openReady() {
  renderBpDetail("?tab=customer");
  await screen.findByRole("heading", { name: HEADING });
  await waitFor(() => expect(within(select()).getByRole("option", { name: /CRED001/ })).toBeTruthy());
  await waitFor(() => expect(saveButton().disabled).toBe(false));
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue(permsOf(ALL_PERMS) as unknown as ReturnType<typeof usePermissionsUi>);
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(makeBp([CUSTOMER_ROLE]));
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([]);
  vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue(settings(null) as never);
});
afterEach(() => cleanup());

describe("condición de ventas del cliente", () => {
  it("guarda, confirma y relee el valor persistido sin recargar la ficha entera", async () => {
    vi.mocked(businessPartnerFacade.upsertSalesSettings).mockResolvedValue(settings("term-30") as never);
    await openReady();
    vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue(settings("term-30") as never);
    fireEvent.change(select(), { target: { value: "term-30" } });
    fireEvent.click(saveButton());
    await waitFor(() =>
      expect(businessPartnerFacade.upsertSalesSettings).toHaveBeenCalledWith("bp-1", { paymentTermId: "term-30" }),
    );
    expect(message.success).toHaveBeenCalledWith("Condición de ventas guardada correctamente.");
    await waitFor(() => expect(businessPartnerFacade.getSalesSettings).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(select().value).toBe("term-30"));
  });

  it("si falla el guardado muestra el error del servidor y mantiene la selección", async () => {
    vi.mocked(businessPartnerFacade.upsertSalesSettings).mockRejectedValue({
      isAxiosError: true, response: { status: 409, data: { message: { user: "No se pudo guardar la condición." } } },
    });
    await openReady();
    fireEvent.change(select(), { target: { value: "term-30" } });
    fireEvent.click(saveButton());
    expect(await within(section()).findByText("No se pudo guardar la condición.")).toBeTruthy();
    expect(select().value).toBe("term-30");
    expect(message.success).not.toHaveBeenCalled();
  });

  it("al abrir con un valor persistido lo muestra y no escribe", async () => {
    vi.mocked(businessPartnerFacade.getSalesSettings).mockResolvedValue(settings("term-30") as never);
    await openReady();
    expect(select().value).toBe("term-30");
    expect(businessPartnerFacade.upsertSalesSettings).not.toHaveBeenCalled();
  });

  it("evita envíos duplicados mientras el guardado está pendiente", async () => {
    let finish!: (value: ReturnType<typeof settings>) => void;
    vi.mocked(businessPartnerFacade.upsertSalesSettings).mockReturnValue(
      new Promise((resolve) => { finish = resolve; }) as never,
    );
    await openReady();
    fireEvent.change(select(), { target: { value: "term-30" } });
    const form = saveButton().closest("form")!;
    fireEvent.submit(form);
    fireEvent.submit(form);
    expect(businessPartnerFacade.upsertSalesSettings).toHaveBeenCalledTimes(1);
    await act(async () => { finish(settings("term-30")); });
    await waitFor(() => expect(message.success).toHaveBeenCalledTimes(1));
  });
});
