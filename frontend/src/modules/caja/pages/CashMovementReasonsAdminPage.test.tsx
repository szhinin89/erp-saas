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
import { I18nProvider } from "../../../i18n/i18n";
import { CashMovementReasonsAdminPage } from "./CashMovementReasonsAdminPage";
import { cajaService, type CashMovementReasonDto } from "../api/cajaService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

vi.mock("../api/cajaService", () => ({
  cajaService: {
    listCashMovementReasons: vi.fn(),
    createCashMovementReason: vi.fn(),
    updateCashMovementReason: vi.fn(),
    toggleCashMovementReason: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const CAMBIO: CashMovementReasonDto = {
  id: "reason-1",
  code: "CAMBIO_CAJA",
  name: "Cambio de caja chica",
  movementType: "ManualIncome",
  isActive: true,
  sortOrder: 1,
};

const DEPOSITO: CashMovementReasonDto = {
  id: "reason-2",
  code: "DEPOSITO_BANCARIO",
  name: "Depósito bancario",
  movementType: "Withdrawal",
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
        <CashMovementReasonsAdminPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  grant("all");
  vi.mocked(cajaService.listCashMovementReasons).mockResolvedValue([
    CAMBIO,
    DEPOSITO,
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CashMovementReasonsAdminPage", () => {
  it("lista los motivos activos e inactivos del catálogo, incluyendo inactivos", async () => {
    renderPage();

    expect(await screen.findByText("CAMBIO_CAJA")).toBeTruthy();
    expect(screen.getByText("Cambio de caja chica")).toBeTruthy();
    expect(screen.getByText("DEPOSITO_BANCARIO")).toBeTruthy();
    expect(screen.getByText("Activo")).toBeTruthy();
    expect(screen.getByText("Inactivo")).toBeTruthy();
    expect(cajaService.listCashMovementReasons).toHaveBeenCalledWith(true);
  });

  it("oculta las acciones de gestión sin el permiso manage", async () => {
    grant(["caja.view"]);
    renderPage();

    await screen.findByText("CAMBIO_CAJA");
    expect(screen.queryByText("Nuevo motivo")).toBeNull();
    expect(screen.queryByText("Acciones")).toBeNull();
  });

  it("crea un motivo nuevo con el payload esperado (sin companyId)", async () => {
    vi.mocked(cajaService.createCashMovementReason).mockResolvedValue({
      ...CAMBIO,
      id: "reason-new",
      code: "OTRO_INGRESO",
      name: "Otro ingreso",
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.change(await screen.findByLabelText("Código"), {
      target: { value: "OTRO_INGRESO" },
    });
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Otro ingreso" },
    });
    fireEvent.change(screen.getByLabelText("Tipo"), {
      target: { value: "ManualIncome" },
    });
    fireEvent.click(screen.getByText("Guardar motivo"));

    await waitFor(() =>
      expect(cajaService.createCashMovementReason).toHaveBeenCalledWith({
        code: "OTRO_INGRESO",
        name: "Otro ingreso",
        movementType: "ManualIncome",
        sortOrder: 0,
      }),
    );
  });

  it("exige código, nombre y tipo antes de llamar al backend", async () => {
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.click(await screen.findByText("Guardar motivo"));

    expect(await screen.findByText("El código es obligatorio.")).toBeTruthy();
    expect(screen.getByText("El nombre es obligatorio.")).toBeTruthy();
    expect(screen.getByText("Seleccione el tipo de movimiento.")).toBeTruthy();
    expect(cajaService.createCashMovementReason).not.toHaveBeenCalled();
  });

  it("crear permite seleccionar el tipo", async () => {
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    const select = (await screen.findByLabelText("Tipo")) as HTMLSelectElement;

    expect(select.disabled).toBe(false);
    fireEvent.change(select, { target: { value: "Withdrawal" } });
    expect(select.value).toBe("Withdrawal");
  });

  it("al editar deja el código inmutable y llama a update sin él", async () => {
    vi.mocked(cajaService.updateCashMovementReason).mockResolvedValue({
      ...CAMBIO,
      name: "Cambio de caja corregido",
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Editar Cambio de caja chica"));
    const code = (await screen.findByLabelText("Código")) as HTMLInputElement;
    expect(code.value).toBe("CAMBIO_CAJA");
    expect(code.disabled).toBe(true);

    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Cambio de caja corregido" },
    });
    fireEvent.click(screen.getByText("Actualizar motivo"));

    await waitFor(() =>
      expect(cajaService.updateCashMovementReason).toHaveBeenCalledWith(
        "reason-1",
        {
          id: "reason-1",
          name: "Cambio de caja corregido",
          sortOrder: 1,
        },
      ),
    );
    // TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A — el payload de update nunca incluye
    // movementType: no hay forma de que la pantalla reclasifique un motivo existente.
    const [, payload] = vi.mocked(cajaService.updateCashMovementReason).mock.calls[0];
    expect(payload).not.toHaveProperty("movementType");
  });

  it("al editar el Tipo se muestra deshabilitado (solo lectura)", async () => {
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Editar Cambio de caja chica"));
    const select = (await screen.findByLabelText("Tipo")) as HTMLSelectElement;

    expect(select.value).toBe("ManualIncome");
    expect(select.disabled).toBe(true);
  });

  it("al editar, cambiar Nombre y Orden sí se envían al backend", async () => {
    vi.mocked(cajaService.updateCashMovementReason).mockResolvedValue({
      ...CAMBIO,
      name: "Nombre editado",
      sortOrder: 7,
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Editar Cambio de caja chica"));
    fireEvent.change(await screen.findByLabelText("Nombre"), {
      target: { value: "Nombre editado" },
    });
    fireEvent.change(screen.getByLabelText("Orden"), {
      target: { value: "7" },
    });
    fireEvent.click(screen.getByText("Actualizar motivo"));

    await waitFor(() =>
      expect(cajaService.updateCashMovementReason).toHaveBeenCalledWith("reason-1", {
        id: "reason-1",
        name: "Nombre editado",
        sortOrder: 7,
      }),
    );
  });

  it("registros existentes no cambian de clasificación: el motivo conserva su MovementType original tras editar", async () => {
    // El backend nunca recibe MovementType en el update — este test documenta esa garantía desde
    // el frontend: aunque el <select> muestre "ManualIncome" (deshabilitado), la ausencia de
    // movementType en el payload es lo que impide reclasificar el registro.
    vi.mocked(cajaService.updateCashMovementReason).mockResolvedValue({
      ...CAMBIO,
      name: "Cambio renombrado",
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Editar Cambio de caja chica"));
    fireEvent.change(await screen.findByLabelText("Nombre"), {
      target: { value: "Cambio renombrado" },
    });
    fireEvent.click(screen.getByText("Actualizar motivo"));

    await waitFor(() => expect(cajaService.updateCashMovementReason).toHaveBeenCalled());
    const [, payload] = vi.mocked(cajaService.updateCashMovementReason).mock.calls[0];
    expect(payload).toEqual({
      id: "reason-1",
      name: "Cambio renombrado",
      sortOrder: 1,
    });
  });

  it("activa/desactiva por el endpoint dedicado tras confirmar", async () => {
    vi.mocked(cajaService.toggleCashMovementReason).mockResolvedValue({
      ...CAMBIO,
      isActive: false,
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Desactivar Cambio de caja chica"));
    expect(cajaService.toggleCashMovementReason).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByText("Desactivar"));

    await waitFor(() =>
      expect(cajaService.toggleCashMovementReason).toHaveBeenCalledWith("reason-1"),
    );
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Motivo desactivado."),
    );
  });

  it("si falla el toggle, muestra el mensaje real y no éxito", async () => {
    vi.mocked(cajaService.toggleCashMovementReason).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El motivo está en uso en un movimiento activo." } },
      },
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByLabelText("Desactivar Cambio de caja chica"));
    fireEvent.click(await screen.findByText("Desactivar"));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith(
        "El motivo está en uso en un movimiento activo.",
      ),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("muestra el error de validación del backend en el campo (código duplicado)", async () => {
    vi.mocked(cajaService.createCashMovementReason).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: { code: ["Ya existe un motivo con ese código."] } } },
      },
    });
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    fireEvent.change(await screen.findByLabelText("Código"), {
      target: { value: "CAMBIO_CAJA" },
    });
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Duplicado" },
    });
    fireEvent.change(screen.getByLabelText("Tipo"), {
      target: { value: "ManualIncome" },
    });
    fireEvent.click(screen.getByText("Guardar motivo"));

    expect(
      await screen.findByText("Ya existe un motivo con ese código."),
    ).toBeTruthy();
  });

  it("el tipo del formulario solo ofrece los tipos manuales (ManualIncome/ManualExpense/Withdrawal)", async () => {
    renderPage();
    await screen.findByText("CAMBIO_CAJA");

    fireEvent.click(screen.getByText("Nuevo motivo"));
    const select = (await screen.findByLabelText("Tipo")) as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);

    expect(values).toContain("ManualIncome");
    expect(values).toContain("ManualExpense");
    expect(values).toContain("Withdrawal");
    expect(values).not.toContain("Opening");
    expect(values).not.toContain("SaleIncome");
    expect(values).not.toContain("SaleRefund");
  });
});
