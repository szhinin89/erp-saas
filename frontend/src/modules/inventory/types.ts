// Superficie pública de tipos del módulo inventory para consumo cross-module.
// Fuente única de verdad: ./warehouses/api/warehouseService y ./stock/api/stockService
// — este archivo solo re-exporta.
export type { WarehouseDto } from "./warehouses/api/warehouseService";
export type { ItemWarehouseAvailabilityDto } from "./stock/api/stockService";
