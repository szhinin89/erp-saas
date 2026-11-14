import { describe, expect, it } from "vitest";
import { hasMappedFieldErrors, mapExpenseBackendErrors } from "./expenseBackendErrors";

/**
 * EXPENSE-UX-01 (B2) — claves reales del backend: ExceptionMiddleware pone en minúscula solo la
 * primera letra del `PropertyName` de FluentValidation (`lines[2].Quantity`, `supplierId`…).
 */

const KEYS = ["k-a", "k-b", "k-c"];
const fmt = (n: number, m: string) => `Línea ${n}: ${m}`;

describe("mapExpenseBackendErrors", () => {
  it("asocia errores de cabecera a su campo", () => {
    const map = mapExpenseBackendErrors(
      {
        supplierId: ["El proveedor es obligatorio."],
        documentNumber: ["El numero de documento es obligatorio."],
        authorizationNumber: ["Demasiado largo."],
        notes: ["Máximo 500."],
      },
      KEYS,
      fmt,
    );
    expect(map.header).toEqual({
      supplierId: "El proveedor es obligatorio.",
      documentNumber: "El numero de documento es obligatorio.",
      authorizationNumber: "Demasiado largo.",
      notes: "Máximo 500.",
    });
    expect(map.lines).toEqual({});
    expect(map.unmapped).toEqual([]);
  });

  it("asocia lines[i].Campo a la línea y campo correspondientes (índice → key de línea)", () => {
    const map = mapExpenseBackendErrors(
      {
        "lines[0].ExpenseSubcategoryId": ["La subcategoria de gasto es obligatoria por linea."],
        "lines[2].Quantity": ["La cantidad debe ser mayor a cero."],
        "lines[2].VatCode": ["El codigo IVA es obligatorio por linea."],
        "lines[1].unitPrice": ["El valor unitario no puede ser negativo."],
      },
      KEYS,
      fmt,
    );
    expect(map.lines).toEqual({
      "k-a": { expenseSubcategoryId: "La subcategoria de gasto es obligatoria por linea." },
      "k-c": {
        quantity: "La cantidad debe ser mayor a cero.",
        vatCode: "El codigo IVA es obligatorio por linea.",
      },
      "k-b": { unitPrice: "El valor unitario no puede ser negativo." },
    });
    expect(map.unmapped).toEqual([]);
    expect(hasMappedFieldErrors(map)).toBe(true);
  });

  it("una regla de línea sin propiedad (lines[i]) queda general con el número de línea", () => {
    const map = mapExpenseBackendErrors(
      { "lines[1]": ["El descuento no puede superar el subtotal de la linea."] },
      KEYS,
      fmt,
    );
    expect(map.lines).toEqual({});
    expect(map.unmapped).toEqual(["Línea 2: El descuento no puede superar el subtotal de la linea."]);
  });

  it("propiedad de línea desconocida o índice fuera de rango → mensaje general", () => {
    const map = mapExpenseBackendErrors(
      {
        "lines[0].Algo": ["Campo raro."],
        "lines[9].Quantity": ["Fuera de rango."],
      },
      KEYS,
      fmt,
    );
    expect(map.lines).toEqual({});
    expect(map.unmapped).toEqual(["Línea 1: Campo raro.", "Fuera de rango."]);
  });

  it("claves sin campo asociable (`_`, `lines`, desconocidas) quedan como mensaje general", () => {
    const map = mapExpenseBackendErrors(
      {
        _: ["La subcategoria 'X' esta inactiva."],
        lines: ["Debe incluir al menos una linea."],
        cualquierCosa: ["Otro."],
      },
      KEYS,
      fmt,
    );
    expect(map.header).toEqual({});
    expect(map.lines).toEqual({});
    expect(map.unmapped).toEqual([
      "La subcategoria 'X' esta inactiva.",
      "Debe incluir al menos una linea.",
      "Otro.",
    ]);
    expect(hasMappedFieldErrors(map)).toBe(false);
  });

  it("solo conserva el primer mensaje de cada campo e ignora mensajes vacíos", () => {
    const map = mapExpenseBackendErrors(
      {
        "lines[0].Quantity": ["  ", "Primero.", "Segundo."],
        dueDate: [],
      },
      KEYS,
      fmt,
    );
    expect(map.lines).toEqual({ "k-a": { quantity: "Primero." } });
    expect(map.header).toEqual({});
    expect(map.unmapped).toEqual([]);
  });
});
