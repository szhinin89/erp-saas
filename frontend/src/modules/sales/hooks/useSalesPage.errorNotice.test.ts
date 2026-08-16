import { describe, it, expect, vi, afterEach } from "vitest";
import { buildSalesErrorNotice, extractErrorText } from "./useSalesPage";

function axiosError(status: number, data: unknown): unknown {
  return {
    isAxiosError: true,
    response: { status, data },
  };
}

describe("SALES-RETAIL-READY-01-FIX03 — buildSalesErrorNotice / extractErrorText", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("con data.errors como array, muestra el detalle y no solo message.user", () => {
    const err = axiosError(422, {
      code: "VALIDATION_ERROR",
      message: { user: "Datos inválidos. Revisa el formulario.", dev: "Validation failed." },
      data: {
        errors: [
          "Línea 'CUBHUEVO — CUBETA DE HUEVO': stock insuficiente en la bodega seleccionada (disponible: 0, solicitado: 1,0000). Reduce la cantidad, elige otra bodega, o realiza un ingreso de inventario antes de emitir.",
        ],
      },
    });

    const notice = buildSalesErrorNotice(
      err,
      "No se puede emitir la factura.",
      "Revise los datos de la factura antes de emitir.",
    );

    expect(notice.title).toBe("No se puede emitir la factura.");
    expect(notice.detail).toContain("CUBHUEVO");
    expect(notice.detail).toContain("stock insuficiente");
    expect(notice.detail).not.toBe("Datos inválidos. Revisa el formulario.");
  });

  it("con múltiples data.errors, incluye todos los detalles (no solo el primero)", () => {
    const err = axiosError(422, {
      message: { user: "Datos inválidos. Revisa el formulario." },
      data: {
        errors: [
          "Línea 'A': stock insuficiente.",
          "Línea 'B': stock insuficiente.",
        ],
      },
    });

    const notice = buildSalesErrorNotice(err, "No se puede emitir la factura.", "fallback");

    expect(notice.detail).toContain("Línea 'A'");
    expect(notice.detail).toContain("Línea 'B'");
  });

  it("con data.errors como objeto (mapa campo→mensajes), muestra los detalles", () => {
    const err = axiosError(422, {
      message: { user: "Datos inválidos. Revisa el formulario." },
      data: {
        errors: {
          customerId: ["Seleccione un cliente."],
          lines: ["Agregue al menos un producto."],
        },
      },
    });

    const notice = buildSalesErrorNotice(err, "No se puede guardar la venta.", "fallback");

    expect(notice.detail).toContain("Seleccione un cliente.");
    expect(notice.detail).toContain("Agregue al menos un producto.");
  });

  it("sin data.errors, usa message.user", () => {
    const err = axiosError(400, {
      message: { user: "El punto de emisión no está configurado." },
    });

    const notice = buildSalesErrorNotice(err, "No se puede emitir la factura.", "fallback");

    expect(notice.detail).toBe("El punto de emisión no está configurado.");
  });

  it("sin data.errors ni message.user, usa el fallback local", () => {
    const err = { isAxiosError: false };

    const notice = buildSalesErrorNotice(
      err,
      "No se puede guardar la venta.",
      "No se pudieron guardar los cambios pendientes.",
    );

    expect(notice.detail).toBe("No se pudieron guardar los cambios pendientes.");
  });

  it("nunca muestra message.dev (\"Validation failed.\") al usuario", () => {
    const err = axiosError(422, {
      message: { user: "Datos inválidos. Revisa el formulario.", dev: "Validation failed." },
      data: { errors: ["Línea 'X': stock insuficiente."] },
    });

    const notice = buildSalesErrorNotice(err, "No se puede emitir la factura.", "fallback");

    expect(notice.detail).not.toContain("Validation failed.");
    expect(notice.title).not.toContain("Validation failed.");
  });

  it("extractErrorText: mismo detalle que buildSalesErrorNotice, sin título (para toasts/paneles de un solo string)", () => {
    const err = axiosError(422, {
      message: { user: "Datos inválidos. Revisa el formulario." },
      data: { errors: ["No se pudo obtener el precio del producto: sin lista de precios."] },
    });

    expect(extractErrorText(err, "fallback")).toBe(
      "No se pudo obtener el precio del producto: sin lista de precios.",
    );
  });

  it("caso real reportado: authorize con stock insuficiente produce título + detalle accionable de la línea", () => {
    // Payload exacto reportado en SALES-RETAIL-READY-01-FIX03.
    const err = axiosError(422, {
      code: "VALIDATION_ERROR",
      severity: "error",
      message: {
        user: "Datos inválidos. Revisa el formulario.",
        dev: "Validation failed.",
      },
      data: {
        errors: [
          "Línea 'CUBHUEVO — CUBETA DE HUEVO': stock insuficiente en la bodega seleccionada (disponible: 0, solicitado: 1,0000). Reduce la cantidad, elige otra bodega, o realiza un ingreso de inventario antes de emitir.",
        ],
      },
    });

    const notice = buildSalesErrorNotice(
      err,
      "No se puede emitir la factura.",
      "Revise los datos de la factura antes de emitir.",
    );

    expect(notice.title).toBe("No se puede emitir la factura.");
    expect(notice.detail).toBe(
      "Línea 'CUBHUEVO — CUBETA DE HUEVO': stock insuficiente en la bodega seleccionada (disponible: 0, solicitado: 1,0000). Reduce la cantidad, elige otra bodega, o realiza un ingreso de inventario antes de emitir.",
    );
  });
});
