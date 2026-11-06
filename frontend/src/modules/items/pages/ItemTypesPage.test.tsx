// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ItemTypesPage } from "./ItemTypesPage";
import { itemTypeService, type ItemTypeDto } from "../api/itemTypeService";
import { message } from "../../../lib/messages";
import { I18nProvider } from "../../../i18n/i18n";

/**
 * FUNC-DEACTIVATION-CONFIRM-01 (AA-D5) — Tipos de ítem: desactivar confirma, cancelar no llama
 * al backend, una sola petición, error real del backend (ítems activos con el tipo), botón
 * deshabilitado mientras está pendiente, permiso items.edit conservado y activar sin confirmar.
 */

const permissions = { edit: true };
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    canShow: (perm: string) => (perm === "items.edit" ? permissions.edit : true),
  }),
}));

vi.mock("../../../lib/messages", () => ({
  message: { confirm: vi.fn(), success: vi.fn(), error: vi.fn() },
}));

vi.mock("../api/itemTypeService", () => ({
  itemTypeService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    toggle: vi.fn(),
  },
}));

const ACTIVE: ItemTypeDto = { id: "t-1", code: "FIS", name: "Tipo Ficticio", isActive: true, sortOrder: 1 };
const INACTIVE: ItemTypeDto = { id: "t-2", code: "SRV", name: "Tipo Inactivo", isActive: false, sortOrder: 2 };
const service = vi.mocked(itemTypeService);

async function renderPage() {
  render(
    <I18nProvider>
      <MemoryRouter>
        <ItemTypesPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  await screen.findByText("Tipo Ficticio");
}

beforeEach(() => {
  vi.clearAllMocks();
  permissions.edit = true;
  service.list.mockResolvedValue([ACTIVE, INACTIVE]);
  service.toggle.mockResolvedValue(ACTIVE);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(cleanup);

describe("ItemTypesPage — desactivar con confirmación", () => {
  it("cancelar no envía petición al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await renderPage();
    fireEvent.click(screen.getByTitle("Desactivar"));
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(vi.mocked(message.confirm).mock.calls[0][0]).toMatchObject({
      title: 'Desactivar tipo de ítem "FIS — Tipo Ficticio"',
      confirmLabel: "Desactivar",
      cancelLabel: "Cancelar",
    });
    expect(service.toggle).not.toHaveBeenCalled();
  });

  it("confirmar envía una única petición aunque se pulse dos veces", async () => {
    await renderPage();
    const btn = screen.getByTitle("Desactivar");
    fireEvent.click(btn);
    fireEvent.click(btn);
    await waitFor(() => expect(message.success).toHaveBeenCalledWith("Tipo de ítem desactivado."));
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(service.toggle).toHaveBeenCalledTimes(1);
    expect(service.toggle).toHaveBeenCalledWith("t-1");
  });

  it("deshabilita los botones de estado mientras la petición está pendiente", async () => {
    let resolve: (v: ItemTypeDto) => void = () => {};
    service.toggle.mockReturnValue(new Promise<ItemTypeDto>((r) => (resolve = r)));
    await renderPage();
    fireEvent.click(screen.getByTitle("Desactivar"));
    await waitFor(() => expect(screen.getByTitle("Desactivar")).toHaveProperty("disabled", true));
    expect(screen.getByTitle("Activar")).toHaveProperty("disabled", true);
    await act(async () => resolve(ACTIVE));
    await waitFor(() => expect(screen.getByTitle("Desactivar")).toHaveProperty("disabled", false));
  });

  it("registro no elegible: muestra el motivo del backend", async () => {
    service.toggle.mockRejectedValue({
      response: { data: { message: { user: "No se puede desactivar: existen ítems activos con este tipo." } } },
    });
    await renderPage();
    fireEvent.click(screen.getByTitle("Desactivar"));
    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith(
        "No se puede desactivar: existen ítems activos con este tipo.",
      ),
    );
    expect(screen.getByTitle("Desactivar")).toHaveProperty("disabled", false);
  });

  it("sin items.edit no ofrece la acción", async () => {
    permissions.edit = false;
    await renderPage();
    expect(screen.queryByTitle("Desactivar")).toBeNull();
    expect(screen.queryByTitle("Activar")).toBeNull();
  });

  it("activar conserva el flujo directo sin confirmación", async () => {
    await renderPage();
    fireEvent.click(screen.getByTitle("Activar"));
    await waitFor(() => expect(service.toggle).toHaveBeenCalledWith("t-2"));
    expect(message.confirm).not.toHaveBeenCalled();
  });
});
