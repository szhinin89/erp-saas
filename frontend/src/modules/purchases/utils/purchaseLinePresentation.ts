import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { calcMarginPercent } from "../../../lib/margin";
import { lineNet, calcLineTax } from "./purchaseCalc";

/** Dato desconocido — nunca se confunde con un valor real (p. ej. stock 0). */
const UNKNOWN = "—";

// PURCHASE-P2-P3-CLEANUP-CLOSE-01 — mismo criterio semántico que el guard equivalente en backend
// (ConfirmPurchaseUseCases.cs: SuspiciousPurchaseMarginThresholdPct), sin compartir archivo físico
// entre frontend/backend — solo el nombre y el valor deben mantenerse alineados.
const SUSPICIOUS_PURCHASE_MARGIN_THRESHOLD_PCT = -50;
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
    taxableBase: string;
    iceValue: string;
    irbpnrValue: string;
    hasIrbpnr: boolean;
    /** PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — detallesAdicionales/detAdicional, solo lectura. */
    additionalFields: { name: string; value: string }[];
    hasAdditionalFields: boolean;
  };
  /**
   * PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — cabecera editable de la línea (lo que se
   * guardará como draft): SIEMPRE recalculada desde quantity/unitPrice/discountPct/vatCode/iceCode
   * actuales — nunca desde `xml.*` de arriba (ese bloque es el snapshot documental, fijo). Editar
   * Cantidad/Costo en cabecera cambia `line.quantity`/`line.unitPrice`, y este bloque recalcula en
   * consecuencia en cada render — nunca queda pegado a un valor anterior.
   */
  editableHeader: {
    taxableBase: number;
    vatAmount: number;
    iceAmount: number;
    /**
     * Única excepción: a diferencia de IVA/ICE (porcentuales sobre la base imponible), no hay
     * tarifa/fórmula en el frontend para recalcular IRBPNR proporcionalmente a un cambio de
     * cantidad/costo — es un monto específico del catálogo SRI que solo el backend conoce. Se
     * conserva el monto documental del XML (`xml.irbpnrValue`) para no inventar un recálculo
     * incorrecto; queda desactualizado si el usuario edita cantidad/costo de una línea con IRBPNR
     * hasta que exista una tarifa IRBPNR expuesta al frontend.
     */
    irbpnrAmount: number;
    total: number;
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
    /** Valor numérico crudo de baseQuantity — usado por PurchaseLineCard para poblar/reversar
     * el input de Cantidad de la cabecera cuando hay presentación vinculada (ver
     * PURCHASE-LINE-HEADER-INVENTORY-MODE-01). */
    baseQuantityValue: number;
    /** Factor de conversión activo (presentación seleccionada o el persistido) — mismo valor
     * usado internamente para calcular baseQuantityValue; expuesto para reversar la edición. */
    conversionFactorValue: number;
    baseUnitCost: string;
    /** Valor numérico crudo de baseUnitCost — usado por PurchaseLineCard para poblar el input de
     * Costo de la cabecera en modo inventario (solo lectura, ver
     * PURCHASE-LINE-HEADER-COST-BASE-MODE-01). Nunca se reversa a unitPrice: editar unitPrice se
     * hace siempre en unidad de presentación/factura, nunca desde este valor derivado. */
    baseUnitCostValue: number;
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
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): PurchaseLinePresentationVM {
  const decimals = getDecimalConfig();
  const ctx = line.context;
  const hasItem = !!line.itemId;
  const isLoading = !!line._contextLoading;
  const hasContext = hasItem && !isLoading && !!ctx;
  const hasOrigin = !!line.purchaseReceptionLineId || !!line.xmlSupplierCode;

  // PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — mismas funciones que ya usaban las
  // líneas manuales (purchaseCalc.ts), aplicadas también a líneas con origen XML: antes, una línea
  // con `hasOrigin=true` siempre mostraba `xmlTaxValue`/`xmlTotalLine` (el snapshot documental)
  // aunque el usuario ya hubiera editado quantity/unitPrice en cabecera — Base imp. sí se
  // recalculaba (`lineNet`) pero IVA/Total quedaban pegados al valor original del XML.
  const editableTaxableBase = lineNet(line);
  const editableTax = calcLineTax(line, vatRates, iceRates);
  const editableIrbpnrAmount = hasOrigin ? (line.xmlIrbpnrAmount ?? 0) : 0;
  const editableTotal =
    editableTaxableBase + editableTax.vat + editableTax.ice + editableIrbpnrAmount;

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
  // PURCHASE-LINE-HEADER-INVENTORY-MODE-01 — el costo real unitario base debe descontar el
  // descuento de línea (y sumar flete/otros gastos ya asignados, si existen) antes de dividir
  // entre la cantidad en unidad base — nunca el bruto sin descuento. Misma fórmula que
  // `buildCostDistributionInputFromFormLines` (purchaseCalc.ts) para una sola línea.
  const grossLineSubtotal = quantity * unitPrice;
  const discountAmount = grossLineSubtotal * ((line.discountPct ?? 0) / 100);
  const netLineSubtotal = grossLineSubtotal - discountAmount;
  const freightAllocated = line.freightAllocated ?? 0;
  const otherCostsAllocated = line.otherCostsAllocated ?? 0;
  const baseUnitCost =
    quantityInBase > 0
      ? (netLineSubtotal + freightAllocated + otherCostsAllocated) / quantityInBase
      : 0;
  // PURCHASE-PRESENTATION-MARGIN-AUDIT-01 — el margen siempre debe comparar valores en la misma
  // unidad: pvp es por unidad base, así que el costo comparado debe ser baseUnitCost (por unidad
  // base), nunca unitPrice (costo por unidad de presentación — p. ej. por SIXPACK). Comparar
  // unitPrice contra pvp mezcla unidades y produce márgenes absurdos (ej. -447%) en cualquier
  // línea con presentación (conversionFactor > 1).
  const marginPctValue = hasContext ? calcMarginPercent(baseUnitCost, pvp) : 0;

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
      // PURCHASE-LINE-PACKAGING-XML-SNAPSHOT-IMMUTABLE-01 — Cantidad/Precio del snapshot
      // documental, NUNCA los editables (`quantity`/`unitPrice` de arriba cambian con
      // Cantidad/Costo de cabecera). Fallback a los editables solo cuando no hay snapshot
      // (`xmlQuantity`/`xmlUnitPrice` undefined): línea manual sin origen XML, o una compra ya
      // guardada reabierta para editar (`loadForEdit` no repuebla el snapshot XML) — en ambos
      // casos no existe un original documental distinto que preservar.
      quantity: formatMoney(
        line.xmlQuantity ?? quantity,
        decimals.quantity,
      ),
      unitPrice: formatMoneyWithSymbol(
        line.xmlUnitPrice ?? unitPrice,
        decimals.purchaseUnitPrice,
      ),
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
      taxableBase: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTaxableBase ?? 0, decimals.totalAmount)
        : UNKNOWN,
      iceValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlIceAmount ?? 0, decimals.totalAmount)
        : UNKNOWN,
      irbpnrValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlIrbpnrAmount ?? 0, decimals.totalAmount)
        : UNKNOWN,
      hasIrbpnr: hasOrigin && (line.xmlIrbpnrAmount ?? 0) > 0,
      additionalFields: line.xmlAdditionalFields ?? [],
      hasAdditionalFields: (line.xmlAdditionalFields?.length ?? 0) > 0,
    },
    editableHeader: {
      taxableBase: editableTaxableBase,
      vatAmount: editableTax.vat,
      iceAmount: editableTax.ice,
      irbpnrAmount: editableIrbpnrAmount,
      total: editableTotal,
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
      baseQuantityValue: quantityInBase,
      conversionFactorValue: conversionFactor,
      baseUnitCost:
        hasItem && quantityInBase > 0
          ? formatMoneyWithSymbol(baseUnitCost, decimals.purchaseUnitPrice)
          : UNKNOWN,
      baseUnitCostValue: baseUnitCost,
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
            ? (t?.("purchases.lines.stockCritical", "Crítico") ?? "Crítico")
            : (t?.("purchases.lines.stockOk", "OK") ?? "OK")
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
    status: buildLineStatus(
      {
        hasItem,
        isLoading,
        missingRequiredData,
        hasOrigin,
        hasPresentation,
        presentationLabel,
      },
      t,
    ),
  };
}

function buildLineStatus(
  input: {
    hasItem: boolean;
    isLoading: boolean;
    missingRequiredData: boolean;
    hasOrigin: boolean;
    hasPresentation: boolean;
    presentationLabel: string;
  },
  t?: TFunction,
): PurchaseLinePresentationVM["status"] {
  if (!input.hasItem) {
    return {
      icon: "🟡",
      label: t?.("purchases.lines.statusNoItem", "Sin ítem vinculado") ?? "Sin ítem vinculado",
      tone: "warning",
    };
  }
  if (input.isLoading) {
    return {
      icon: "🟡",
      label: t?.("purchases.lines.statusLoading", "Contexto cargando") ?? "Contexto cargando",
      tone: "warning",
    };
  }
  if (input.missingRequiredData) {
    return {
      icon: "🔴",
      label: t?.("purchases.lines.statusIncomplete", "Información incompleta") ?? "Información incompleta",
      tone: "danger",
    };
  }
  if (input.hasPresentation) {
    return {
      icon: "🟢",
      label:
        t?.("purchases.lines.statusItemWithPresentation", {
          presentation: input.presentationLabel,
        }) ?? `Ítem + ${input.presentationLabel}`,
      tone: "success",
    };
  }
  if (input.hasOrigin) {
    return {
      icon: "🟡",
      label:
        t?.("purchases.lines.statusItemNoPresentation", "Ítem vinculado sin presentación") ??
        "Ítem vinculado sin presentación",
      tone: "warning",
    };
  }
  return {
    icon: "🟢",
    label: t?.("purchases.lines.statusItemLinked", "Ítem vinculado") ?? "Ítem vinculado",
    tone: "success",
  };
}

/**
 * PURCHASE-LINE-HEADER-BASE-QTY-COST-EDIT-01 — fórmula inversa de `baseUnitCost`
 * (ver arriba): dado un nuevo costo real unitario base tecleado en la cabecera
 * (línea con presentación vinculada), despeja el `unitPrice` de factura/presentación
 * que hace que la línea siga cuadrando fiscalmente. Único punto de esta fórmula —
 * `PurchaseLineCard` la consume, no la reimplementa. Devuelve `null` cuando el
 * cálculo no es seguro (cantidades en 0, descuento >= 100% o resultado negativo) —
 * el llamador debe entonces no aplicar el cambio, nunca inventar un valor.
 */
export function computeUnitPriceFromBaseUnitCost(params: {
  newBaseUnitCost: number;
  quantity: number;
  quantityInBaseUom: number;
  discountPct?: number;
  freightAllocated?: number;
  otherCostsAllocated?: number;
}): number | null {
  const { newBaseUnitCost, quantity, quantityInBaseUom } = params;
  const discountPct = params.discountPct ?? 0;
  const freightAllocated = params.freightAllocated ?? 0;
  const otherCostsAllocated = params.otherCostsAllocated ?? 0;
  if (
    !(quantity > 0) ||
    !(quantityInBaseUom > 0) ||
    !(discountPct < 100) ||
    !(newBaseUnitCost >= 0)
  ) {
    return null;
  }
  const denom = quantity * (1 - discountPct / 100);
  if (!(denom > 0)) return null;
  const unitPrice =
    (newBaseUnitCost * quantityInBaseUom - freightAllocated - otherCostsAllocated) /
    denom;
  if (!Number.isFinite(unitPrice) || unitPrice < 0) return null;
  return unitPrice;
}

/**
 * PURCHASE-LINE-PACKAGING-SUSPICIOUS-COST-GUARD-01 — detecta una presentación evidentemente
 * incorrecta (p. ej. XML dice "UNIDAD X1" cuando en realidad el proveedor entregó "SIXPACK X6")
 * ANTES de guardar el draft: ese error de presentación diluye/infla `baseUnitCost` (ver
 * PURCHASE-PRESENTATION-MARGIN-AUDIT-01 arriba) y produce un margen absurdo (< -50%) que, si se
 * confirma la compra, contamina Kardex/costo promedio. Reutiliza EXACTAMENTE
 * `buildPurchaseLinePresentation` (mismos `baseUnitCostValue`/`marginPctValue` que ya pinta la
 * cabecera) — nunca reimplementa la fórmula de costo/margen en paralelo.
 *
 * Deliberadamente NO bloquea: sin producto vinculado, sin precio de venta (pvp) conocido, en
 * servicios/no inventariables (`tracksStock !== true`), o con margen negativo leve (>= -50%) —
 * esos son negocios reales de bajo margen, no errores de presentación.
 */
export function buildSuspiciousPackagingCostWarning(
  line: PurchaseLineFormValues,
  t?: TFunction,
): { blocking: true; message: string } | null {
  const ctx = line.context;
  if (!line.itemId || !ctx || ctx.tracksStock !== true) return null;
  const salePrice = ctx.pvp ?? 0;
  if (!(salePrice > 0)) return null;

  const vm = buildPurchaseLinePresentation(line, t);
  const baseUnitCostValue = vm.inventory.baseUnitCostValue;
  if (!(baseUnitCostValue > 0)) return null;
  const marginPctValue = vm.commercial.profitability.marginPctValue;
  if (!(marginPctValue < SUSPICIOUS_PURCHASE_MARGIN_THRESHOLD_PCT)) return null;

  const description = vm.xml.description !== "—" ? vm.xml.description : vm.item.name;
  const message =
    t?.("purchases.lineReadiness.suspiciousPackagingCostDetail", {
      description,
      presentation: vm.inventory.presentationLabel,
      baseUnitCost: vm.inventory.baseUnitCost,
      salePrice: vm.commercial.profitability.pvp,
      marginPct: `${vm.commercial.profitability.marginPct}%`,
    }) ??
    `Presentación sospechosa en ${description}. La presentación seleccionada (${vm.inventory.presentationLabel}) produce un costo unitario base de ${vm.inventory.baseUnitCost} frente a un precio de venta de ${vm.commercial.profitability.pvp} (${vm.commercial.profitability.marginPct}%). Revise si corresponde usar unidad, pack, paca o caja.`;

  return { blocking: true, message };
}

/**
 * PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — complemento no bloqueante de
 * `buildSuspiciousPackagingCostWarning`: cuando un producto inventariable con costo base ya
 * calculado no tiene precio de venta (pvp) configurado, el guard de margen extremo no puede
 * evaluarse (nunca se ejecuta, por el `return null` temprano en `salePrice > 0`). Este aviso
 * informa al usuario de esa laguna sin impedir guardar el borrador ni confirmar la compra —
 * mutuamente excluyente con el warning bloqueante de arriba (uno exige pvp > 0, este exige
 * pvp <= 0/ausente).
 */
export function buildMissingSalePriceForMarginWarning(
  line: PurchaseLineFormValues,
  t?: TFunction,
): { blocking: false; message: string } | null {
  const ctx = line.context;
  if (!line.itemId || !ctx || ctx.tracksStock !== true) return null;
  const salePrice = ctx.pvp ?? 0;
  if (salePrice > 0) return null;

  const vm = buildPurchaseLinePresentation(line, t);
  const baseUnitCostValue = vm.inventory.baseUnitCostValue;
  if (!(baseUnitCostValue > 0)) return null;

  const description = vm.xml.description !== "—" ? vm.xml.description : vm.item.name;
  const message =
    t?.("purchases.lineReadiness.missingSalePriceForMarginDetail", { description }) ??
    `No se puede calcular margen para '${description}' porque el producto no tiene precio de venta configurado. Revise el precio de venta para validar si el costo y la presentación son correctos.`;

  return { blocking: false, message };
}

/**
 * PURCHASE-LINE-HEADER-PACKAGING-CHANGE-SYNC-01 — key de remount para los inputs no controlados
 * (`defaultValue`) de Cantidad/Costo de la cabecera de línea. `vm.inventory.hasPresentation` por sí
 * solo NO alcanza: es `true` tanto para "SIXPACK X6" como para "UNIDAD X1" (ambas son presentaciones
 * reales, cada una con su propio `packagingLevelId`), así que cambiar de una a otra no cambiaba la
 * key — React reusaba el mismo nodo DOM y el `defaultValue` nunca se reaplicaba, dejando Cantidad/
 * Costo de cabecera con el valor de la presentación anterior mientras Margen (que sí lee del
 * view-model en cada render) ya reflejaba la nueva. Incluir `packagingLevelId` + el factor de
 * conversión efectivo fuerza el remount exactamente cuando la presentación cambia — nunca en cada
 * tecla/blur de edición manual, porque esos campos no tocan ninguno de los dos valores.
 */
export function headerInputKey(
  field: "qty" | "cost",
  vm: PurchaseLinePresentationVM,
  packagingLevelId: string | null | undefined,
): string {
  const mode = vm.inventory.hasPresentation ? "base" : "invoice";
  return `${field}-${mode}-${packagingLevelId ?? "none"}-${vm.inventory.conversionFactorValue}`;
}
