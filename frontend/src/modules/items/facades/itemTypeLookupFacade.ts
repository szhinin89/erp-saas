/**
 * itemTypeLookupFacade — superficie pública read-only del catálogo de tipos de ítem
 * activos para consumidores externos (purchases: clasificación de productos nuevos).
 *
 * Expone el hook cacheado del módulo items (una sola petición por carga de página);
 * nunca la administración de tipos. Los módulos externos deben importar desde aquí,
 * nunca directamente de items/hooks o items/api.
 */
export { useItemTypeOptions } from "../hooks/useItemTypeOptions";
export type { ItemTypeDto } from "../api/itemTypeService";
