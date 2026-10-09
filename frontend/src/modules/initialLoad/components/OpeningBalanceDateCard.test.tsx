// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, configure, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { OpeningBalanceDateCard } from "./OpeningBalanceDateCard";
import { initialLoadService } from "../api/initialLoadService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type { OpeningBalanceDateDto } from "../types/importBatch.types";

configure({ asyncUtilTimeout: 5000 });

vi.mock("../api/initialLoadService", () => ({
  initialLoadService: {
    getOpeningBalanceDate: vi.fn(),
    setOpeningBalanceDate: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn() },
}));

const free: OpeningBalanceDateDto = {
  openingBalanceDate: null,
  hasRealOperations: false,
  confirmedOpeningDates: [],
  isLocked: false,
  lockReason: null,
};

function mockPermission(allowed: boolean) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => allowed,
    has: () => allowed,
    isAdminRole: false,
  });
}

function dateInput(): HTMLInputElement {
  return screen.getByLabelText(/Fecha de apertura de saldos/i, { selector: "input" });
}

describe("OpeningBalanceDateCard (IL-5A)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPermission(true);
  });

  afterEach(() => cleanup());

  it("define la fecha cuando la empresa aún no la tiene", async () => {
    vi.mocked(initialLoadService.getOpeningBalanceDate).mockResolvedValue(free);
    vi.mocked(initialLoadService.setOpeningBalanceDate).mockResolvedValue({
      ...free,
      openingBalanceDate: "2026-09-30",
    });
    render(<OpeningBalanceDateCard />);

    await screen.findByText("Sin definir");
    fireEvent.change(dateInput(), { target: { value: "2026-09-30" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar fecha" }));

    await waitFor(() =>
      expect(initialLoadService.setOpeningBalanceDate).toHaveBeenCalledWith("2026-09-30"),
    );
    expect(message.success).toHaveBeenCalled();
  });

  it("bloqueada muestra el motivo y no permite editar", async () => {
    vi.mocked(initialLoadService.getOpeningBalanceDate).mockResolvedValue({
      openingBalanceDate: "2026-09-30",
      hasRealOperations: true,
      confirmedOpeningDates: ["2026-09-30"],
      isLocked: true,
      lockReason: "La fecha de apertura (2026-09-30) es definitiva: la empresa ya registra operaciones reales.",
    });
    render(<OpeningBalanceDateCard />);

    await screen.findByText(/es definitiva/);
    expect(dateInput().disabled).toBe(true);
    expect(screen.getByRole("button", { name: "Guardar fecha" })).toHaveProperty("disabled", true);
  });

  it("con apertura confirmada avisa que solo se admite esa fecha", async () => {
    vi.mocked(initialLoadService.getOpeningBalanceDate).mockResolvedValue({
      ...free,
      hasRealOperations: true,
      confirmedOpeningDates: ["2026-09-30"],
    });
    render(<OpeningBalanceDateCard />);

    await screen.findByText(/solo se admite esa fecha/);
    expect(dateInput().disabled).toBe(false);
  });

  it("muestra el rechazo del backend", async () => {
    vi.mocked(initialLoadService.getOpeningBalanceDate).mockResolvedValue(free);
    vi.mocked(initialLoadService.setOpeningBalanceDate).mockRejectedValue(new Error("rechazado"));
    render(<OpeningBalanceDateCard />);

    await screen.findByText("Sin definir");
    fireEvent.change(dateInput(), { target: { value: "2026-08-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar fecha" }));

    await waitFor(() => expect(message.error).toHaveBeenCalled());
  });

  it("sin permiso no permite editar", async () => {
    mockPermission(false);
    vi.mocked(initialLoadService.getOpeningBalanceDate).mockResolvedValue(free);
    render(<OpeningBalanceDateCard />);

    await screen.findByText(/No tiene permiso/);
    expect(dateInput().disabled).toBe(true);
  });
});
