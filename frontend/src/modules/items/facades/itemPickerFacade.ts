/**
 * itemPickerFacade — superficie pública de la selección MANUAL de Item (ZH-PRODUCT-SELECTOR-SSOT-01)
 * para formularios de otros módulos (inventory, pricing, …): el picker del Design System y la
 * búsqueda canónica que lo alimenta (GET /items vía itemLookupFacade.search: SKU/nombre/descripción,
 * solo activos, orden del backend). Mismo patrón que supplierPickerFacade.
 *
 * Fuera de alcance, con búsqueda propia y legítima: Ventas/POS (GET /sales/item-search — ranking
 * por barcode/SKU exacto, stock por bodega y precio resuelto) y el matching automático de
 * recepción de Compras (códigos de proveedor/similitud). Los módulos externos importan desde aquí,
 * nunca desde items/components o items/hooks.
 */
export { ItemLookupPicker } from "../components/ItemLookupPicker";
export type { ItemLookupPickerProps } from "../components/ItemLookupPicker";
export {
  ITEM_LOOKUP_DEBOUNCE_MS,
  ITEM_LOOKUP_MIN_LENGTH,
  useItemLookupSearch,
} from "../hooks/useItemLookupSearch";
export type { ItemLookupSearchOptions, ItemLookupSearchState } from "../hooks/useItemLookupSearch";
