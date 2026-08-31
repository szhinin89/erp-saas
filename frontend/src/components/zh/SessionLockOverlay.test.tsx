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
import { logoutSession } from "../../lib/session/logoutSession";

vi.mock("../../lib/session/logoutSession", () => ({
  logoutSession: vi.fn().mockResolvedValue(undefined),
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
    navigateMock.mockClear();
    vi.mocked(logoutSession).mockClear();
  });

  afterEach(() => {
    cleanup();
    useSessionIdleStore.setState({ isLocked: false });
  });

  it("no renderiza nada cuando la sesión no está bloqueada", () => {
    renderOverlay();

    expect(screen.queryByText("Sesión pausada por inactividad")).toBeNull();
  });

  it("muestra el overlay con título, mensaje y botón cuando isLocked=true", () => {
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    expect(screen.getByText("Sesión pausada por inactividad")).toBeTruthy();
    expect(
      screen.getByText(/vuelve a iniciar sesión/i),
    ).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Ir al inicio de sesión" }),
    ).toBeTruthy();
  });

  it("el botón ejecuta logoutSession() y redirige a /login", async () => {
    useSessionIdleStore.setState({ isLocked: true });
    renderOverlay();

    fireEvent.click(
      screen.getByRole("button", { name: "Ir al inicio de sesión" }),
    );

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
});
