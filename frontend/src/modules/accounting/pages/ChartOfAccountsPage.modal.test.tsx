// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ChartOfAccountsPage } from "./ChartOfAccountsPage";
import { accountingApi } from "../api/accountingApi";
import { message } from "../../../lib/messages";
import { account, byId, CHART_ACCOUNTS } from "./ChartOfAccountsPage.testFixtures";

/**
 * ACCOUNTING-CHART-UX-01 — crear / crear subcuenta / editar en ZHModal: padre deducido del código
 * (solo lectura, el servidor valida), mismos payloads y mensajes, errores del servidor, avisos
 * informativos de ACCOUNTING-CHART-INTEGRITY-01 sin bloqueo y recuperación del foco.
 */

vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listAccounts: vi.fn(),
    createAccount: vi.fn(),
    updateAccount: vi.fn(),
    disableAccount: vi.fn(),
    enableAccount: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

async function renderLoaded() {
  const view = render(
    <I18nProvider>
      <ChartOfAccountsPage />
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.getByText("Activo corriente")).toBeTruthy());
  return view;
}

const dialog = () => screen.getByRole("dialog");
/** Control de un ZHField por su rótulo visible (el <label> envuelve rótulo, "*" y ayuda). */
const field = (label: string) => {
  const span = Array.from(dialog().querySelectorAll(".zh-field-label")).find(
    (el) => el.firstChild?.textContent === label,
  );
  const control = span?.closest("label")?.querySelector("input, select");
  if (!control) throw new Error(`Campo "${label}" no encontrado en el modal`);
  return control as HTMLInputElement;
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({ canShow: () => true, has: () => true, isAdminRole: false });
  vi.mocked(accountingApi.listAccounts).mockResolvedValue(CHART_ACCOUNTS);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("ChartOfAccountsPage — modal Nueva cuenta", () => {
  it("abre ZHModal, enfoca el código, deduce el padre y crea con el payload esperado", async () => {
    const created = account({ id: "new", code: "1.1.03", name: "Caja chica", ...{ parentAccountId: "c11" } });
    vi.mocked(accountingApi.createAccount).mockResolvedValue(created);
    vi.mocked(accountingApi.listAccounts)
      .mockResolvedValueOnce(CHART_ACCOUNTS)
      .mockResolvedValueOnce([...CHART_ACCOUNTS, created]);
    await renderLoaded();

    const opener = screen.getByRole("button", { name: /Nueva cuenta/ });
    fireEvent.click(opener);

    expect(dialog().getAttribute("aria-label")).toBe("Nueva cuenta");
    await waitFor(() => expect(document.activeElement).toBe(field("Código")));
    expect(within(dialog()).queryByRole("combobox", { name: "Cuenta padre" })).toBeNull();
    expect(field("Cuenta padre").value).toBe("(cuenta raíz — sin padre)");

    fireEvent.change(field("Código"), { target: { value: "1.1.03" } });
    fireEvent.change(field("Nombre"), { target: { value: "Caja chica" } });
    expect(field("Cuenta padre").value).toBe("1.1 · Activo corriente");
    expect(field("Cuenta padre").disabled).toBe(true);

    fireEvent.click(within(dialog()).getByRole("button", { name: "Crear" }));

    await waitFor(() =>
      expect(accountingApi.createAccount).toHaveBeenCalledWith({
        code: "1.1.03",
        name: "Caja chica",
        parentAccountId: "c11",
        accountType: "Asset",
        nature: "Debit",
        allowsPosting: true,
      }),
    );
    expect(message.success).toHaveBeenCalledWith("Cuenta creada correctamente.");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(opener));
    // La cuenta guardada queda visible: se expanden sus ancestros (1.1 estaba contraída) tras recargar.
    await waitFor(() => expect(screen.getByText("Caja chica")).toBeTruthy());
    expect(screen.getByText("Caja general")).toBeTruthy();
  });

  it("si el padre implicado no existe, solo informa y deja que el servidor valide", async () => {
    vi.mocked(accountingApi.createAccount).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 400,
        data: { message: { user: "El código '3.1' requiere que exista primero la cuenta agrupadora '3'." } },
      },
    });
    await renderLoaded();
    fireEvent.click(screen.getByRole("button", { name: /Nueva cuenta/ }));

    fireEvent.change(field("Código"), { target: { value: "3.1" } });
    fireEvent.change(field("Nombre"), { target: { value: "Capital" } });
    expect(field("Cuenta padre").value).toBe("3 (no encontrada)");
    expect(within(dialog()).getByText(/No existe la cuenta agrupadora «3»/)).toBeTruthy();

    fireEvent.click(within(dialog()).getByRole("button", { name: "Crear" }));

    await waitFor(() =>
      expect(accountingApi.createAccount).toHaveBeenCalledWith(expect.objectContaining({ parentAccountId: null })),
    );
    await waitFor(() => expect(within(dialog()).getByText(/requiere que exista primero/)).toBeTruthy());
    expect(message.success).not.toHaveBeenCalled();
  });

  it("valida campos obligatorios con el esquema existente sin llamar al backend", async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole("button", { name: /Nueva cuenta/ }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Crear" }));

    await waitFor(() => expect(within(dialog()).getByText("El código es obligatorio.")).toBeTruthy());
    expect(accountingApi.createAccount).not.toHaveBeenCalled();
  });
});

describe("ChartOfAccountsPage — modal Crear subcuenta", () => {
  it("precarga prefijo del código, tipo y naturaleza del padre", async () => {
    vi.mocked(accountingApi.createAccount).mockResolvedValue(byId("c21"));
    await renderLoaded();

    fireEvent.click(screen.getByRole("button", { name: "Crear subcuenta de 2 Pasivo" }));

    expect(dialog().getAttribute("aria-label")).toBe("Nueva subcuenta");
    expect(within(dialog()).getByText("Dentro de: 2 · Pasivo")).toBeTruthy();
    expect(field("Código").value).toBe("2.");
    expect((field("Tipo de cuenta") as unknown as HTMLSelectElement).value).toBe("Liability");
    expect((field("Naturaleza") as unknown as HTMLSelectElement).value).toBe("Credit");
    expect(field("Cuenta padre").value).toBe("2 · Pasivo");

    fireEvent.change(field("Código"), { target: { value: "2.2" } });
    fireEvent.change(field("Nombre"), { target: { value: "Obligaciones" } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Crear" }));

    await waitFor(() =>
      expect(accountingApi.createAccount).toHaveBeenCalledWith(
        expect.objectContaining({ code: "2.2", parentAccountId: "b2", accountType: "Liability", nature: "Credit" }),
      ),
    );
  });
});

describe("ChartOfAccountsPage — modal Editar", () => {
  const openEditCaja = async () => {
    await renderLoaded();
    fireEvent.click(screen.getByRole("button", { name: /Expandir todo/ }));
    const opener = screen.getByRole("button", { name: "Editar 1.1.01 Caja general" });
    fireEvent.click(opener);
    return opener;
  };

  it("muestra código, tipo, naturaleza y padre en solo lectura y envía el padre sin cambios", async () => {
    vi.mocked(accountingApi.updateAccount).mockResolvedValue(byId("leaf1"));
    await openEditCaja();

    expect(dialog().getAttribute("aria-label")).toBe("Editar cuenta — 1.1.01");
    expect(field("Código").disabled).toBe(true);
    expect(field("Tipo").value).toBe("Activo");
    expect(field("Naturaleza").value).toBe("Débito");
    expect(field("Cuenta padre").value).toBe("1.1 · Activo corriente");
    expect(field("Cuenta padre").disabled).toBe(true);
    await waitFor(() => expect(document.activeElement).toBe(field("Nombre")));
    expect(field("Nombre").value).toBe("Caja general");
    expect((field("Clase") as unknown as HTMLSelectElement).value).toBe("true");

    fireEvent.change(field("Nombre"), { target: { value: "Caja matriz" } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Actualizar" }));

    await waitFor(() =>
      expect(accountingApi.updateAccount).toHaveBeenCalledWith("leaf1", {
        id: "leaf1",
        name: "Caja matriz",
        parentAccountId: "c11",
        allowsPosting: true,
      }),
    );
    expect(message.success).toHaveBeenCalledWith("Cuenta actualizada correctamente.");
  });

  it("al quitar movimiento muestra un aviso informativo que no bloquea ni sustituye al servidor", async () => {
    vi.mocked(accountingApi.updateAccount).mockResolvedValue(byId("leaf1"));
    await openEditCaja();

    fireEvent.change(field("Clase"), { target: { value: "false" } });

    expect(within(dialog()).getByText("La cuenta dejará de recibir asientos.")).toBeTruthy();
    expect(within(dialog()).getByText(/no sustituye las validaciones del servidor/)).toBeTruthy();

    fireEvent.click(within(dialog()).getByRole("button", { name: "Actualizar" }));
    await waitFor(() =>
      expect(accountingApi.updateAccount).toHaveBeenCalledWith("leaf1", expect.objectContaining({ allowsPosting: false })),
    );
  });

  it("los errores de campo del servidor se aplican al formulario", async () => {
    vi.mocked(accountingApi.updateAccount).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { errors: { Name: ["El nombre ya está en uso."] } } },
    });
    await openEditCaja();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Actualizar" }));

    await waitFor(() => expect(within(dialog()).getByText("El nombre ya está en uso.")).toBeTruthy());
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("Cancelar sin cambios cierra sin confirmar y devuelve el foco al botón Editar", async () => {
    const opener = await openEditCaja();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Cancelar" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(message.confirm).not.toHaveBeenCalled();
    await waitFor(() => expect(document.activeElement).toBe(opener));
  });

  it("Escape con cambios pide confirmar el descarte; si se rechaza, el modal sigue abierto", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await openEditCaja();
    fireEvent.change(field("Nombre"), { target: { value: "Otro nombre" } });

    fireEvent.keyDown(window, { key: "Escape" });

    await waitFor(() =>
      expect(message.confirm).toHaveBeenCalledWith(expect.objectContaining({ title: "Descartar cambios" })),
    );
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(accountingApi.updateAccount).not.toHaveBeenCalled();
  });
});
