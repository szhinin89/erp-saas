import { describe, expect, it } from "vitest";
import { ITEM_FORM_TABS } from "./itemFormTabConfig";

describe("ITEM_FORM_TABS", () => {
  it("mantiene el mismo orden de pestañas para crear y editar", () => {
    expect(ITEM_FORM_TABS.map((tab) => tab.id)).toEqual([
      "principal",
      "inventory-presentations",
      "images",
      "advanced",
    ]);
    expect(ITEM_FORM_TABS.map((tab) => tab.labelFb)).toEqual([
      "Principal",
      "Inventario y presentaciones",
      "Imágenes",
      "Avanzado",
    ]);
  });
});
