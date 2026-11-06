// @vitest-environment jsdom
import React from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { z } from "zod";
import { useCatalogCrud } from "./useCatalogCrud";
import { message } from "../../../../lib/messages";
import { I18nProvider } from "../../../../i18n/i18n";

/**
 * FUNC-DEACTIVATION-CONFIRM-01 (AA-D5) — Marcas / Grupos / Definiciones de atributo comparten
 * useCatalogCrud: desactivar confirma, cancelar no llama al backend, una sola petición por
 * confirmación, error visible, permisos conservados y activar sin confirmación (sin cambios).
 */

const permissions = { allowed: true };
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => permissions.allowed }),
}));

vi.mock("../../../../lib/messages", () => ({
  message: { confirm: vi.fn(), success: vi.fn(), error: vi.fn() },
}));

interface Dto {
  id: string;
  name: string;
  isActive: boolean;
}

const ACTIVE: Dto = { id: "brand-1", name: "Marca Ficticia", isActive: true };
const INACTIVE: Dto = { id: "brand-2", name: "Marca Inactiva", isActive: false };

const service = {
  list: vi.fn<() => Promise<Dto[]>>(),
  create: vi.fn(),
  update: vi.fn(),
  enable: vi.fn<(id: string) => Promise<boolean>>(),
  disable: vi.fn<(id: string) => Promise<boolean>>(),
};

const schema = z.object({ name: z.string() });

function useSubject() {
  return useCatalogCrud<Dto, { name: string }>({
    service,
    schema,
    emptyForm: () => ({ name: "" }),
    toCreatePayload: (f) => f,
    toUpdatePayload: (_id, f) => f,
    toFormValues: (d) => ({ name: d.name }),
    getId: (d) => d.id,
    permissionPrefix: "items.brands",
  });
}

async function renderReady() {
  const hook = renderHook(() => useSubject(), {
    wrapper: ({ children }: { children: React.ReactNode }) =>
      React.createElement(I18nProvider, null, children),
  });
  await waitFor(() => expect(hook.result.current.loading).toBe(false));
  return hook;
}

beforeEach(() => {
  vi.clearAllMocks();
  permissions.allowed = true;
  service.list.mockResolvedValue([ACTIVE, INACTIVE]);
  service.disable.mockResolvedValue(true);
  service.enable.mockResolvedValue(true);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("useCatalogCrud — desactivar con confirmación", () => {
  it("cancelar no envía petición al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(vi.mocked(message.confirm).mock.calls[0][0]).toMatchObject({
      title: 'Desactivar "Marca Ficticia"',
      confirmLabel: "Desactivar",
      cancelLabel: "Cancelar",
      variant: "danger",
    });
    expect(service.disable).not.toHaveBeenCalled();
  });

  it("confirmar envía una única petición aunque se pulse dos veces", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await Promise.all([
        result.current.toggleDisable(ACTIVE),
        result.current.toggleDisable(ACTIVE),
      ]);
    });
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(service.disable).toHaveBeenCalledTimes(1);
    expect(service.disable).toHaveBeenCalledWith("brand-1");
  });

  it("expone el estado de carga mientras la petición está pendiente", async () => {
    let resolve: (v: boolean) => void = () => {};
    service.disable.mockReturnValue(new Promise<boolean>((r) => (resolve = r)));
    const { result } = await renderReady();
    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.toggleDisable(ACTIVE);
    });
    await waitFor(() => expect(result.current.togglingId).toBe("brand-1"));
    await act(async () => {
      resolve(true);
      await pending;
    });
    expect(result.current.togglingId).toBeNull();
  });

  it("muestra el error del backend y libera el estado de carga", async () => {
    service.disable.mockRejectedValue(new Error("boom"));
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(result.current.error).toBe("Error al cambiar el estado.");
    expect(result.current.togglingId).toBeNull();
  });

  it("sin permiso no confirma ni llama al backend", async () => {
    permissions.allowed = false;
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(message.confirm).not.toHaveBeenCalled();
    expect(service.disable).not.toHaveBeenCalled();
  });

  it("activar conserva el flujo directo sin confirmación", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(INACTIVE);
    });
    expect(message.confirm).not.toHaveBeenCalled();
    expect(service.enable).toHaveBeenCalledTimes(1);
    expect(service.enable).toHaveBeenCalledWith("brand-2");
  });
});
