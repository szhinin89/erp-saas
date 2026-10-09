import { describe, expect, it } from "vitest";
import { importSuccessMessage } from "./importResultMessage";

describe("importSuccessMessage", () => {
  it("usa singular cuando se importa 1 fila", () => {
    expect(importSuccessMessage(1, "proveedores", "proveedor")).toBe(
      "Se importó 1 proveedor correctamente.",
    );
  });

  it("usa plural para n filas", () => {
    expect(importSuccessMessage(3, "proveedores", "proveedor")).toBe(
      "Se importaron 3 proveedores correctamente.",
    );
  });

  it("sin singular configurado conserva el mensaje anterior", () => {
    expect(importSuccessMessage(1, "clientes")).toBe("Se importaron 1 clientes correctamente.");
  });
});
