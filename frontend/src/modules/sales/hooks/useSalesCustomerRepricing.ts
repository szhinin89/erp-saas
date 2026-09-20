import { useCallback, useRef, useState } from "react";
import type { CustomerPickerRow } from "../../masterData/types/businessPartner.types";
import {
  salesRepricingPreviewService,
  type SalesRepricingPreviewItemDto,
} from "../api/salesRepricingPreviewService";

/**
 * SALES-CUSTOMER-CHANGE-REPRICE-UX-06C2 / 06C2A / 06C2B: pieza autocontenida de "cambio de
 * cliente con líneas ya cargadas" — separada de useSalesPage (que ya es enorme) para poder
 * testear la decisión (aplicar directo / abrir modal / fail-closed) y la mutación de líneas sin
 * tener que montar todo el hook de la página con sus decenas de dependencias.
 *
 * Reglas de negocio (auditadas en SALES-CUSTOMER-CHANGE-REPRICE-AUDIT-06C):
 * - El precio "actual" mostrado SIEMPRE es line.unitPrice (lo que realmente terminaría
 *   facturado, manual o automático) — nunca preview.OldResolvedPrice, que puede no coincidir
 *   si el cajero editó el precio a mano.
 * - El modal SOLO se muestra cuando al menos una línea cambia de PRECIO (priceChanged) — un
 *   cambio de metadata (lista/BasePrice/descripción) sin cambio de precio nunca interrumpe el
 *   flujo POS con un modal (06C2B).
 * - Independientemente de si hay modal, la metadata de Pricing (_pvp/_basePrice/
 *   _priceListName/_discountDescription) de TODA línea con preview se sincroniza con el cliente
 *   nuevo al aplicar el cambio — nunca queda apuntando al cliente anterior. Una línea cuyo
 *   precio no cambió mantiene su unitPrice y su `_isManualPrice` tal cual estaban: sincronizar
 *   metadata no es "reemplazar el precio", así que nunca se fuerza `_isManualPrice = false` por
 *   esa vía sola.
 * - Un error en el preview es fail-closed: no se cambia el cliente ni se toca ninguna línea.
 * - Cancelar el modal deja el cliente y TODAS las líneas intactas, incluida la metadata de las
 *   líneas que solo iban a sincronizar metadata.
 */

export interface RepricingLineInput {
  key: number;
  itemId: string | null | undefined;
  description: string;
  unitPrice: number;
  conversionFactor?: number;
  // Metadata de pricing actualmente en la línea — se usa SOLO para decidir si hace falta
  // sincronizarla (evita reconstruir líneas idénticas cuando lista/BasePrice/descripción no
  // cambiaron realmente).
  _pvp?: number;
  _basePrice?: number;
  _priceListName?: string;
  _discountDescription?: string | null;
}

/**
 * SALES-CUSTOMER-REPRICE-METADATA-06C2A: campos de línea que produce resolver un precio del
 * Pricing Engine — MISMA semántica exacta que addLineWithItem usa para una línea agregada desde
 * cero (ver useSalesPage.ts): `_pvp`/`_basePrice` quedan SIN escalar por conversionFactor
 * (tal como el backend los resuelve, en unidad base), solo `unitPrice` (el precio facturado) se
 * escala. Única fuente de este mapeo — evita que Sales reimplemente "Pricing → línea" en más de
 * un lugar y que ambos caminos diverjan.
 */
export interface ResolvedPricingLineFields {
  unitPrice: number;
  _pvp: number;
  _basePrice: number;
  _priceListName: string;
  _discountDescription: string | null;
  _isManualPrice: false;
}

export function mapResolvedPricingToLineFields(
  resolvedUnitPrice: number,
  basePrice: number,
  priceListName: string,
  discountDescription: string | null,
  conversionFactor: number,
): ResolvedPricingLineFields {
  return {
    unitPrice: resolvedUnitPrice * conversionFactor,
    _pvp: resolvedUnitPrice,
    _basePrice: basePrice,
    _priceListName: priceListName,
    _discountDescription: discountDescription,
    _isManualPrice: false,
  };
}

/** Solo la metadata de pricing (sin unitPrice ni _isManualPrice) — para sincronizar una línea
 * cuyo precio facturado NO cambia, sin tocar su manualidad. */
export type PricingMetadataFields = Pick<
  ResolvedPricingLineFields,
  "_pvp" | "_basePrice" | "_priceListName" | "_discountDescription"
>;

export interface RepricingRow {
  key: number;
  itemId: string;
  description: string;
  /** Precio real actualmente facturado (line.unitPrice, manual o automático) — lo que se
   * muestra como "Precio actual" en el modal. */
  currentUnitPrice: number;
  /** Campos completos a aplicar si se confirma (precio + metadata + _isManualPrice=false) —
   * fields.unitPrice (ya escalado y redondeado) es el "Nuevo precio" mostrado en el modal. */
  fields: ResolvedPricingLineFields;
}

export interface MetadataSyncRow {
  key: number;
  itemId: string;
  /** Metadata a sincronizar preservando unitPrice/_isManualPrice actuales de la línea. */
  metadata: PricingMetadataFields;
}

/** Resultado de comparar TODAS las líneas con ítem contra el preview del cliente nuevo —
 * `priceChangedRows` es lo único que se muestra en el modal; `metadataOnlyRows` se aplica
 * siempre en silencio (con o sin modal), nunca se le pide confirmación aparte. */
export interface RepricingPlan {
  priceChangedRows: RepricingRow[];
  metadataOnlyRows: MetadataSyncRow[];
}

function roundTo(value: number, decimals: number): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

function metadataAlreadyInSync(
  line: RepricingLineInput,
  fields: ResolvedPricingLineFields,
  decimals: number,
): boolean {
  const pvpMatches =
    line._pvp !== undefined && roundTo(line._pvp, decimals) === roundTo(fields._pvp, decimals);
  const baseMatches =
    line._basePrice !== undefined &&
    roundTo(line._basePrice, decimals) === roundTo(fields._basePrice, decimals);
  const nameMatches = (line._priceListName ?? null) === fields._priceListName;
  const discountMatches = (line._discountDescription ?? null) === fields._discountDescription;
  return pvpMatches && baseMatches && nameMatches && discountMatches;
}

/**
 * Separa las líneas con ítem en dos grupos, comparando SIEMPRE line.unitPrice (precio real,
 * manual o automático) contra el precio resuelto para el cliente nuevo — nunca contra
 * preview.OldResolvedPrice:
 * - priceChangedRows: el precio facturado cambiaría — van al modal, y al confirmar reemplazan
 *   precio + metadata + marcan _isManualPrice=false.
 * - metadataOnlyRows: el precio facturado NO cambia, pero lista/BasePrice/descripción sí — se
 *   sincronizan en silencio (nunca abren modal, nunca tocan unitPrice ni _isManualPrice). Una
 *   línea cuya metadata ya coincide con el cliente nuevo (comparación redondeada) no genera
 *   ninguna fila — no hay nada que sincronizar.
 */
export function buildRepricingPlan(
  lines: RepricingLineInput[],
  previewItems: SalesRepricingPreviewItemDto[],
  decimals: number,
): RepricingPlan {
  const byItem = new Map(previewItems.map((p) => [p.itemId, p]));
  const priceChangedRows: RepricingRow[] = [];
  const metadataOnlyRows: MetadataSyncRow[] = [];

  for (const line of lines) {
    if (!line.itemId) continue;
    const preview = byItem.get(line.itemId);
    if (!preview) continue; // ítem sin precio resolvible para alguno de los dos clientes — no hay nada que sincronizar

    const conversionFactor = line.conversionFactor ?? 1;
    const fields = mapResolvedPricingToLineFields(
      preview.newResolvedPrice,
      preview.newBasePrice,
      preview.newPriceListName,
      preview.newDiscountDescription,
      conversionFactor,
    );
    const newUnitPrice = roundTo(fields.unitPrice, decimals);
    const currentUnitPrice = roundTo(line.unitPrice, decimals);

    if (currentUnitPrice !== newUnitPrice) {
      priceChangedRows.push({
        key: line.key,
        itemId: line.itemId,
        description: line.description,
        currentUnitPrice: line.unitPrice,
        fields: { ...fields, unitPrice: newUnitPrice },
      });
      continue;
    }

    if (!metadataAlreadyInSync(line, fields, decimals)) {
      metadataOnlyRows.push({
        key: line.key,
        itemId: line.itemId,
        metadata: {
          _pvp: fields._pvp,
          _basePrice: fields._basePrice,
          _priceListName: fields._priceListName,
          _discountDescription: fields._discountDescription,
        },
      });
    }
  }

  return { priceChangedRows, metadataOnlyRows };
}

/**
 * Aplica un RepricingPlan completo a las líneas: las de `priceChangedRows` reemplazan precio +
 * metadata + `_isManualPrice=false`; las de `metadataOnlyRows` sincronizan SOLO metadata,
 * preservando unitPrice y la manualidad actuales; el resto de líneas (sin cambio de ningún tipo,
 * o sin itemId) quedan exactamente intactas, misma referencia. Ningún otro campo (quantity,
 * discountPct, warehouseId, vatCode, iceCode, notes, packagingLevelId, uomCode, etc.) se toca.
 */
export function applyRepricingPlanToLines<
  T extends {
    _key: number;
    unitPrice?: number;
    _pvp?: number;
    _basePrice?: number;
    _priceListName?: string;
    _discountDescription?: string | null;
    _isManualPrice?: boolean;
  },
>(lines: T[], plan: RepricingPlan): T[] {
  const priceRowByKey = new Map(plan.priceChangedRows.map((r) => [r.key, r]));
  const metadataRowByKey = new Map(plan.metadataOnlyRows.map((r) => [r.key, r]));
  return lines.map((line) => {
    const priceRow = priceRowByKey.get(line._key);
    if (priceRow) return { ...line, ...priceRow.fields };
    const metadataRow = metadataRowByKey.get(line._key);
    if (metadataRow) return { ...line, ...metadataRow.metadata };
    return line;
  });
}

export interface RepricingModalState {
  customer: CustomerPickerRow;
  /** Solo las líneas cuyo PRECIO cambia — lo único que se muestra en la tabla del modal. */
  rows: RepricingRow[];
  /** Líneas cuyo precio no cambia pero sí su metadata — se aplican en silencio junto con `rows`
   * al confirmar, nunca se muestran ni se piden aparte. */
  metadataOnlyRows: MetadataSyncRow[];
}

export interface UseSalesCustomerRepricingOptions {
  /** Aplica el cambio de cliente "de verdad" (customerId, perfil, paymentTerm/schedule) — mismo
   * comportamiento que existía antes de esta tarea, reutilizado tal cual. */
  applyCustomerChange: (customer: CustomerPickerRow | null) => Promise<void> | void;
  /** Aplica un RepricingPlan a las líneas del formulario (setValue("lines", ...)). Se invoca
   * tanto en el camino directo (solo metadata) como al confirmar el modal (precio + metadata). */
  applyPlanToLines: (plan: RepricingPlan) => void;
  /** Fail-closed: se invoca cuando el preview falla — NO se cambia el cliente ni se toca ninguna línea. */
  onPreviewError: (err: unknown) => void;
}

export function useSalesCustomerRepricing({
  applyCustomerChange,
  applyPlanToLines,
  onPreviewError,
}: UseSalesCustomerRepricingOptions) {
  const [loading, setLoading] = useState(false);
  const [pending, setPending] = useState<RepricingModalState | null>(null);
  // Guarda contra doble click / doble disparo mientras el preview está en vuelo — una petición
  // concurrente se ignora en vez de encolarse o pisar la anterior.
  const inFlightRef = useRef(false);

  const requestCustomerChange = useCallback(
    async (
      customer: CustomerPickerRow | null,
      lines: RepricingLineInput[],
      oldCustomerId: string | undefined,
      decimals: number,
    ) => {
      if (inFlightRef.current) return;

      const linesWithItem = lines.filter((l) => !!l.itemId);
      if (!customer || linesWithItem.length === 0) {
        await applyCustomerChange(customer);
        return;
      }

      inFlightRef.current = true;
      setLoading(true);
      try {
        const itemIds = Array.from(new Set(linesWithItem.map((l) => l.itemId as string)));
        const preview = await salesRepricingPreviewService.preview({
          itemIds,
          oldCustomerId,
          newCustomerId: customer.id,
        });
        const plan = buildRepricingPlan(linesWithItem, preview, decimals);

        if (plan.priceChangedRows.length === 0) {
          // Sin cambio de precio en ninguna línea: nunca se interrumpe con modal — pero la
          // metadata (si cambió) se sincroniza igual, en silencio.
          if (plan.metadataOnlyRows.length > 0) {
            applyPlanToLines(plan);
          }
          await applyCustomerChange(customer);
          return;
        }

        setPending({ customer, rows: plan.priceChangedRows, metadataOnlyRows: plan.metadataOnlyRows });
      } catch (err) {
        onPreviewError(err);
      } finally {
        setLoading(false);
        inFlightRef.current = false;
      }
    },
    [applyCustomerChange, applyPlanToLines, onPreviewError],
  );

  const cancel = useCallback(() => setPending(null), []);

  const confirm = useCallback(async () => {
    if (!pending) return;
    const { customer, rows, metadataOnlyRows } = pending;
    applyPlanToLines({ priceChangedRows: rows, metadataOnlyRows });
    setPending(null);
    await applyCustomerChange(customer);
  }, [pending, applyCustomerChange, applyPlanToLines]);

  return { loading, pending, requestCustomerChange, cancel, confirm };
}
