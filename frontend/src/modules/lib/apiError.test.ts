import { describe, expect, it, vi } from "vitest";
import { readApiErrorMessage, readApiErrorMessages } from "./apiError";
import { applyServerErrors } from "./validationErrors";

function axiosError(status: number, data: unknown): unknown {
  return {
    isAxiosError: true,
    response: { status, data },
  };
}

describe("apiError validation messages", () => {
  it("prioriza data.errors sobre message.user para no ocultar la validación específica", () => {
    const err = axiosError(422, {
      code: "VALIDATION_ERROR",
      message: { user: "Datos inválidos. Revisa el formulario." },
      data: {
        errors: ["El proveedor 'Proveedor Inactivo S.A.' se encuentra inactivo."],
      },
    });

    expect(readApiErrorMessage(err)).toBe(
      "El proveedor 'Proveedor Inactivo S.A.' se encuentra inactivo.",
    );
    expect(readApiErrorMessages(err)).toEqual([
      "El proveedor 'Proveedor Inactivo S.A.' se encuentra inactivo.",
    ]);
  });

  it("applyServerErrors maneja data.errors plano como error general", () => {
    const err = axiosError(422, {
      data: {
        errors: ["El proveedor seleccionado se encuentra inactivo."],
      },
    });
    const setError = vi.fn();
    const fallback = vi.fn();

    const handled = applyServerErrors(err, setError, fallback);

    expect(handled).toBe(true);
    expect(setError).not.toHaveBeenCalled();
    expect(fallback).toHaveBeenCalledWith(
      "El proveedor seleccionado se encuentra inactivo.",
    );
  });
});
