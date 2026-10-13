import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/** FASE 5 PURCHASE-UX — tamaño de cada lote de "Actualizar estados" (el backend admite hasta 500). */
export const RECEPTION_REFRESH_CHUNK_SIZE = 200;

/** Parte los ids en lotes de {@link RECEPTION_REFRESH_CHUNK_SIZE}. */
export function chunkIds(ids: readonly string[], size = RECEPTION_REFRESH_CHUNK_SIZE): string[][] {
  const chunks: string[][] = [];
  for (let i = 0; i < ids.length; i += size) chunks.push(ids.slice(i, i + size));
  return chunks;
}

/**
 * FASE 5 PURCHASE-UX — fusiona el resultado de "Actualizar estados" en las filas ya importadas, por
 * `documentId`, SIN cambiar orden ni cantidad de filas. Todo lo que el backend recalcula se toma de la
 * respuesta; `supplierTradeName` (solo llega al descargar el XML, no se persiste) se conserva. Las filas
 * que el backend no devuelve (fuera de alcance) quedan como estaban.
 */
export function mergeRefreshedReceptionItems(
  items: readonly PurchaseReceptionItem[],
  refreshed: readonly PurchaseReceptionItem[],
): PurchaseReceptionItem[] {
  const byId = new Map(refreshed.map((r) => [r.documentId, r]));
  return items.map((item) => {
    const fresh = byId.get(item.documentId);
    if (!fresh) return item;
    return {
      ...item,
      ...fresh,
      supplierTradeName: item.supplierTradeName ?? fresh.supplierTradeName ?? null,
    };
  });
}
