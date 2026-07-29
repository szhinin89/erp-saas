// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import { membershipService } from "../api/membershipService";
import { AssignTemporaryPasswordModal } from "./AssignTemporaryPasswordModal";

vi.mock("../api/membershipService", () => ({
  membershipService: {
    assignTemporaryPassword: vi.fn(),
  },
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

function renderModal(
  props: Partial<{ open: boolean; username: string; onClose: () => void }> = {},
) {
  const onClose = props.onClose ?? vi.fn();
  const utils = render(
    <I18nProvider>
      <AssignTemporaryPasswordModal
        open={props.open ?? true}
        username={props.username ?? "ana.perez"}
        onClose={onClose}
      />
    </I18nProvider>,
  );
  return { ...utils, onClose };
}

function fieldByName(container: HTMLElement, name: string): HTMLInputElement {
  const el = container.querySelector(`input[name="${name}"]`);
  if (!el) throw new Error(`input[name="${name}"] no encontrado`);
  return el as HTMLInputElement;
}

async function fillAndSubmit(
  container: HTMLElement,
  values: { password: string; confirm: string },
) {
  fireEvent.change(fieldByName(container, "temporaryPassword"), {
    target: { value: values.password },
  });
  fireEvent.change(fieldByName(container, "confirmPassword"), {
    target: { value: values.confirm },
  });
  fireEvent.click(screen.getByRole("button", { name: "Asignar contraseña" }));
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("AssignTemporaryPasswordModal — render", () => {
  it("no renderiza contenido cuando open=false", () => {
    renderModal({ open: false });

    expect(screen.queryByText("Asignar contraseña temporal")).toBeNull();
  });

  it("muestra título, campos, aviso informativo y botones cuando open=true", () => {
    const { container } = renderModal();

    expect(screen.getByText("Asignar contraseña temporal")).toBeTruthy();
    expect(fieldByName(container, "temporaryPassword")).toBeTruthy();
    expect(fieldByName(container, "confirmPassword")).toBeTruthy();
    expect(screen.getByText(/La contraseña será temporal/)).toBeTruthy();
    expect(
      screen.getByText(
        /Todas las sesiones activas serán cerradas automáticamente/,
      ),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Asignar contraseña" }),
    ).toBeTruthy();
  });

  it("nunca renderiza un <form> propio — este modal se monta dentro del <form> único de UserConfigPage, y un <form> anidado hace que el navegador recargue la página en vez de interceptar el submit", () => {
    const { container } = renderModal();

    expect(container.querySelector("form")).toBeNull();
    expect(
      (
        screen.getByRole("button", {
          name: "Asignar contraseña",
        }) as HTMLButtonElement
      ).type,
    ).toBe("button");
  });
});

describe("AssignTemporaryPasswordModal — validación", () => {
  it("contraseña que no cumple complejidad muestra error y no llama al servicio", async () => {
    const { container } = renderModal();

    await fillAndSubmit(container, { password: "short", confirm: "short" });

    await waitFor(() =>
      expect(
        screen.getByText("La contraseña debe tener al menos 8 caracteres."),
      ).toBeTruthy(),
    );
    expect(membershipService.assignTemporaryPassword).not.toHaveBeenCalled();
  });

  it("confirmación distinta a la contraseña muestra error de coincidencia y no llama al servicio", async () => {
    const { container } = renderModal();

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Otra0ral!",
    });

    await waitFor(() =>
      expect(screen.getByText("Las contraseñas no coinciden.")).toBeTruthy(),
    );
    expect(membershipService.assignTemporaryPassword).not.toHaveBeenCalled();
  });
});

describe("AssignTemporaryPasswordModal — envío", () => {
  it("éxito: llama al servicio con username y password, muestra toast, cierra y limpia el formulario", async () => {
    vi.mocked(membershipService.assignTemporaryPassword).mockResolvedValue(
      "Contraseña temporal asignada correctamente.",
    );
    const onClose = vi.fn();
    const { container } = renderModal({ username: "ana.perez", onClose });

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Temp0ral!",
    });

    await waitFor(() => {
      expect(membershipService.assignTemporaryPassword).toHaveBeenCalledWith(
        "ana.perez",
        "Temp0ral!",
      );
      expect(message.success).toHaveBeenCalledWith(
        "Contraseña temporal asignada correctamente.",
      );
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    expect(fieldByName(container, "temporaryPassword").value).toBe("");
    expect(fieldByName(container, "confirmPassword").value).toBe("");
  });

  it("loading: deshabilita campos y botón mientras la petición está en curso", async () => {
    let resolvePromise!: (value: string) => void;
    vi.mocked(membershipService.assignTemporaryPassword).mockReturnValue(
      new Promise((resolve) => {
        resolvePromise = resolve;
      }),
    );
    const { container } = renderModal();

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Temp0ral!",
    });

    await waitFor(() => {
      expect(fieldByName(container, "temporaryPassword").disabled).toBe(true);
      expect(fieldByName(container, "confirmPassword").disabled).toBe(true);
      expect(
        (
          screen.getByRole("button", {
            name: "Guardando...",
          }) as HTMLButtonElement
        ).disabled,
      ).toBe(true);
    });

    resolvePromise("Contraseña temporal asignada correctamente.");
    await waitFor(() =>
      expect(membershipService.assignTemporaryPassword).toHaveBeenCalledTimes(
        1,
      ),
    );
  });

  it("bloquea doble envío: dos clics rápidos solo invocan el servicio una vez", async () => {
    let resolvePromise!: (value: string) => void;
    vi.mocked(membershipService.assignTemporaryPassword).mockReturnValue(
      new Promise((resolve) => {
        resolvePromise = resolve;
      }),
    );
    const { container } = renderModal();

    fireEvent.change(fieldByName(container, "temporaryPassword"), {
      target: { value: "Temp0ral!" },
    });
    fireEvent.change(fieldByName(container, "confirmPassword"), {
      target: { value: "Temp0ral!" },
    });
    const submitButton = () =>
      screen.getByRole("button", { name: /Asignar contraseña|Guardando/ });
    fireEvent.click(submitButton());
    fireEvent.click(submitButton());
    fireEvent.click(submitButton());

    await waitFor(() =>
      expect(submitButton()).toHaveProperty("disabled", true),
    );
    expect(membershipService.assignTemporaryPassword).toHaveBeenCalledTimes(1);

    resolvePromise("Contraseña temporal asignada correctamente.");
  });

  it("cerrar (Cancelar) mientras hay un envío en curso no invoca onClose", async () => {
    let resolvePromise!: (value: string) => void;
    vi.mocked(membershipService.assignTemporaryPassword).mockReturnValue(
      new Promise((resolve) => {
        resolvePromise = resolve;
      }),
    );
    const onClose = vi.fn();
    const { container } = renderModal({ onClose });

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Temp0ral!",
    });
    await waitFor(() =>
      expect(
        (
          screen.getByRole("button", {
            name: "Guardando...",
          }) as HTMLButtonElement
        ).disabled,
      ).toBe(true),
    );

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onClose).not.toHaveBeenCalled();

    resolvePromise("Contraseña temporal asignada correctamente.");
  });

  it("error 422 mapea el mensaje del servidor al campo correspondiente, sin cerrar el modal", async () => {
    const validationError = {
      isAxiosError: true,
      response: {
        status: 422,
        data: {
          data: {
            errors: {
              temporaryPassword: [
                "El servidor rechazó la contraseña temporal.",
              ],
            },
          },
        },
      },
    };
    vi.mocked(membershipService.assignTemporaryPassword).mockRejectedValue(
      validationError,
    );
    const onClose = vi.fn();
    const { container } = renderModal({ onClose });

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Temp0ral!",
    });

    await waitFor(() =>
      expect(
        screen.getByText("El servidor rechazó la contraseña temporal."),
      ).toBeTruthy(),
    );
    expect(onClose).not.toHaveBeenCalled();
    expect(message.success).not.toHaveBeenCalled();
    // El campo conserva el valor ingresado para que el admin pueda corregirlo sin retipear.
    expect(fieldByName(container, "temporaryPassword").value).toBe("Temp0ral!");
  });

  it("error genérico (no 422) muestra el mensaje estándar del API, sin cerrar el modal", async () => {
    const networkError = { isAxiosError: true, response: undefined };
    vi.mocked(membershipService.assignTemporaryPassword).mockRejectedValue(
      networkError,
    );
    const onClose = vi.fn();
    const { container } = renderModal({ onClose });

    await fillAndSubmit(container, {
      password: "Temp0ral!",
      confirm: "Temp0ral!",
    });

    await waitFor(() =>
      expect(
        screen.getByText("No se pudo asignar la contraseña temporal."),
      ).toBeTruthy(),
    );
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe("AssignTemporaryPasswordModal — cierre", () => {
  it("Cancelar sin envío en curso invoca onClose y limpia el formulario", async () => {
    const onClose = vi.fn();
    const { container, rerender } = renderModal({ onClose });

    fireEvent.change(fieldByName(container, "temporaryPassword"), {
      target: { value: "Temp0ral!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onClose).toHaveBeenCalledTimes(1);

    // Reabrir (mismo componente montado, open vuelve a true) — el campo debe seguir limpio.
    rerender(
      <I18nProvider>
        <AssignTemporaryPasswordModal
          open={true}
          username="ana.perez"
          onClose={onClose}
        />
      </I18nProvider>,
    );
    expect(fieldByName(container, "temporaryPassword").value).toBe("");
  });
});
