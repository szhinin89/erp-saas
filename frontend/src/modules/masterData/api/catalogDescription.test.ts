import { describe, expect, it } from "vitest";
import { describeCatalogCode } from "./catalogDescription";

/** Unitaria: traducción de códigos de catálogo a su descripción oficial (el código nunca se altera). */
const unknown = (c: string) => `${c} (no catalogado)`;
const ID_TYPES = [
  { code: "04", name: "Registro Único de Contribuyentes" },
  { code: "05", name: "Cédula de ciudadanía" },
];

describe("describeCatalogCode", () => {
  it("devuelve el nombre del catálogo (04 = Registro Único de Contribuyentes)", () => {
    expect(describeCatalogCode("04", ID_TYPES, false, unknown)).toBe("Registro Único de Contribuyentes");
  });
  it("mientras el catálogo carga o no está disponible no muestra el código crudo", () => {
    expect(describeCatalogCode("04", [], true, unknown)).toBeNull();
    expect(describeCatalogCode("04", [], false, unknown)).toBeNull();
  });
  it("con el catálogo cargado y código ausente indica que no está catalogado, conservando el código", () => {
    expect(describeCatalogCode("99", ID_TYPES, false, unknown)).toBe("99 (no catalogado)");
  });
  it("valor vacío o nulo no produce texto", () => {
    expect(describeCatalogCode(null, ID_TYPES, false, unknown)).toBeNull();
    expect(describeCatalogCode("", ID_TYPES, false, unknown)).toBeNull();
    expect(describeCatalogCode(undefined, ID_TYPES, false, unknown)).toBeNull();
  });
});
