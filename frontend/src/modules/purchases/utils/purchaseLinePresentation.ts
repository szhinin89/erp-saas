import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { calcMarginPercent } from "../../../lib/margin";

/** Dato desconocido — nunca se confunde con un valor real (p. ej. stock 0). */
const UNKNOWN = "—";
type TFunction = (
  key: string,
  fallbackOrParams?: string | Record<string, string | number>,
) => string;

export type LineStatusTone = "success" | "warning" | "danger";

/**
 * View model de presentación de una línea de compra — responde, en este orden, "qué llegó del
 * proveedor", "con qué Item del ERP está relacionado", "qué impacto tiene para el negocio" y "qué
 * acción está pendiente". Centraliza toda la lógica de null-check y de distinguir "valor real"
 * (p. ej. 0 unidades de stock) de "valor desconocido" (contexto aún no cargado / sin Item) — el
 * JSX de `PurchaseLineCard` solo consume campos ya resueltos, nunca vuelve a preguntar `?.`/`??`.
 */
export interface PurchaseLinePresentationVM {
  /** 1. Qué llegó del proveedor — "Producto recibido (XML)". */
  xml: {
    /** false en una línea de compra manual — el bloque igual se muestra, con nota + "—". */
    hasOrigin: boolean;
    supplierCode: string;
    supplierAuxCode: string;
    hasSupplierAuxCode: boolean;
    description: string;
    quantity: string;
    unitPrice: string;
    discount: string;
    vatPercentage: string;
    taxValue: string;
    totalLine: string;
  };
  /** 2. Con qué Item del ERP está relacionado — siempre visible, incluso sin Item aún. */
  item: {
    hasItem: boolean;
    isLoading: boolean;
    sku: string;
    name: string;
    /** No existe en PurchaseItemContextDto hoy — se muestra "—" a propósito, nunca "0" ni inventado. */
    uom: string;
    baseUom: string;
    matchStatus: ItemMatchStatus | null;
  };
  inventory: {
    presentation: string;
    hasPresentation: boolean;
    presentationLabel: string;
    conversionDetail: string;
    /** Redacción legible de la conversión (p. ej. "1 PACA X12 = 12 unidades") —
     * "" cuando no hay presentación con nombre legible y factor > 1; nunca
     * expone el código técnico crudo de UOM. Usar en vez de conversionDetail
     * para mostrar al usuario. */
    equivalenceDetail: string;
    baseQuantity: string;
    baseUnitCost: string;
  };
  /** 3. Impacto para el negocio — "Información Comercial", menor peso visual, solo si hay contexto real. */
  commercial: {
    /** Contexto realmente cargado (Item + datos resueltos) — controla real vs "—", nunca 0 falso. */
    hasContext: boolean;
    stock: {
      current: string;
      available: string;
      reserved: string;
      statusLabel: string;
      isCritical: boolean;
    };
    costs: {
      average: string;
      last: string;
      showDeviationAlert: boolean;
      deviationLabel: string;
    };
    profitability: {
      pvp: string;
      marginPct: string;
      marginPctValue: number;
      maxDiscountPercent: string;
    };
  };
  /** 4. Resumen de estado de la línea — solo interpreta visualmente estados ya existentes. */
  status: {
    icon: string;
    label: string;
    tone: LineStatusTone;
  };
}

export function buildPurchaseLinePresentation(
  line: PurchaseLineFormValues,
  t?: TFunction,
): PurchaseLinePresentationVM {
  const decimals = getDecimalConfig();
  const ctx = line.context;
  const hasItem = !!line.itemId;
  const isLoading = !!line._contextLoading;
  const hasContext = hasItem && !isLoading && !!ctx;
  const hasOrigin = !!line.purchaseReceptionLineId || !!line.xmlSupplierCode;

  const quantity = line.quantity ?? 0;
  const unitPrice = line.unitPrice ?? 0;

  const shortName = ctx?.shortName || "";
  const displayName =
    shortName || line.description?.split(" — ")[1] || line.description || "";

  const currentStock = ctx?.currentStock ?? 0;
  const averageCost = ctx?.averageCost ?? 0;
  const pvp = ctx?.pvp ?? 0;
  const selectedPackaging = ctx?.packagingLevels?.find(
    (p) => p.id === line.packagingLevelId,
  );
  const persistedFactor = line.conversionFactor && line.conversionFactor > 0
    ? line.conversionFactor
    : 1;
  const conversionFactor = selectedPackaging?.baseQuantity ?? persistedFactor;
  const baseUom = ctx?.baseUomCode || line.baseUomCode || UNKNOWN;
  const presentationUom =
    selectedPackaging?.uomCode || line.uomCode || ctx?.baseUomCode || UNKNOWN;
  const quantityInBase =
    selectedPackaging || line.quantityInBaseUom === undefined
      ? quantity * conversionFactor
      : line.quantityInBaseUom;
  const hasPresentation = !!line.packagingLevelId;
  const presentationLabel =
    selectedPackaging?.name ??
    (hasPresentation && conversionFactor > 1
      ? `${presentationUom} x ${formatMoney(conversionFactor, decimals.quantity)}`
      : presentationUom);
  // Palabra genérica para expresar cantidades en unidad base al usuario —
  // nunca el código técnico crudo (p. ej. "04"/"19"): el DTO de contexto no
  // trae un nombre legible de UOM, solo el código, así que no se muestra
  // el código suelto (ver purchases.lines.baseUnitGeneric).
  const baseUnitWord =
    t?.("purchases.lines.baseUnitGeneric", "unidades") ?? "unidades";
  // Solo se arma cuando hay una presentación real con nombre legible y un
  // factor de conversión mayor a 1 — si no hay datos legibles, se deja
  // vacío en vez de inventar una equivalencia con el código técnico.
  const equivalenceDetail =
    hasItem && selectedPackaging && conversionFactor > 1
      ? (t?.("purchases.lines.equivalenceDetail", {
          package: selectedPackaging.name,
          qty: formatMoney(conversionFactor, decimals.quantity),
          unit: baseUnitWord,
        }) ??
        `1 ${selectedPackaging.name} = ${formatMoney(conversionFactor, decimals.quantity)} ${baseUnitWord}`)
      : "";
  const baseUnitCost =
    quantityInBase > 0 ? (quantity * unitPrice) / quantityInBase : 0;
  const marginPctValue = hasContext ? calcMarginPercent(unitPrice, pvp) : 0;

  const referenceCost =
    hasContext && (ctx?.lastPurchaseCost ?? 0) > 0
      ? ctx!.lastPurchaseCost
      : averageCost;
  const referenceLabel =
    hasContext && (ctx?.lastPurchaseCost ?? 0) > 0
      ? t?.("purchases.lines.costReferenceLast", "último costo") ??
        "último costo"
      : t?.("purchases.lines.costReferenceAverage", "costo promedio") ??
        "costo promedio";
  const deviationRatio =
    hasContext && referenceCost > 0 && baseUnitCost > 0
      ? Math.abs(baseUnitCost - referenceCost) / referenceCost
      : 0;
  const showDeviationAlert = hasContext && deviationRatio > 0.5;
  const deviationPercent = formatMoney(
    deviationRatio * 100,
    decimals.percentage,
  );
  const deviationLabel = showDeviationAlert
    ? baseUnitCost > referenceCost
      ? t?.("purchases.lines.baseCostDeviationHigh", {
          percent: deviationPercent,
          reference: referenceLabel,
        }) ??
        `Costo base ${deviationPercent}% sobre ${referenceLabel}. Revise presentación/factor.`
      : t?.("purchases.lines.baseCostDeviationLow", {
          percent: deviationPercent,
          reference: referenceLabel,
        }) ??
        `Costo base ${deviationPercent}% bajo ${referenceLabel}. Revise presentación/factor.`
    : "";

  const missingRequiredData = hasItem && !isLoading && !line.vatCode;

  return {
    xml: {
      hasOrigin,
      supplierCode: line.xmlSupplierCode || UNKNOWN,
      supplierAuxCode: line.xmlSupplierAuxCode || UNKNOWN,
      hasSupplierAuxCode: !!line.xmlSupplierAuxCode,
      description: line.description || UNKNOWN,
      quantity: formatMoney(quantity, decimals.quantity),
      unitPrice: formatMoneyWithSymbol(unitPrice, decimals.purchaseUnitPrice),
      discount: hasOrigin
        ? formatMoneyWithSymbol(line.xmlDiscount ?? 0, decimals.totalAmount)
        : UNKNOWN,
      vatPercentage: hasOrigin
        ? formatMoney(line.xmlVatPercentage ?? 0, decimals.percentage)
        : UNKNOWN,
      taxValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTaxValue ?? 0, decimals.totalAmount)
        : UNKNOWN,
      totalLine: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTotalLine ?? 0, decimals.totalAmount)
        : UNKNOWN,
    },
    item: {
      hasItem,
      isLoading,
      sku:
        (hasItem && (ctx?.sku || line.description?.split(" — ")[0])) || UNKNOWN,
      name: (hasItem && displayName) || UNKNOWN,
      uom: presentationUom,
      baseUom,
      matchStatus: line.itemMatchStatus ?? null,
    },
    inventory: {
      presentation:
        selectedPackaging?.name ??
        (conversionFactor > 1
          ? `${presentationUom} x ${formatMoney(conversionFactor, decimals.quantity)}`
          : presentationUom),
      hasPresentation,
      presentationLabel,
      conversionDetail: hasItem
        ? `${formatMoney(quantity, decimals.quantity)} ${presentationUom} -> ${formatMoney(quantityInBase, decimals.quantity)} ${baseUom}`
        : UNKNOWN,
      equivalenceDetail,
      baseQuantity: hasItem
        ? `${formatMoney(quantityInBase, decimals.quantity)} ${baseUnitWord}`
        : UNKNOWN,
      baseUnitCost:
        hasItem && quantityInBase > 0
          ? formatMoneyWithSymbol(baseUnitCost, decimals.purchaseUnitPrice)
          : UNKNOWN,
    },
    commercial: {
      hasContext,
      stock: {
        current: hasContext
          ? formatMoney(currentStock, decimals.quantity)
          : UNKNOWN,
        available: hasContext
          ? formatMoney(ctx!.availableStock, decimals.quantity)
          : UNKNOWN,
        reserved: hasContext
          ? formatMoney(ctx!.reservedStock, decimals.quantity)
          : UNKNOWN,
        statusLabel: hasContext
          ? currentStock <= 0
            ? "Crítico"
            : "OK"
          : UNKNOWN,
        isCritical: hasContext && currentStock <= 0,
      },
      costs: {
        average: hasContext
          ? formatMoneyWithSymbol(averageCost, decimals.purchaseUnitPrice)
          : UNKNOWN,
        last: hasContext
          ? formatMoneyWithSymbol(
              ctx!.lastPurchaseCost,
              decimals.purchaseUnitPrice,
            )
          : UNKNOWN,
        showDeviationAlert,
        deviationLabel,
      },
      profitability: {
        pvp: hasContext
          ? formatMoneyWithSymbol(pvp, decimals.salesUnitPrice)
          : UNKNOWN,
        marginPct: hasContext
          ? formatMoney(marginPctValue, decimals.percentage)
          : UNKNOWN,
        marginPctValue,
        maxDiscountPercent: hasContext
          ? formatMoney(ctx!.maxDiscountPercent, decimals.percentage)
          : UNKNOWN,
      },
    },
    status: buildLineStatus({
      hasItem,
      isLoading,
      missingRequiredData,
      hasOrigin,
      hasPresentation,
      presentationLabel,
    }),
  };
}

function buildLineStatus(input: {
  hasItem: boolean;
  isLoading: boolean;
  missingRequiredData: boolean;
  hasOrigin: boolean;
  hasPresentation: boolean;
  presentationLabel: string;
}): PurchaseLinePresentationVM["status"] {
  if (!input.hasItem) {
    return { icon: "🟡", label: "Sin ítem vinculado", tone: "warning" };
  }
  if (input.isLoading) {
    return { icon: "🟡", label: "Contexto cargando", tone: "warning" };
  }
  if (input.missingRequiredData) {
    return { icon: "🔴", label: "Información incompleta", tone: "danger" };
  }
  if (input.hasPresentation) {
    return {
      icon: "🟢",
      label: `Ítem + ${input.presentationLabel}`,
      tone: "success",
    };
  }
  if (input.hasOrigin) {
    return {
      icon: "🟡",
      label: "Ítem vinculado sin presentación",
      tone: "warning",
    };
  }
  return { icon: "🟢", label: "Ítem vinculado", tone: "success" };
}
