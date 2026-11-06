import { describe, expect, it } from "vitest";
import es from "./locales/es.json";
import en from "./locales/en.json";

/**
 * FUNC-DEACTIVATION-CONFIRM-01 — claves de confirmación de desactivación (catálogos, tipos de
 * ítem, cajas, variantes, imágenes): existen en es y en, conservan los mismos parámetros
 * dinámicos y el inglés está realmente traducido.
 */

const KEYS: Record<string, string[]> = {
  "catalog.deactivate.confirm.title": ["name"],
  "catalog.deactivate.confirm.message": ["name"],
  "itemTypes.deactivate.confirm.title": ["code", "name"],
  "itemTypes.deactivate.confirm.message": ["name"],
  "cashRegisters.deactivate.confirm.title": ["code", "name"],
  "cashRegisters.deactivate.confirm.message": ["name"],
  "items.variants.deactivate.confirm.title": ["label"],
  "items.variants.deactivate.confirm.message": ["label"],
  "items.variants.deactivate.unknownLabel": [],
  "items.images.deactivate.confirm.title": ["label"],
  "items.images.deactivate.confirm.message": ["label"],
  "items.images.deactivate.confirm.messageMain": ["label"],
  "items.images.deactivate.unknownLabel": [],
  "common.deactivate": [],
  "common.cancel": [],
};

const esDict = es as Record<string, string>;
const enDict = en as Record<string, string>;

function params(text: string): string[] {
  return [...text.matchAll(/\{\{(\w+)\}\}/g)].map((m) => m[1]).sort();
}

describe("i18n — confirmaciones de desactivación", () => {
  it.each(Object.entries(KEYS))("%s existe en es/en con los mismos parámetros", (key, expected) => {
    expect(esDict[key]).toBeTruthy();
    expect(enDict[key]).toBeTruthy();
    expect(enDict[key]).not.toBe(esDict[key]);
    expect(params(esDict[key])).toEqual([...expected].sort());
    expect(params(enDict[key])).toEqual([...expected].sort());
  });

  it("los mensajes de imagen explican la recuperación como secundaria en ambos idiomas", () => {
    for (const key of [
      "items.images.deactivate.confirm.message",
      "items.images.deactivate.confirm.messageMain",
    ]) {
      expect(esDict[key]).toContain("se recuperará como secundaria");
      expect(enDict[key]).toContain("return as a secondary image");
    }
  });
});
