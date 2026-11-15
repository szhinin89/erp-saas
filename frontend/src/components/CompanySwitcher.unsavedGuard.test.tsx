// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { useAuthStore } from "../store/authStore";
import { authService } from "../modules/auth/api/authService";
import { syncCompanySelection } from "../modules/auth/syncCompanySelection";
import { releaseDiscardApproval, useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard";
import { message } from "../lib/messages";
import { CompanySwitcher } from "./CompanySwitcher";

/**
 * BP-NAV-GUARD-01 — cambio voluntario de empresa: con cambios pendientes se confirma ANTES de
 * `switchCompany`; cancelar conserva empresa, sesión y formulario; confirmar ejecuta el cambio y la
 * navegación sin una segunda confirmación del router.
 */

vi.mock("../modules/auth/api/authService", () => ({
  authService: { listMyCompanies: vi.fn(), switchCompany: vi.fn() },
}));
vi.mock("../modules/auth/syncCompanySelection", () => ({ syncCompanySelection: vi.fn() }));
vi.mock("../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const company = (id: string, name: string) => ({
  companyId: id, tenantId: "tenant-1", legalName: `${name} S.A.`, displayName: name, ruc: "0999999999001",
  role: "Admin", isActive: true, operationalStatus: "Operational", taxRegime: null,
  isAccountingRequired: false, assignedBranchCount: 1, totalBranchCount: 1,
});

/** Pantalla con cambios sin guardar (usa el mismo mecanismo que la ficha) y el selector real. */
function FormWithPendingChanges({ unsaved }: { unsaved: boolean }) {
  useUnsavedChangesGuard(unsaved);
  return (
    <>
      <input aria-label="campo" defaultValue="inicial" />
      <CompanySwitcher />
    </>
  );
}

function Probe() {
  return <div data-testid="path">{useLocation().pathname}</div>;
}

function mount(unsaved: boolean) {
  const router = createMemoryRouter(
    [
      { path: "/settings", element: <FormWithPendingChanges unsaved={unsaved} /> },
      { path: "*", element: <Probe /> },
    ],
    { initialEntries: ["/settings"] },
  );
  return render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
}

const changeCompany = async () => {
  const select = (await screen.findByLabelText("Cambiar empresa operativa")) as HTMLSelectElement;
  fireEvent.change(select, { target: { value: "company-2" } });
  return select;
};

beforeEach(() => {
  vi.clearAllMocks();
  releaseDiscardApproval();
  useAuthStore.setState({
    user: { userId: "u1", fullName: "Ana", username: "ana", email: "a@t.com", role: "Admin", tenantId: "tenant-1", companyId: "company-1" },
    token: "old-token", isAuthenticated: true, hasHydrated: true, companySessionVersion: 0,
  });
  vi.mocked(authService.listMyCompanies).mockResolvedValue([company("company-1", "Empresa Uno"), company("company-2", "Empresa Dos")] as never);
  vi.mocked(authService.switchCompany).mockResolvedValue({ companyId: "company-2", token: "t" } as never);
  vi.mocked(syncCompanySelection).mockResolvedValue(undefined);
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => cleanup());

describe("CompanySwitcher — cambios sin guardar", () => {
  it("sin cambios pendientes mantiene el comportamiento actual (sin confirmación)", async () => {
    mount(false);
    await changeCompany();
    await waitFor(() => expect(authService.switchCompany).toHaveBeenCalledWith("company-2"));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/dashboard"));
    expect(message.confirm).not.toHaveBeenCalled();
  });

  it("con cambios: confirma una sola vez antes de cambiar y luego cambia y navega sin segunda confirmación", async () => {
    mount(true);
    await changeCompany();
    await waitFor(() => expect(authService.switchCompany).toHaveBeenCalledWith("company-2"));
    expect(vi.mocked(message.confirm).mock.invocationCallOrder[0]).toBeLessThan(
      vi.mocked(authService.switchCompany).mock.invocationCallOrder[0],
    );
    expect(syncCompanySelection).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/dashboard"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });

  it("con cambios: cancelar conserva empresa, sesión y formulario, y no llama al servidor", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    mount(true);
    fireEvent.change(await screen.findByLabelText("campo"), { target: { value: "editado" } });
    const select = await changeCompany();
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));

    expect(authService.switchCompany).not.toHaveBeenCalled();
    expect(syncCompanySelection).not.toHaveBeenCalled();
    expect(useAuthStore.getState().user?.companyId).toBe("company-1");
    expect(useAuthStore.getState().isAuthenticated).toBe(true);
    expect(screen.queryByTestId("path")).toBeNull();
    expect((screen.getByLabelText("campo") as HTMLInputElement).value).toBe("editado");
    await waitFor(() => expect(select.value).toBe("company-1"));
  });

  it("dos cambios seguidos mientras se confirma generan una sola confirmación", async () => {
    let answer: (v: boolean) => void = () => {};
    vi.mocked(message.confirm).mockReturnValue(new Promise<boolean>((res) => { answer = res; }));
    mount(true);
    await changeCompany();
    await changeCompany();
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await act(async () => answer(true));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/dashboard"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });
});
