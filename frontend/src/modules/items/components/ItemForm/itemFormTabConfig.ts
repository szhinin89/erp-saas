export type ItemFormTabId =
  | "principal"
  | "inventory-presentations"
  | "images"
  | "advanced";

export const ITEM_FORM_TABS: {
  id: ItemFormTabId;
  labelKey: string;
  labelFb: string;
}[] = [
  {
    id: "principal",
    labelKey: "items.tabs.principal",
    labelFb: "Principal",
  },
  {
    id: "inventory-presentations",
    labelKey: "items.tabs.inventoryPresentations",
    labelFb: "Inventario y presentaciones",
  },
  { id: "images", labelKey: "items.tabs.images", labelFb: "Imágenes" },
  { id: "advanced", labelKey: "items.tabs.advanced", labelFb: "Avanzado" },
];
