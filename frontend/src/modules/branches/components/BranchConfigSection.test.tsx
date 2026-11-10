// @vitest-environment jsdom
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ensureLocaleLoaded } from "../../../i18n/dictionaries";
import { useAuthStore } from "../../../store/authStore";
import { useSessionStore } from "../../../store/sessionStore";
import { BranchConfigSection } from "./BranchConfigSection";

// BRANCH-CONFIG-WAREHOUSE-UNSET-01 — «Sin configurar» envía null (fallback a la bodega
// principal); una bodega guardada que ya no es válida se advierte en lugar de mostrarse como
// «sin configurar». BRANCH-CONFIG-READ-PERMISSION-01 — Editar exige erp.companies.update (PUT) e
// inventory.warehouses.view (selector); un error de carga se muestra como error, nunca como
// ausencia de configuración.

const BRANCH = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const MAIN = "11111111-1111-4111-8111-111111111111";
const SECONDARY = "22222222-2222-4222-8222-222222222222";
const STALE = "33333333-3333-4333-8333-333333333333";
const INVALID_WAREHOUSE_ERROR =
  "La bodega seleccionada no existe, está inactiva o no pertenece a esta sucursal.";

const EDIT_PERMISSIONS = ["erp.companies.update", "inventory.warehouses.view"];

const getBranchInvoiceDefaults = vi.fn();
const listWarehouses = vi.fn();
const upsertBranchInvoiceDefaults = vi.fn();
vi.mock("../../configuracion/empresa/facades/branchInvoiceDefaultsFacade", () => ({
  branchInvoiceDefaultsFacade: {
    getBranchInvoiceDefaults: (branchId: string) => getBranchInvoiceDefaults(branchId),
    upsertBranchInvoiceDefaults: (branchId: string, body: unknown) =>
      upsertBranchInvoiceDefaults(branchId, body),
  },
}));
vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({
  warehouseLookupFacade: { list: () => listWarehouses() },
}));

function signIn(permissions: string[]) {
  useAuthStore.setState({ user: { userId: "u", fullName: "U", username: "u", role: "User" } as never, isAuthenticated: true });
  useSessionStore.setState({ isLoaded: true, authorization: { roles: ["User"], permissions } });
}

function renderSection(permissions: string[]) {
  signIn(permissions);
  render(
    <I18nProvider>
      <BranchConfigSection branchId={BRANCH} />
    </I18nProvider>,
  );
}

async function openEditor() {
  fireEvent.click(await screen.findByRole("button", { name: /Editar/ }));
  await screen.findByText("Secundaria", { exact: false, selector: "option" });
  return screen.getByRole("combobox") as HTMLSelectElement;
}

beforeAll(async () => { await ensureLocaleLoaded("es"); });
beforeEach(() => {
  listWarehouses.mockResolvedValue([
    { id: MAIN, name: "Principal", code: "P01" },
    { id: SECONDARY, name: "Secundaria", code: "S01" },
  ]);
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  useAuthStore.setState({ user: null, isAuthenticated: false });
  useSessionStore.setState({ isLoaded: false, authorization: null });
});

describe("BranchConfigSection — bodega por defecto de la sucursal", () => {
  it("«Sin configurar» envía null en el PUT", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: SECONDARY });
    upsertBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    renderSection(EDIT_PERMISSIONS);
    const select = await openEditor();
    expect(select.value).toBe(SECONDARY);

    fireEvent.change(select, { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));

    await waitFor(() => expect(upsertBranchInvoiceDefaults).toHaveBeenCalledTimes(1));
    expect(upsertBranchInvoiceDefaults).toHaveBeenCalledWith(BRANCH, { defaultWarehouseId: null });
  });

  it("una bodega activa envía su id", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    upsertBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: MAIN });
    renderSection(EDIT_PERMISSIONS);
    const select = await openEditor();

    fireEvent.change(select, { target: { value: MAIN } });
    fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));

    await waitFor(() => expect(upsertBranchInvoiceDefaults).toHaveBeenCalledTimes(1));
    expect(upsertBranchInvoiceDefaults).toHaveBeenCalledWith(BRANCH, { defaultWarehouseId: MAIN });
  });

  it("sin preferencia explica que se usa la bodega principal", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    renderSection(EDIT_PERMISSIONS);

    expect(await screen.findByText(/usan la bodega principal de la sucursal/)).toBeTruthy();
  });

  it("el rechazo 422 del servidor se muestra en el formulario", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    upsertBranchInvoiceDefaults.mockRejectedValue(new Error(INVALID_WAREHOUSE_ERROR));
    renderSection(EDIT_PERMISSIONS);
    const select = await openEditor();

    fireEvent.change(select, { target: { value: MAIN } });
    fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));

    expect(await screen.findByText(INVALID_WAREHOUSE_ERROR, { exact: false })).toBeTruthy();
  });

  it("Editar no aparece con settings.branches.update sin erp.companies.update", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    renderSection(["settings.branches.view", "settings.branches.update", "settings.company.view"]);

    await screen.findByText("Bodega por Defecto");
    expect(screen.queryByRole("button", { name: /Editar/ })).toBeNull();
  });

  it("una bodega guardada que ya no es válida se advierte en lugar de mostrarse sin configurar", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: STALE });
    renderSection(EDIT_PERMISSIONS);

    expect(await screen.findByText("Bodega no válida")).toBeTruthy();
    expect(screen.getByText(/ya no es válida/)).toBeTruthy();
    expect(screen.queryByText(/usan la bodega principal de la sucursal/)).toBeNull();
  });

  it("Editar no aparece sin inventory.warehouses.view aunque tenga erp.companies.update", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: null });
    renderSection(["erp.companies.update", "settings.company.view"]);

    await screen.findByText("Bodega por Defecto");
    expect(screen.queryByRole("button", { name: /Editar/ })).toBeNull();
  });

  it("un error al cargar la configuración se muestra como error, sin fallback ni Editar", async () => {
    getBranchInvoiceDefaults.mockRejectedValue(
      new Error("No tiene autorización para operar en esta sucursal."),
    );
    renderSection(EDIT_PERMISSIONS);

    expect(await screen.findByText("No se pudo cargar la configuración de la sucursal.")).toBeTruthy();
    expect(screen.queryByText(/usan la bodega principal de la sucursal/)).toBeNull();
    expect(screen.queryByText("Bodega por Defecto")).toBeNull();
    expect(screen.queryByRole("button", { name: /Editar/ })).toBeNull();
  });

  it("un error al cargar las bodegas se advierte, no como «sin bodegas activas»", async () => {
    getBranchInvoiceDefaults.mockResolvedValue({ defaultWarehouseId: SECONDARY });
    listWarehouses.mockRejectedValue(new Error("Acceso denegado."));
    renderSection(EDIT_PERMISSIONS);

    expect(await screen.findByText("No se pudieron cargar las bodegas de la sucursal.")).toBeTruthy();
    expect(screen.queryByText("Bodega no válida")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Editar/ }));
    expect(screen.queryByText("No hay bodegas activas para esta sucursal.")).toBeNull();
  });
});
