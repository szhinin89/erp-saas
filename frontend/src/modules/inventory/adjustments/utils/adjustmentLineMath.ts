import type { ItemPackagingLevelDto } from "../../../../types/items";

/**
 * INVENTORY-ADJUSTMENTS-03 — matemática de presentación/empaque de una línea de ajuste.
 *
 * Auditoría de reutilización: se revisó `purchases/utils/purchaseLinePresentation.ts`
 * (`buildPurchaseLinePresentation`) y `purchases/utils/purchaseCalc.ts`. Ambos contienen esta
 * misma conversión, pero embebida en un view-model acoplado a `PurchaseLineFormValues` (snapshot
 * XML del SRI, IVA/ICE/IRBPNR, prorrateo de flete, margen contra PVP) — nada de eso existe ni
 * aplica en un ajuste de inventario, y no hay en ese archivo una función pura reutilizable que
 * exponga solo la conversión. De ahí estas funciones puras mínimas, locales a Ajustes: NO se
 * bifurca ni se copia la fórmula de costo/margen de Compras, solo la equivalencia de unidades
 * (`QuantityInBaseUom = Quantity * ConversionFactor`), que es la misma regla que aplica el
 * backend al persistir la línea.
 *
 * El backend sigue siendo la autoridad: esto es cálculo visual previo, nunca la fuente de verdad.
 */

/** Factor de conversión de la presentación seleccionada; 1 cuando no hay presentación. */
export function resolveConversionFactor(
  packagingLevels: ItemPackagingLevelDto[],
  packagingLevelId: string | null,
): number {
  if (!packagingLevelId) return 1;
  const level = packagingLevels.find((p) => p.id === packagingLevelId);
  const factor = level?.baseQuantity ?? 1;
  return factor > 0 ? factor : 1;
}

/** `QuantityInBaseUom = Quantity * ConversionFactor` — misma regla que el backend. */
export function computeQuantityInBaseUom(
  quantity: number,
  conversionFactor: number,
): number {
  const qty = Number.isFinite(quantity) ? quantity : 0;
  const factor = conversionFactor > 0 ? conversionFactor : 1;
  return qty * factor;
}

/** Código de UOM en el que se teclea la cantidad (presentación o unidad base). */
export function resolveLineUomCode(
  packagingLevels: ItemPackagingLevelDto[],
  packagingLevelId: string | null,
  baseUomCode: string,
): string {
  if (!packagingLevelId) return baseUomCode;
  return (
    packagingLevels.find((p) => p.id === packagingLevelId)?.uomCode ?? baseUomCode
  );
}

/**
 * Solo aviso visual (no bloqueante): el backend valida stock real al Ejecutar.
 * Devuelve false cuando el stock aún no se conoce (`null`) — nunca se asume 0.
 */
export function isStockInsufficient(
  quantityInBaseUom: number,
  currentStock: number | null,
): boolean {
  if (currentStock === null) return false;
  return quantityInBaseUom > currentStock;
}
