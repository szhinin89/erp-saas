// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { InventoryAdjustmentReasonsPage } from "./InventoryAdjustmentReasonsPage";
import { inventoryAdjustmentReasonsService } from "../api/inventoryAdjustmentReasonsService";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import type { InventoryAdjustmentReasonDto } from "../types";

vi.mock("../api/inventoryAdjustmentReasonsService", () => ({
  inventoryAdjustmentReasonsService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    toggle: vi.fn(),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const MERMA: InventoryAdjustmentReasonDto = {
  id: "rsn-1",
  code: "MERMA",
  name: "Merma",
  allowedMovementType: "Egreso",
  requiresNotes: true,
  isActive: true,
  sortOrder: 1,
};

const SOBRA: InventoryAdjustmentReasonDto = {
  id: "rsn-2",
  code: "SOBRA",
  name: "Sobrante",
  allowedMovementType: "Ingreso",
  requiresNotes: false,
  isActive: false,
  sortOrder: 2,
};

function grant(granted: string[] | "all" = "all") {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (key: string) => granted === "all" || granted.includes(key),
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <InventoryAdjustmentReasonsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  grant("all");
  vi.mocked(inventoryAdjustmentReasonsService.list).mockResolvedValue([
    MERMA,
    SOBRA,
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("InventoryAdjustmentReasonsPage", () => {
  it("lista los motivos activos e inactivos del catálogo", async () => {
    renderPage();

    expect(await screen.findByText("MERMA")).toBeTruthy();
    expect(screen.getByText("Merma")).toBeTruthy();
    expect(screen.getByText("SOBRA")).toBeTruthy();
    expect(screen.getByText("Activo")).toBeTruthy();
    expect(screen.getByText("Inactivo")).toBeTruthy();
    // Se piden también los inactivos: es la pantalla de administración del catálogo.
    expect(inventoryAdjustmentReasonsService.list).toHaveBeenCalledWith(true);
  });

  it("oculta las acciones de gestión sin el permiso manage", async () => {
    grant(["inventory.adjustment-reasons.view"]);
    renderPage();

    await screen.findByText("MERMA");
    expect(screen.queryByText("Nuevo motivo")).toBeNull();
    expect(screen.queryByText("Acciones")).toBeNull();
  });

  it("crea un motivo nuevo con el payload esperado (sin companyId)", async () => {
    vi.mocked(inventoryAdjustmentReasonsService.create).mockResolvedValue({
      ...MERMA,
      id: "rsn-new",
      code: "ROTURA",
      name: "Rotura",
    });
    renderPage();
    await screen.findByText("MERMA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.change(await screen.findByLabelText("Código"), {
      target: { value: "ROTURA" },
    });
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Rotura" },
    });
    fireEvent.change(screen.getByLabelText("Movimiento permitido"), {
      target: { value: "Egreso" },
    });
    fireEvent.click(screen.getByText("Guardar motivo"));

    await waitFor(() =>
      expect(inventoryAdjustmentReasonsService.create).toHaveBeenCalledWith({
        code: "ROTURA",
        name: "Rotura",
        allowedMovementType: "Egreso",
        requiresNotes: false,
        sortOrder: 0,
      }),
    );
  });

  it("exige código y nombre antes de llamar al backend", async () => {
    renderPage();
    await screen.findByText("MERMA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.click(await screen.findByText("Guardar motivo"));

    expect(await screen.findByText("El código es obligatorio.")).toBeTruthy();
    expect(screen.getByText("El nombre es obligatorio.")).toBeTruthy();
    expect(inventoryAdjustmentReasonsService.create).not.toHaveBeenCalled();
  });

  it("al editar deja el código inmutable y llama a update sin él", async () => {
    vi.mocked(inventoryAdjustmentReasonsService.update).mockResolvedValue({
      ...MERMA,
      name: "Merma corregida",
    });
    renderPage();
    await screen.findByText("MERMA");

    fireEvent.click(screen.getByLabelText("Editar Merma"));
    const code = (await screen.findByLabelText("Código")) as HTMLInputElement;
    expect(code.value).toBe("MERMA");
    expect(code.disabled).toBe(true);

    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Merma corregida" },
    });
    fireEvent.click(screen.getByText("Actualizar motivo"));

    await waitFor(() =>
      expect(inventoryAdjustmentReasonsService.update).toHaveBeenCalledWith(
        "rsn-1",
        {
          id: "rsn-1",
          name: "Merma corregida",
          allowedMovementType: "Egreso",
          requiresNotes: true,
          sortOrder: 1,
        },
      ),
    );
  });

  it("activa/desactiva por el endpoint dedicado tras confirmar", async () => {
    vi.mocked(inventoryAdjustmentReasonsService.toggle).mockResolvedValue({
      ...MERMA,
      isActive: false,
    });
    renderPage();
    await screen.findByText("MERMA");

    fireEvent.click(screen.getByLabelText("Deshabilitar Merma"));
    expect(inventoryAdjustmentReasonsService.toggle).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByText("Deshabilitar"));

    await waitFor(() =>
      expect(inventoryAdjustmentReasonsService.toggle).toHaveBeenCalledWith(
        "rsn-1",
        false,
      ),
    );
  });

  it("muestra el error de validación del backend en el campo (código duplicado)", async () => {
    vi.mocked(inventoryAdjustmentReasonsService.create).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: { code: ["Ya existe un motivo con ese código."] } } },
      },
    });
    renderPage();
    await screen.findByText("MERMA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.change(await screen.findByLabelText("Código"), {
      target: { value: "MERMA" },
    });
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Merma duplicada" },
    });
    fireEvent.click(screen.getByText("Guardar motivo"));

    expect(
      await screen.findByText("Ya existe un motivo con ese código."),
    ).toBeTruthy();
  });
});
