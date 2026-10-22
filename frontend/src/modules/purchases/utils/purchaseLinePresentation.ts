import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";
import {
  resolvePrecisionDecimals,
  type PrecisionPolicy,
} from "../../../lib/config/precisionPolicy.config";
import { formatMoney, formatMoneyWithSymbol, roundToDecimals as roundTo } from "../../../lib/sanitizers";
import { calcMarginPercent } from "../../../lib/margin";
import { lineNet, resolveLineTaxes } from "./purchaseCalc";

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
   * guardará como draft): recalculada desde quantity/unitPrice/discountPct/vatCode/iceCode actuales
   * en cada render. PURCHASES-HARDENING-P0 (B4-B6): mientras la base imponible coincida con la del
   * XML, IVA/ICE muestran el monto documental (el mismo que persiste el backend); al editar
   * Cantidad/Costo la base cambia y se recalculan por tarifa — nunca quedan pegados a un valor anterior.
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
    conversionFactorLabel: string;
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
  /**
   * FASE 5 PURCHASE-UX — qué se registrará realmente en inventario. Cantidad y Costo de la
   * cabecera SIEMPRE están en unidad base; esto lo explicita en una frase y deriva la
   * presentación/origen como información, nunca como el significado de los campos.
   */
  registration: {
    /** false sin producto vinculado (nada que registrar en inventario todavía). */
    show: boolean;
    /** Cantidad a registrar autoritativa (unidad base) — ver resolveLineBaseQuantity. */
    baseQuantityValue: number;
    /** "Se registrarán 24 unidades a $2.000000 c/u". */
    sentence: string;
    /** Aclaración del costo cuando incluye descuento y/o flete/gastos asignados; "" si no aplica. */
    costNote: string;
    /** "= 2 PACA (1 PACA = 12 unidades) · $24.00 por PACA" ("≈" si no es exacto); "" sin presentación. */
    presentationDetail: string;
    /** Línea manual cuya cantidad/costo no es exacto en la presentación: se guardará en unidad base. */
    savedInBaseUnitNote: string;
    /** Línea XML: resumen del documento del proveedor; "" sin origen documental. */
    originDetail: string;
    /** Línea XML cuya cantidad o base imponible final difiere del documento del proveedor. */
    originDiffers: boolean;
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
  // ZH-DESIGN-SYSTEM-PRECISION-06 — utilidad pura: la policy la entrega el caller React
  // (`usePrecisionPolicy`) y cada escala se resuelve con `resolvePrecisionDecimals`.
  policy: PrecisionPolicy,
  t?: TFunction,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): PurchaseLinePresentationVM {
  const decimals = {
    conversionFactorDecimals: resolvePrecisionDecimals(policy, "conversionFactor"),
    percentageDecimals: resolvePrecisionDecimals(policy, "percentage"),
    fiscalPercentageDecimals: resolvePrecisionDecimals(policy, "fiscalPercentage"),
    quantityDecimals: resolvePrecisionDecimals(policy, "quantity"),
    purchaseUnitPriceDecimals: resolvePrecisionDecimals(policy, "purchaseUnitPrice"),
    moneyDecimals: resolvePrecisionDecimals(policy, "money"),
    taxDecimals: resolvePrecisionDecimals(policy, "tax"),
    unitCostDecimals: resolvePrecisionDecimals(policy, "unitCost"),
    averageCostDecimals: resolvePrecisionDecimals(policy, "averageCost"),
    salesUnitPriceDecimals: resolvePrecisionDecimals(policy, "salesUnitPrice"),
  };
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
  // PURCHASES-HARDENING-P0 (B4-B6): `resolveLineTaxes` — el monto documental del XML prevalece
  // (igual que el backend) mientras la base editable siga siendo la del comprobante.
  const editableTaxableBase = lineNet(line);
  const editableTax = resolveLineTaxes(line, vatRates, iceRates);
  const editableIrbpnrAmount = hasOrigin ? editableTax.irbpnr : 0;
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
  // FASE 5 PURCHASE-UX — cantidad a registrar autoritativa (sin round-trip por la presentación).
  const quantityInBase = resolveLineBaseQuantity(line, conversionFactor);
  const hasPresentation = !!line.packagingLevelId;
  // The DTO supplies UOM codes, not readable names. Never use them as labels.
  const presentationLabel = selectedPackaging?.name?.trim() ||
    (t?.("purchases.lines.presentationNameUnavailable", "Nombre de presentación no disponible") ??
      "Nombre de presentación no disponible");
  // ERP-PRECISION-FRONTEND-06B1: el factor visible respeta conversionFactorDecimals (igual que
  // la equivalencia); el valor numérico crudo (conversionFactorValue) no cambia.
  const conversionFactorText = formatMoney(conversionFactor, decimals.conversionFactorDecimals);
  const conversionFactorLabel = conversionFactor === 1
    ? (t?.("purchases.lines.oneBaseUnit", "1 unidad") ?? "1 unidad")
    : (t?.("purchases.lines.unitsPerPresentation", { factor: conversionFactorText }) ??
      `${conversionFactorText} unidades por presentación`);
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
          qty: formatMoney(conversionFactor, decimals.conversionFactorDecimals),
          unit: baseUnitWord,
        }) ??
        `1 ${selectedPackaging.name} = ${formatMoney(conversionFactor, decimals.conversionFactorDecimals)} ${baseUnitWord}`)
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
    decimals.percentageDecimals,
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

  // FASE 5 PURCHASE-UX — lo que se registrará es la cantidad/costo autoritativos en unidad base;
  // la presentación y el documento del proveedor son información derivada.
  const registeredBaseText = formatMoney(quantityInBase, decimals.quantityDecimals);
  const baseUnitCostText = formatMoneyWithSymbol(baseUnitCost, decimals.unitCostDecimals);
  const registrationSentence =
    t?.("purchases.lines.registrationSentence", {
      qty: registeredBaseText,
      unit: baseUnitWord,
      cost: baseUnitCostText,
    }) ?? `Se registrarán ${registeredBaseText} ${baseUnitWord} a ${baseUnitCostText} c/u`;
  const hasDiscount = (line.discountPct ?? 0) > 0;
  const hasAllocatedCosts = freightAllocated + otherCostsAllocated > 0;
  let costNote = "";
  if (hasDiscount && hasAllocatedCosts)
    costNote =
      t?.(
        "purchases.lines.costNoteDiscountAndAllocated",
        "Costo neto de descuento, incluye flete/gastos asignados.",
      ) ?? "Costo neto de descuento, incluye flete/gastos asignados.";
  else if (hasDiscount)
    costNote =
      t?.("purchases.lines.costNoteDiscount", "Costo neto de descuento.") ??
      "Costo neto de descuento.";
  else if (hasAllocatedCosts)
    costNote =
      t?.("purchases.lines.costNoteAllocated", "Incluye flete/gastos asignados.") ??
      "Incluye flete/gastos asignados.";
  // Manual o XML editada: la presentación se guarda solo si expresa EXACTO lo a registrar; si no,
  // la línea se guarda en unidad base y la equivalencia queda solo informativa (≈).
  const persistence = resolveLinePersistence(
    { ...line, conversionFactor },
    decimals.purchaseUnitPriceDecimals,
  );
  const savedInBaseUnit = persistence.mode === "base";
  const presentationQuantityValue = quantityInBase / conversionFactor;
  const presentationQuantityText = formatMoney(presentationQuantityValue, decimals.quantityDecimals);
  const presentationQuantityIsExact =
    Math.abs(roundTo(presentationQuantityValue, decimals.quantityDecimals) - presentationQuantityValue) <=
    1e-9;
  const presentationCostText = formatMoneyWithSymbol(
    baseUnitCost * conversionFactor,
    decimals.unitCostDecimals,
  );
  const presentationDetail =
    hasItem && selectedPackaging && conversionFactor !== 1
      ? (t?.(
          presentationQuantityIsExact && !savedInBaseUnit
            ? "purchases.lines.registrationPresentationDetail"
            : "purchases.lines.registrationPresentationApprox",
          {
            qty: presentationQuantityText,
            presentation: selectedPackaging.name,
            equivalence: equivalenceDetail,
            cost: presentationCostText,
          },
        ) ??
        `${presentationQuantityIsExact && !savedInBaseUnit ? "=" : "≈"} ${presentationQuantityText} ${selectedPackaging.name} (${equivalenceDetail}) · ${presentationCostText} por ${selectedPackaging.name}`)
      : "";
  const savedInBaseUnitNote =
    hasItem && selectedPackaging && savedInBaseUnit
      ? (t?.("purchases.lines.registrationSavedInBaseUnit", {
          presentation: selectedPackaging.name,
        }) ??
        `No es una cantidad exacta de ${selectedPackaging.name}: la línea se guardará en unidad base, sin alterar lo que se registrará.`)
      : "";
  // Origen del proveedor: snapshot XML intacto, interpretado con la presentación de ORIGEN (no la
  // operativa: una XML editada puede persistirse en unidad base).
  const hasOriginSnapshot =
    hasOrigin && typeof line.xmlQuantity === "number" && typeof line.xmlUnitPrice === "number";
  const originQuantity = line.xmlQuantity ?? 0;
  const originUnitPrice = line.xmlUnitPrice ?? 0;
  const originFactor = resolveXmlOriginFactor(line, conversionFactor);
  const originPresentationName =
    ctx?.packagingLevels?.find(
      (p) => p.id === (line.xmlOriginPackagingLevelId ?? line.packagingLevelId),
    )?.name ?? "";
  // Base imponible IVA del XML (puede incluir ICE): solo informativa, nunca se compara.
  const originTaxableBase =
    line.xmlTaxableBase ?? originQuantity * originUnitPrice - (line.xmlDiscount ?? 0);
  // Subtotal neto de producto del XML (sin impuestos): es lo comparable con la línea editable.
  const originProductNet = roundTo(
    (line.xmlLineSubtotal ?? originQuantity * originUnitPrice) - (line.xmlDiscount ?? 0),
    2,
  );
  const originDiffers =
    hasOriginSnapshot &&
    (isXmlLineEditedFromOrigin(line, conversionFactor) ||
      Math.abs(roundTo(editableTaxableBase, 2) - originProductNet) > 0.005);
  const originBaseText = formatMoney(originQuantity * originFactor, decimals.quantityDecimals);
  const originQuantityText = [
    formatMoney(originQuantity, decimals.quantityDecimals),
    originFactor !== 1 ? originPresentationName : "",
  ]
    .filter(Boolean)
    .join(" ");
  const originDetail = hasOriginSnapshot
    ? (t?.("purchases.lines.registrationOrigin", {
        qty: originQuantityText,
        price: formatMoneyWithSymbol(originUnitPrice, decimals.purchaseUnitPriceDecimals),
        base: formatMoneyWithSymbol(originTaxableBase, decimals.moneyDecimals),
        baseQty: originBaseText,
        unit: baseUnitWord,
      }) ??
      `Documento del proveedor: ${originQuantityText} × ${formatMoneyWithSymbol(originUnitPrice, decimals.purchaseUnitPriceDecimals)} (base imp. ${formatMoneyWithSymbol(originTaxableBase, decimals.moneyDecimals)}) = ${originBaseText} ${baseUnitWord}.`)
    : "";

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
        decimals.quantityDecimals,
      ),
      unitPrice: formatMoneyWithSymbol(
        line.xmlUnitPrice ?? unitPrice,
        decimals.purchaseUnitPriceDecimals,
      ),
      discount: hasOrigin
        ? formatMoneyWithSymbol(line.xmlDiscount ?? 0, decimals.moneyDecimals)
        : UNKNOWN,
      vatPercentage: hasOrigin
        ? formatMoney(line.xmlVatPercentage ?? 0, decimals.fiscalPercentageDecimals)
        : UNKNOWN,
      taxValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTaxValue ?? 0, decimals.taxDecimals)
        : UNKNOWN,
      totalLine: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTotalLine ?? 0, decimals.moneyDecimals)
        : UNKNOWN,
      taxableBase: hasOrigin
        ? formatMoneyWithSymbol(line.xmlTaxableBase ?? 0, decimals.moneyDecimals)
        : UNKNOWN,
      iceValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlIceAmount ?? 0, decimals.taxDecimals)
        : UNKNOWN,
      irbpnrValue: hasOrigin
        ? formatMoneyWithSymbol(line.xmlIrbpnrAmount ?? 0, decimals.taxDecimals)
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
      presentation: presentationLabel,
      hasPresentation,
      presentationLabel,
      conversionFactorLabel: hasItem ? conversionFactorLabel : UNKNOWN,
      conversionDetail: hasItem
        ? `${formatMoney(quantity, decimals.quantityDecimals)} ${presentationUom} -> ${formatMoney(quantityInBase, decimals.quantityDecimals)} ${baseUom}`
        : UNKNOWN,
      equivalenceDetail,
      baseQuantity: hasItem
        ? `${formatMoney(quantityInBase, decimals.quantityDecimals)} ${baseUnitWord}`
        : UNKNOWN,
      baseQuantityValue: quantityInBase,
      conversionFactorValue: conversionFactor,
      // ERP-PRECISION-FRONTEND-06B: baseUnitCost es un costo unitario derivado → unitCostDecimals
      // (ya no purchaseUnitPriceDecimals, que gobierna el precio de compra capturado).
      baseUnitCost:
        hasItem && quantityInBase > 0
          ? formatMoneyWithSymbol(baseUnitCost, decimals.unitCostDecimals)
          : UNKNOWN,
      baseUnitCostValue: baseUnitCost,
    },
    registration: {
      show: hasItem,
      baseQuantityValue: quantityInBase,
      sentence: registrationSentence,
      costNote,
      presentationDetail,
      savedInBaseUnitNote,
      originDetail,
      originDiffers,
    },
    commercial: {
      hasContext,
      stock: {
        current: hasContext
          ? formatMoney(currentStock, decimals.quantityDecimals)
          : UNKNOWN,
        available: hasContext
          ? formatMoney(ctx!.availableStock, decimals.quantityDecimals)
          : UNKNOWN,
        reserved: hasContext
          ? formatMoney(ctx!.reservedStock, decimals.quantityDecimals)
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
          ? formatMoneyWithSymbol(averageCost, decimals.averageCostDecimals)
          : UNKNOWN,
        last: hasContext
          ? formatMoneyWithSymbol(
              ctx!.lastPurchaseCost,
              decimals.unitCostDecimals,
            )
          : UNKNOWN,
        showDeviationAlert,
        deviationLabel,
      },
      profitability: {
        pvp: hasContext
          ? formatMoneyWithSymbol(pvp, decimals.salesUnitPriceDecimals)
          : UNKNOWN,
        marginPct: hasContext
          ? formatMoney(marginPctValue, decimals.percentageDecimals)
          : UNKNOWN,
        marginPctValue,
        maxDiscountPercent: hasContext
          ? formatMoney(ctx!.maxDiscountPercent, decimals.percentageDecimals)
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
 * servicios/no inventariables (`participatesInInventory !== true`), o con margen negativo leve (>= -50%) —
 * esos son negocios reales de bajo margen, no errores de presentación.
 */
export function buildSuspiciousPackagingCostWarning(
  line: PurchaseLineFormValues,
  policy: PrecisionPolicy,
  t?: TFunction,
): { blocking: true; message: string } | null {
  const ctx = line.context;
  if (!line.itemId || !ctx || ctx.participatesInInventory !== true) return null;
  const salePrice = ctx.pvp ?? 0;
  if (!(salePrice > 0)) return null;

  const vm = buildPurchaseLinePresentation(line, policy, t);
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
  policy: PrecisionPolicy,
  t?: TFunction,
): { blocking: false; message: string } | null {
  const ctx = line.context;
  if (!line.itemId || !ctx || ctx.participatesInInventory !== true) return null;
  const salePrice = ctx.pvp ?? 0;
  if (salePrice > 0) return null;

  const vm = buildPurchaseLinePresentation(line, policy, t);
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
 * tecla de edición manual (el estado de la línea solo cambia al blur).
 * El valor efectivo del propio campo también forma parte de la key: una acción que cambia la línea
 * sin cambiar presentación ni costo («Recalcular desde XML» tras 4 → 3) debe remontar el input,
 * o mostraría una cantidad distinta a la que se registrará.
 */
export function headerInputKey(
  field: "qty" | "cost",
  vm: PurchaseLinePresentationVM,
  packagingLevelId: string | null | undefined,
): string {
  const mode = vm.inventory.hasPresentation ? "base" : "invoice";
  // FASE 5 PURCHASE-UX — Costo de cabecera es el costo unitario base (incluye flete/gastos
  // asignados): al distribuir flete el input no controlado debe remontar con el valor nuevo.
  const fieldValue = field === "qty" ? `-${vm.inventory.baseQuantityValue}` : "";
  return `${field}-${mode}-${packagingLevelId ?? "none"}-${vm.inventory.conversionFactorValue}-${vm.inventory.baseUnitCostValue}${fieldValue}`;
}

/** Tolerancia de punto flotante para comparar valores ya en la misma escala. */
const FLOAT_EPS = 1e-9;
/** Escala con la que el backend persiste `quantity` (columna numeric(20,6)). */
const PERSISTED_QUANTITY_DECIMALS = 6;

type PackagingLine = Pick<
  PurchaseLineFormValues,
  | "quantity"
  | "unitPrice"
  | "quantityInBaseUom"
  | "baseUnitPrice"
  | "purchaseReceptionLineId"
  | "xmlSupplierCode"
  | "xmlQuantity"
  | "xmlUnitPrice"
  | "xmlOriginFactor"
  | "xmlOriginPackagingLevelId"
>;

const safeFactor = (factor: number | null | undefined) => (factor && factor > 0 ? factor : 1);
const sameValue = (a: number, b: number) => Math.abs(a - b) <= FLOAT_EPS * Math.max(1, Math.abs(b));

/** Línea con origen documental del proveedor (Recepción Electrónica) — mismo criterio que el VM. */
export function hasSupplierDocumentOrigin(line: PackagingLine): boolean {
  return !!line.purchaseReceptionLineId || !!line.xmlSupplierCode;
}

/**
 * FASE 5 PURCHASE-UX — cantidad a registrar (unidad base) AUTORITATIVA de la línea. Manda
 * `quantityInBaseUom` (lo que el usuario tecleó, sin round-trip por la presentación) mientras siga
 * alineado con quantity × factor; si quedó desalineado (otro flujo cambió quantity), se deriva.
 */
export function resolveLineBaseQuantity(line: PackagingLine, factor: number): number {
  const derived = (line.quantity ?? 0) * safeFactor(factor);
  const typed = line.quantityInBaseUom;
  if (typed === undefined) return derived;
  const tolerance = Math.max(FLOAT_EPS, safeFactor(factor) * 10 ** -PERSISTED_QUANTITY_DECIMALS);
  return Math.abs(typed - derived) <= tolerance ? typed : derived;
}

/**
 * FASE 5 PURCHASE-UX — precio unitario base (antes de descuento/flete) AUTORITATIVO de la línea:
 * `baseUnitPrice` si sigue alineado con unitPrice / factor; si no, se deriva. El precio por
 * presentación siempre se deriva de este valor, nunca al revés.
 */
export function resolveLineBaseUnitPrice(line: PackagingLine, factor: number): number {
  const derived = (line.unitPrice ?? 0) / safeFactor(factor);
  const typed = line.baseUnitPrice;
  if (typed === undefined) return derived;
  return sameValue(typed * safeFactor(factor), line.unitPrice ?? 0) ? typed : derived;
}

/**
 * FASE 5 PURCHASE-UX — presentación con la que se interpreta la unidad del documento del proveedor
 * (p. ej. "2 CAJA" = 2 × PACA x12). Se recuerda aparte de la presentación operativa de la línea
 * porque una XML editada puede persistirse en unidad base. Sin dato, la presentación actual.
 */
export function resolveXmlOriginFactor(line: PackagingLine, currentFactor: number): number {
  return safeFactor(line.xmlOriginFactor ?? currentFactor);
}

/**
 * FASE 5 PURCHASE-UX — presentación de origen al (re)abrir una línea XML persistida:
 * 1. el origen PERSISTIDO en la línea (`xmlOriginPackagingLevelId` + snapshot `xmlOriginFactor`) —
 *    nunca vuelve a depender de la recepción si existe;
 * Fallback (borradores anteriores sin origen persistido / línea recién creada desde Recepción):
 * 2. la presentación de la línea si es real (factor ≠ 1);
 * 3. la que la recepción resolvió para Proveedor + Código (`xmlSource`);
 * 4. la de la línea.
 */
export function resolveXmlOriginPresentation(
  persisted: {
    packagingLevelId?: string | null;
    conversionFactor?: number | null;
    xmlOriginPackagingLevelId?: string | null;
    xmlOriginFactor?: number | null;
  },
  receptionSource?: { packagingLevelId?: string | null; conversionFactor?: number | null } | null,
): { xmlOriginPackagingLevelId: string | undefined; xmlOriginFactor: number } {
  if (persisted.xmlOriginPackagingLevelId && persisted.xmlOriginFactor && persisted.xmlOriginFactor > 0) {
    return {
      xmlOriginPackagingLevelId: persisted.xmlOriginPackagingLevelId,
      xmlOriginFactor: persisted.xmlOriginFactor,
    };
  }
  if (persisted.packagingLevelId && safeFactor(persisted.conversionFactor) !== 1) {
    return {
      xmlOriginPackagingLevelId: persisted.packagingLevelId,
      xmlOriginFactor: safeFactor(persisted.conversionFactor),
    };
  }
  if (receptionSource?.packagingLevelId && safeFactor(receptionSource.conversionFactor) > 0) {
    return {
      xmlOriginPackagingLevelId: receptionSource.packagingLevelId,
      xmlOriginFactor: safeFactor(receptionSource.conversionFactor),
    };
  }
  return {
    xmlOriginPackagingLevelId: persisted.packagingLevelId ?? undefined,
    xmlOriginFactor: safeFactor(persisted.conversionFactor),
  };
}

/**
 * FASE 5 PURCHASE-UX — la línea XML ya no coincide con lo que mandó el proveedor: se compara en
 * UNIDAD BASE (cantidad y precio a registrar vs. documento × presentación de origen), así sigue
 * siendo correcto aunque la línea se haya persistido en unidad base. Sin snapshot, no hay origen.
 */
export function isXmlLineEditedFromOrigin(line: PackagingLine, currentFactor: number): boolean {
  if (
    !hasSupplierDocumentOrigin(line) ||
    typeof line.xmlQuantity !== "number" ||
    typeof line.xmlUnitPrice !== "number"
  )
    return false;
  const originFactor = resolveXmlOriginFactor(line, currentFactor);
  const quantityTolerance = Math.max(FLOAT_EPS, originFactor * 10 ** -PERSISTED_QUANTITY_DECIMALS);
  const quantityEdited =
    Math.abs(resolveLineBaseQuantity(line, currentFactor) - line.xmlQuantity * originFactor) >
    quantityTolerance;
  const priceEdited = !sameValue(
    resolveLineBaseUnitPrice(line, currentFactor),
    line.xmlUnitPrice / originFactor,
  );
  return quantityEdited || priceEdited;
}

export interface PackagingChangeResult {
  /** "document": se reinterpreta el documento del proveedor; "base": se conserva lo a registrar. */
  anchor: "document" | "base";
  quantity: number;
  unitPrice: number;
  quantityInBaseUom: number;
  baseUnitPrice: number | undefined;
  xmlOriginFactor: number | undefined;
  xmlOriginPackagingLevelId: string | undefined;
}

/**
 * FASE 5 PURCHASE-UX — valores de la línea al cambiar de presentación.
 * - Línea XML sin editar (`document`): la cantidad/costo ORIGINAL del proveedor se expresa en la
 *   presentación elegida → 2 cajas x12 = 24 unidades; la elegida pasa a ser la de origen.
 * - Línea manual, o XML ya editada (`base`): la cantidad y el precio a registrar se CONSERVAN
 *   exactos; la presentación solo se deriva (B / f, P × f) — nunca se encadena presentación → base →
 *   presentación, así que cambiar de presentación las veces que sea no produce drift. Una XML
 *   editada vuelve al origen solo con {@link resetLineToSupplierOrigin} (acción explícita).
 */
export function resolvePackagingChange(
  line: PackagingLine,
  currentFactor: number,
  nextFactor: number,
  nextPackagingLevelId?: string,
): PackagingChangeResult {
  const next = safeFactor(nextFactor);
  if (hasSupplierDocumentOrigin(line) && !isXmlLineEditedFromOrigin(line, currentFactor)) {
    const quantity = line.xmlQuantity ?? line.quantity;
    const unitPrice = line.xmlUnitPrice ?? line.unitPrice;
    return {
      anchor: "document",
      quantity,
      unitPrice,
      quantityInBaseUom: quantity * next,
      baseUnitPrice: undefined,
      xmlOriginFactor: next,
      xmlOriginPackagingLevelId: nextPackagingLevelId,
    };
  }
  const baseQuantity = resolveLineBaseQuantity(line, currentFactor);
  const baseUnitPrice = resolveLineBaseUnitPrice(line, currentFactor);
  return {
    anchor: "base",
    quantity: baseQuantity / next,
    unitPrice: baseUnitPrice * next,
    quantityInBaseUom: baseQuantity,
    baseUnitPrice,
    xmlOriginFactor: line.xmlOriginFactor,
    xmlOriginPackagingLevelId: line.xmlOriginPackagingLevelId ?? undefined,
  };
}

/**
 * FASE 5 PURCHASE-UX — "Recalcular desde XML": única acción que restaura la cantidad/costo del
 * proveedor, en la presentación de origen. null sin snapshot documental.
 */
export function resetLineToSupplierOrigin(
  line: PackagingLine & Pick<PurchaseLineFormValues, "packagingLevelId">,
  currentFactor: number,
): (Omit<PackagingChangeResult, "anchor"> & {
  packagingLevelId: string | undefined;
  conversionFactor: number;
}) | null {
  if (typeof line.xmlQuantity !== "number" || typeof line.xmlUnitPrice !== "number") return null;
  const originFactor = resolveXmlOriginFactor(line, currentFactor);
  const packagingLevelId = line.xmlOriginPackagingLevelId ?? line.packagingLevelId ?? undefined;
  return {
    quantity: line.xmlQuantity,
    unitPrice: line.xmlUnitPrice,
    quantityInBaseUom: line.xmlQuantity * originFactor,
    baseUnitPrice: undefined,
    packagingLevelId,
    conversionFactor: originFactor,
    xmlOriginFactor: originFactor,
    xmlOriginPackagingLevelId: packagingLevelId,
  };
}

export interface LinePersistence {
  /**
   * "document": XML sin editar, en la unidad del documento; "presentation": presentación sin
   * pérdida; "base": unidad base (lo a registrar no es exacto en la presentación).
   */
  mode: "document" | "presentation" | "base";
  quantity: number;
  unitPrice: number;
  packagingLevelId: string | undefined;
  /** Cantidad en presentación para mostrar (informativa cuando mode = "base"). */
  presentationQuantity: number;
}

/**
 * FASE 5 PURCHASE-UX — cómo se PERSISTE una línea. El backend guarda quantity/unitPrice en la
 * presentación y deriva la base (round(quantity × factor)). Invariante: guardar → reabrir → guardar
 * nunca altera la cantidad/costo a registrar por conversión de presentación.
 * - XML sin editar: tal cual, en la unidad del documento del proveedor.
 * - Manual o XML editada: en la presentación solo si lo a registrar se expresa EXACTO en ella
 *   (24 u. en paca x12 = 2 × 25.482); si no (25 u. = 2.08333… pacas), en UNIDAD BASE — manual sin
 *   presentación; XML con el nivel base del ítem (Confirmar exige presentación en XML inventariable
 *   y, sin ella, el backend aplicaría la presentación aprendida). El snapshot XML no se toca.
 * Única fuente para el payload y para los avisos de la pantalla.
 */
export function resolveLinePersistence(
  line: PackagingLine &
    Pick<PurchaseLineFormValues, "packagingLevelId" | "conversionFactor" | "context">,
  purchaseUnitPriceDecimals: number,
): LinePersistence {
  const factor = safeFactor(line.conversionFactor);
  const packagingLevelId = line.packagingLevelId ?? undefined;
  const isXml = hasSupplierDocumentOrigin(line);
  const asIs = (mode: LinePersistence["mode"]): LinePersistence => ({
    mode,
    quantity: line.quantity,
    unitPrice: line.unitPrice,
    packagingLevelId,
    presentationQuantity: line.quantity,
  });
  if (isXml && !isXmlLineEditedFromOrigin(line, factor)) return asIs("document");
  if (!packagingLevelId || factor === 1) return asIs("presentation");

  const baseQuantity = resolveLineBaseQuantity(line, factor);
  const baseUnitPrice = resolveLineBaseUnitPrice(line, factor);
  const presentationQuantity = baseQuantity / factor;
  const storedQuantity = roundTo(presentationQuantity, PERSISTED_QUANTITY_DECIMALS);
  const presentationPrice = baseUnitPrice * factor;
  const storedPrice = roundTo(presentationPrice, purchaseUnitPriceDecimals);
  const lossless =
    Math.abs(storedQuantity * factor - baseQuantity) <= FLOAT_EPS &&
    Math.abs(storedPrice - presentationPrice) <= FLOAT_EPS;
  if (lossless) {
    return {
      mode: "presentation",
      quantity: storedQuantity,
      unitPrice: storedPrice,
      packagingLevelId,
      presentationQuantity,
    };
  }
  const baseUnitLevelId = line.context?.packagingLevels?.find(
    (p) => p.isBaseUnit && p.baseQuantity === 1,
  )?.id;
  // XML sin nivel base conocido (contexto aún no cargado): no se puede expresar en unidad base sin
  // que el backend aplique la presentación aprendida — se conserva la presentación actual.
  if (isXml && !baseUnitLevelId) return asIs("presentation");
  return {
    mode: "base",
    quantity: baseQuantity,
    unitPrice: baseUnitPrice,
    packagingLevelId: isXml ? baseUnitLevelId : undefined,
    presentationQuantity,
  };
}

/** FASE 5 PURCHASE-UX — el valor del input no cambió a la escala visible (blur sin edición real). */
export function isUnchangedAtPrecision(entered: number, current: number, decimals: number): boolean {
  return roundTo(entered, decimals) === roundTo(current, decimals);
}

/**
 * FASE 5 PURCHASE-UX — "Cantidad a registrar" tecleada (unidad base): queda AUTORITATIVA en
 * `quantityInBaseUom`; la cantidad del documento se deriva (B / factor). El precio base no cambia.
 * 0/vacío no se aplica (null).
 */
export function applyRegisteredQuantity(
  line: PackagingLine,
  factor: number,
  entered: number,
): Pick<PackagingChangeResult, "quantity" | "quantityInBaseUom" | "baseUnitPrice"> | null {
  if (!(entered > 0)) return null;
  return {
    quantity: entered / safeFactor(factor),
    quantityInBaseUom: entered,
    baseUnitPrice: resolveLineBaseUnitPrice(line, factor),
  };
}

/**
 * FASE 5 PURCHASE-UX — "Costo unitario a registrar" tecleado (neto de descuento, con flete/gastos
 * asignados): se despeja el precio base AUTORITATIVO (fórmula inversa de computeUnitPriceFromBaseUnitCost
 * en unidad base) y el precio por presentación se deriva (P × factor). null si no es seguro.
 */
export function applyRegisteredUnitCost(
  line: PackagingLine & Pick<PurchaseLineFormValues, "discountPct" | "freightAllocated" | "otherCostsAllocated">,
  factor: number,
  entered: number,
): Pick<PackagingChangeResult, "unitPrice" | "baseUnitPrice"> | null {
  const baseQuantity = resolveLineBaseQuantity(line, factor);
  const baseUnitPrice = computeUnitPriceFromBaseUnitCost({
    newBaseUnitCost: entered,
    quantity: baseQuantity,
    quantityInBaseUom: baseQuantity,
    discountPct: line.discountPct ?? 0,
    freightAllocated: line.freightAllocated ?? 0,
    otherCostsAllocated: line.otherCostsAllocated ?? 0,
  });
  if (baseUnitPrice === null) return null;
  return { unitPrice: baseUnitPrice * safeFactor(factor), baseUnitPrice };
}
