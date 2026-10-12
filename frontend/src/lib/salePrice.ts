import {
  calcMarginAmount,
  calcMarginPercent,
  calcMarkupPercent,
} from "./margin";
import { roundToDecimals } from "./sanitizers";

/**
 * Fórmula única neto ↔ bruto del precio de venta. `BaseSalePrice` se guarda siempre SIN IVA (por
 * unidad base); el PVP con IVA es solo entrada/simulación. `vatPercent` sale del catálogo SRI de
 * IVA (nunca hardcodeado); 0 % = el PVP final es el mismo precio sin IVA.
 */
export function netFromGross(gross: number, vatPercent: number): number {
  return vatPercent > 0 ? gross / (1 + vatPercent / 100) : gross;
}

export function grossFromNet(net: number, vatPercent: number): number {
  return vatPercent > 0 ? net * (1 + vatPercent / 100) : net;
}

export interface SalePriceBreakdown {
  /** `BaseSalePrice` persistible: sin IVA, redondeado a la precisión de precios de la empresa. */
  net: number;
  vatAmount: number;
  /** PVP con IVA que resulta del neto persistible — lo que realmente se cobrará. */
  effectiveGross: number;
  /** El PVP ingresado no es alcanzable con la precisión de precios configurada. */
  differs: boolean;
  /** Utilidad, margen sobre precio y recargo sobre costo; `null` sin costo positivo. */
  profit: number | null;
  marginPct: number | null;
  markupPct: number | null;
}

/**
 * Desglosa un PVP final con IVA. Todo se recalcula desde el neto YA redondeado (el valor que se
 * persiste), nunca desde el PVP ingresado. `null` si el PVP no es positivo.
 */
export function salePriceFromGross(
  gross: number,
  vatPercent: number,
  priceDecimals: number,
  unitCost: number | null,
): SalePriceBreakdown | null {
  if (!Number.isFinite(gross) || gross <= 0) return null;
  const net = roundToDecimals(netFromGross(gross, vatPercent), priceDecimals);
  const effectiveGross = roundToDecimals(grossFromNet(net, vatPercent), priceDecimals);
  const hasCost = unitCost != null && unitCost > 0;
  return {
    net,
    vatAmount: roundToDecimals(effectiveGross - net, priceDecimals),
    effectiveGross,
    differs: effectiveGross !== roundToDecimals(gross, priceDecimals),
    profit: hasCost ? calcMarginAmount(unitCost, net) : null,
    marginPct: hasCost ? calcMarginPercent(unitCost, net) : null,
    markupPct: hasCost ? calcMarkupPercent(unitCost, net) : null,
  };
}
