// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { useAuthStore } from "../store/authStore";
import { useActiveBranchStore } from "../store/activeBranchStore";
import { useSessionStore } from "../store/sessionStore";
import { accessService } from "../modules/auth/api/accessService";
import { authService } from "../modules/auth/api/authService";
import { sessionService } from "../modules/session/api/sessionService";
import { logoutSession } from "../lib/session/logoutSession";
import { releaseDiscardApproval, useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard";
import { message } from "../lib/messages";
import { AppLayout } from "./AppLayout";

/**
 * BP-NAV-GUARD-01 — cierre voluntario de sesión y «volver al Admin Core»: con cambios pendientes se
 * confirma ANTES de `logoutSession()` / `returnToGlobal()`; cancelar conserva sesión y formulario;
 * confirmar ejecuta la acción sin una segunda confirmación del router.
 */

vi.mock("../modules/auth/api/accessService", () => ({ accessService: { getSessionMenu: vi.fn() } }));
vi.mock("../modules/auth/api/authService", () => ({
  authService: { listMyCompanies: vi.fn(), returnToGlobal: vi.fn() },
}));
vi.mock("../modules/session/api/sessionService", () => ({
  sessionService: { getAvailableBranches: vi.fn(), switchBranch: vi.fn(), getContext: vi.fn() },
}));
vi.mock("../lib/session/logoutSession", () => ({ logoutSession: vi.fn() }));
vi.mock("../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));
// Solo se sustituye la cabecera para disparar el `onLogout` real de AppLayout.
vi.mock("./zh/ZHAppTenantHeader", () => ({
  ZHAppTenantHeader: ({ onLogout }: { onLogout?: () => void }) => (
    <button type="button" onClick={onLogout}>
      Cerrar sesión
    </button>
  ),
}));

function FormWithPendingChanges({ unsaved }: { unsaved: boolean }) {
  useUnsavedChangesGuard(unsaved);
  return <input aria-label="campo" defaultValue="inicial" />;
}

function Probe() {
  return <div data-testid="path">{useLocation().pathname}</div>;
}

function mount(unsaved: boolean) {
  const router = createMemoryRouter(
    [
      { element: <AppLayout />, children: [{ path: "/dashboard", element: <FormWithPendingChanges unsaved={unsaved} /> }] },
      { path: "*", element: <Probe /> },
    ],
    { initialEntries: ["/dashboard"] },
  );
  return render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
}

/** El formulario se pinta tras el gate de sucursal (asíncrono): se espera antes de actuar. */
async function mountReady(unsaved: boolean) {
  const view = mount(unsaved);
  if (unsaved) await screen.findByLabelText("campo");
  return view;
}

const USER = {
  userId: "user-1", fullName: "Ana Perez", username: "ana", email: "ana@test.com", role: "Admin",
  tenantId: "tenant-1", companyId: "company-1",
};
const logoutButton = () => screen.findByRole("button", { name: "Cerrar sesión" });

beforeEach(() => {
  vi.clearAllMocks();
  releaseDiscardApproval();
  vi.mocked(accessService.getSessionMenu).mockResolvedValue([]);
  vi.mocked(authService.listMyCompanies).mockResolvedValue([]);
  vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({ loginMode: "AskBranch", defaultBranchId: null, branches: [] });
  // Igual que el real: cierra la sesión local (fullLogout) antes de que la UI navegue a /login.
  vi.mocked(logoutSession).mockImplementation(async () => {
    useAuthStore.getState().logout();
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
  useAuthStore.setState({ user: USER, isAuthenticated: true, hasHydrated: true, companySessionVersion: 0 });
  useActiveBranchStore.setState({ branch: { id: "branch-1", name: "Matriz", isMainBranch: true } });
});
afterEach(() => {
  cleanup();
  useAuthStore.setState({ user: null, isAuthenticated: false, hasHydrated: false, token: null, companySessionVersion: 0 });
  useActiveBranchStore.setState({ branch: null });
  useSessionStore.setState({ isLoading: false, isLoaded: false });
});

describe("AppLayout — cierre voluntario de sesión con cambios sin guardar", () => {
  it("sin cambios pendientes cierra la sesión y navega a /login sin confirmación", async () => {
    await mountReady(false);
    fireEvent.click(await logoutButton());
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    expect(logoutSession).toHaveBeenCalledTimes(1);
    expect(message.confirm).not.toHaveBeenCalled();
  });

  it("con cambios: confirma antes de cerrar sesión y no hay segunda confirmación del router", async () => {
    await mountReady(true);
    fireEvent.click(await logoutButton());
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(vi.mocked(message.confirm).mock.invocationCallOrder[0]).toBeLessThan(
      vi.mocked(logoutSession).mock.invocationCallOrder[0],
    );
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });

  it("con cambios: cancelar conserva la sesión y el formulario y no llama a logoutSession", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    await mountReady(true);
    fireEvent.change(await screen.findByLabelText("campo"), { target: { value: "editado" } });
    fireEvent.click(await logoutButton());
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(logoutSession).not.toHaveBeenCalled();
    expect(useAuthStore.getState().isAuthenticated).toBe(true);
    expect((screen.getByLabelText("campo") as HTMLInputElement).value).toBe("editado");
    expect(screen.queryByTestId("path")).toBeNull();
  });

  it("varios clics mientras se confirma generan una sola confirmación", async () => {
    let answer: (v: boolean) => void = () => {};
    vi.mocked(message.confirm).mockReturnValue(new Promise<boolean>((res) => { answer = res; }));
    await mountReady(true);
    const button = await logoutButton();
    fireEvent.click(button);
    fireEvent.click(button);
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await act(async () => answer(true));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(logoutSession).toHaveBeenCalledTimes(2);
  });
});

describe("AppLayout — «Volver al Admin Core» con cambios sin guardar", () => {
  beforeEach(() => {
    useAuthStore.setState({ user: { ...USER, operatorMode: true } as never });
    vi.mocked(authService.returnToGlobal).mockResolvedValue({ ...USER, token: "t", companyId: null } as never);
  });
  const returnButton = () => screen.findByRole("button", { name: "Volver al Admin Core" });

  it("cancelar no cambia de contexto", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    await mountReady(true);
    fireEvent.click(await returnButton());
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    expect(authService.returnToGlobal).not.toHaveBeenCalled();
    expect(screen.getByLabelText("campo")).toBeTruthy();
  });

  it("confirmar vuelve al Admin Core con una sola confirmación", async () => {
    await mountReady(true);
    fireEvent.click(await returnButton());
    await waitFor(() => expect(authService.returnToGlobal).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/admin-core/dashboard"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });
});
