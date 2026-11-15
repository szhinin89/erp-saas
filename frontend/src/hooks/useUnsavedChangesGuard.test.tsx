// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { useEffect } from "react";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/i18n";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { fullLogout } from "../lib/session/fullLogout";
import { message } from "../lib/messages";
import { useAuthStore } from "../store/authStore";
import {
  releaseDiscardApproval,
  useDiscardConfirmation,
  useUnsavedChangesGuard,
} from "./useUnsavedChangesGuard";

/**
 * BP-NAV-GUARD-01 — mecanismo único: acciones voluntarias (consultan antes de actuar, una sola
 * confirmación, sin segunda confirmación del router) y eventos automáticos de seguridad (nunca bloquean).
 */

vi.mock("../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const USER = {
  userId: "u1",
  email: "u@example.com",
  tenantId: "11111111-1111-1111-1111-111111111111",
  companyId: "22222222-2222-2222-2222-222222222222",
  role: "Admin",
} as never;

const voluntaryRef: { current: () => Promise<boolean> } = { current: async () => true };
const voluntary = () => voluntaryRef.current();

function Page({ unsaved }: { unsaved: boolean }) {
  useUnsavedChangesGuard(unsaved);
  const confirmDiscard = useDiscardConfirmation();
  useEffect(() => {
    voluntaryRef.current = confirmDiscard;
  }, [confirmDiscard]);
  return <input aria-label="campo" defaultValue="inicial" />;
}

function Probe() {
  return <div data-testid="path">{useLocation().pathname}</div>;
}

function mount(unsaved = true) {
  const router = createMemoryRouter(
    [
      {
        // Guard real de la app: al perder la sesión navega a /login por el router.
        element: <ProtectedRoute />,
        children: [{ path: "/page", element: <Page unsaved={unsaved} /> }],
      },
      { path: "*", element: <Probe /> },
    ],
    { initialEntries: ["/page"] },
  );
  const view = render(
    <I18nProvider>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
  return { router, ...view };
}

const go = (router: ReturnType<typeof mount>["router"], to: string) =>
  act(async () => {
    await router.navigate(to);
  });
const onPage = () => screen.queryByLabelText("campo") !== null;

beforeEach(() => {
  vi.clearAllMocks();
  releaseDiscardApproval();
  useAuthStore.setState({ hasHydrated: true, isAuthenticated: true, user: USER, companySessionVersion: 0 });
  vi.mocked(message.confirm).mockResolvedValue(true);
});
afterEach(() => cleanup());

describe("acciones voluntarias (cambio de empresa, cerrar sesión)", () => {
  it("sin cambios pendientes no se pregunta y la acción procede", async () => {
    mount(false);
    await screen.findByLabelText("campo");
    expect(await voluntary()).toBe(true);
    expect(message.confirm).not.toHaveBeenCalled();
  });

  it("con cambios: confirmar autoriza la acción y la navegación posterior no vuelve a preguntar", async () => {
    const { router } = mount();
    await screen.findByLabelText("campo");
    expect(await voluntary()).toBe(true);
    expect(message.confirm).toHaveBeenCalledTimes(1);

    await go(router, "/dashboard");
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/dashboard"));
    expect(message.confirm).toHaveBeenCalledTimes(1);
  });

  it("con cambios: cancelar no autoriza nada y el formulario se conserva", async () => {
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    const { router } = mount();
    await screen.findByLabelText("campo");
    expect(await voluntary()).toBe(false);
    expect(onPage()).toBe(true);

    // La cancelación no dejó aprobación: una navegación posterior sigue protegida.
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(2));
    expect(onPage()).toBe(true);
  });

  it("invocaciones simultáneas (clics repetidos o router a la vez) comparten una sola confirmación", async () => {
    let answer: (v: boolean) => void = () => {};
    vi.mocked(message.confirm).mockReturnValue(new Promise<boolean>((res) => { answer = res; }));
    const { router } = mount();
    await screen.findByLabelText("campo");
    const first = voluntary();
    const second = voluntary();
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    await act(async () => answer(true));
    expect(await first).toBe(true);
    expect(await second).toBe(true);
    expect(message.confirm).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/otra"));
  });

  it("liberar la aprobación restablece la protección de la navegación", async () => {
    const { router } = mount();
    await screen.findByLabelText("campo");
    await voluntary();
    releaseDiscardApproval();
    vi.mocked(message.confirm).mockResolvedValueOnce(false);
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(2));
    expect(onPage()).toBe(true);
  });
});

describe("eventos automáticos de seguridad: nunca se bloquean", () => {
  it("la caducidad/revocación de la sesión navega a /login por el guard sin confirmación", async () => {
    mount();
    await screen.findByLabelText("campo");
    act(() => fullLogout({ broadcast: false }));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    expect(message.confirm).not.toHaveBeenCalled();
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });

  it("un cierre automático no restaura la sesión ni deja aprobaciones colgadas", async () => {
    const { router } = mount();
    await screen.findByLabelText("campo");
    act(() => fullLogout({ broadcast: false }));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    expect(useAuthStore.getState().user).toBeNull();
    await go(router, "/otra");
    expect(screen.getByTestId("path").textContent).toBe("/otra");
    expect(message.confirm).not.toHaveBeenCalled();
  });

  it("si la sesión termina mientras hay una confirmación abierta, la navegación a /login procede", async () => {
    let answer: (v: boolean) => void = () => {};
    vi.mocked(message.confirm).mockReturnValue(new Promise<boolean>((res) => { answer = res; }));
    const { router } = mount();
    await screen.findByLabelText("campo");
    await go(router, "/otra");
    await waitFor(() => expect(message.confirm).toHaveBeenCalledTimes(1));
    act(() => fullLogout({ broadcast: false }));
    await waitFor(() => expect(screen.getByTestId("path").textContent).toBe("/login"));
    // La confirmación abierta ya no tiene efecto: responderla no revive la navegación ni la sesión.
    await act(async () => answer(true));
    expect(screen.getByTestId("path").textContent).toBe("/login");
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });

  it("beforeunload: protege con sesión activa y cambios; no interviene sin sesión (recarga tras 401) ni con descarte aprobado", async () => {
    mount();
    await screen.findByLabelText("campo");
    const fire = () => {
      const e = new Event("beforeunload", { cancelable: true });
      window.dispatchEvent(e);
      return e.defaultPrevented;
    };
    expect(fire()).toBe(true);

    await voluntary();
    expect(fire()).toBe(false);
    releaseDiscardApproval();
    expect(fire()).toBe(true);

    act(() => fullLogout({ broadcast: false }));
    expect(fire()).toBe(false);
  });
});
