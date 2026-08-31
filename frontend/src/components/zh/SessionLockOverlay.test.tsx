// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  fireEvent,
  waitFor,
  cleanup,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SessionLockOverlay } from "./SessionLockOverlay";
import { useSessionIdleStore } from "../../store/sessionIdleStore";
import { useAuthStore } from "../../store/authStore";
import { logoutSession } from "../../lib/session/logoutSession";
import { reauthenticate } from "../../lib/session/reauthenticate";
import { broadcastIdleUnlock } from "../../lib/session/idleBroadcast";

vi.mock("../../lib/session/logoutSession", () => ({
  logoutSession: vi.fn().mockResolvedValue(undefined),
}));

vi.mock("../../lib/session/reauthenticate", () => ({
  reauthenticate: vi.fn(),
  isSessionInvalidError: (message: string) =>
    message.toLowerCase().includes("sesión expirada") ||
    message.toLowerCase().includes("inicia sesión nuevamente"),
}));

vi.mock("../../lib/session/idleBroadcast", () => ({
  broadcastIdleUnlock: vi.fn(),
}));

const navigateMock = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>(
      "react-router-dom",
    );
  return { ...actual, useNavigate: () => navigateMock };
});

function renderOverlay() {
  return render(
    <MemoryRouter>
      <SessionLockOverlay />
    </MemoryRouter>,
  );
}

describe("SessionLockOverlay", () => {
  beforeEach(() => {
    useSessionIdleStore.setState({ isLocked: false });
    useAuthStore.setState({
      user: {
        userId: "u1",
        username: "ana",
        email: "ana@test.com",
        fullName: "Ana Pérez",
        role: "Admin",
        tenantId: "t1",
        companyId: "c1",
      },
    });
    navigateMock.mockClear();
    vi.mocked(logoutSession).mockClear();
    vi.mocked(reauthenticate).mockReset();
    vi.mocked(broadcastIdleUnlock).mockClear();
  });

  afterEach(() => {
    cleanup();
    useSessionIdleStore.setState({ isLocked: false });
    useAuthStore.setState({ user: null });
  });

  it("no renderiza nada cuando la sesión no está bloqueada", () => {
    renderOverlay();

    expect(screen.queryByText("Sesión pausada por inactividad")).toBeNull();
  });

  it("muestra usuario actual (solo lectura) y campo de contraseña cuando isLocked=true", () => {
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    expect(screen.getByText("Sesión pausada por inactividad")).toBeTruthy();
    expect(screen.getByDisplayValue("Ana Pérez")).toHaveProperty(
      "readOnly",
      true,
    );
    expect(screen.getByLabelText("Contraseña")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Continuar" }),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Salir" })).toBeTruthy();
  });

  it("con contraseña correcta: llama a reauthenticate, desbloquea, propaga a otras pestañas y no navega", async () => {
    vi.mocked(reauthenticate).mockResolvedValueOnce(undefined);
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.change(screen.getByLabelText("Contraseña"), {
      target: { value: "Sup3rSecret!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await waitFor(() =>
      expect(useSessionIdleStore.getState().isLocked).toBe(false),
    );

    expect(reauthenticate).toHaveBeenCalledWith("Sup3rSecret!");
    expect(broadcastIdleUnlock).toHaveBeenCalledTimes(1);
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it("con contraseña incorrecta: muestra el error, mantiene el overlay y no navega", async () => {
    vi.mocked(reauthenticate).mockRejectedValueOnce(
      new Error("Contraseña incorrecta."),
    );
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.change(screen.getByLabelText("Contraseña"), {
      target: { value: "mala-clave" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    expect(await screen.findByText("Contraseña incorrecta.")).toBeTruthy();

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
    expect(navigateMock).not.toHaveBeenCalled();
    // El overlay sigue mostrando el campo de contraseña (vacío) para reintentar.
    // (No se usa getByLabelText aquí: el hint de error ahora vive dentro del mismo
    // <label> que el input, lo que cambia su nombre accesible calculado.)
    expect(document.querySelector('input[type="password"]')).not.toBeNull();
  });

  it("no deja la contraseña en el campo tras un intento fallido", async () => {
    vi.mocked(reauthenticate).mockRejectedValueOnce(
      new Error("Contraseña incorrecta."),
    );
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    const input = screen.getByLabelText("Contraseña") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "mala-clave" } });
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await waitFor(() => expect(input.value).toBe(""));
  });

  it("con sesión inválida (refresh vencido): oculta el campo de contraseña y ofrece ir al login", async () => {
    vi.mocked(reauthenticate).mockRejectedValueOnce(
      new Error("Sesión expirada. Inicia sesión nuevamente."),
    );
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.change(screen.getByLabelText("Contraseña"), {
      target: { value: "algo" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    expect(
      await screen.findByRole("button", { name: "Ir al inicio de sesión" }),
    ).toBeTruthy();
    expect(screen.queryByLabelText("Contraseña")).toBeNull();
  });

  it("el botón Salir ejecuta logoutSession() y redirige a /login", async () => {
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.click(screen.getByRole("button", { name: "Salir" }));

    expect(logoutSession).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith("/login"));
  });

  it("hacer click en el backdrop no cierra el overlay (no es un diálogo cancelable)", () => {
    useSessionIdleStore.setState({ isLocked: true });
    const { container } = renderOverlay();

    const overlay = container.querySelector(".zh-modal-overlay");
    expect(overlay).not.toBeNull();
    fireEvent.click(overlay!);

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("no persiste ni la contraseña ni tokens en localStorage/sessionStorage", async () => {
    vi.mocked(reauthenticate).mockResolvedValueOnce(undefined);
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.change(screen.getByLabelText("Contraseña"), {
      target: { value: "Sup3rSecret!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await waitFor(() =>
      expect(useSessionIdleStore.getState().isLocked).toBe(false),
    );

    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i)!;
      expect(localStorage.getItem(key)).not.toContain("Sup3rSecret!");
    }
    for (let i = 0; i < sessionStorage.length; i += 1) {
      const key = sessionStorage.key(i)!;
      expect(sessionStorage.getItem(key)).not.toContain("Sup3rSecret!");
    }
  });
});
