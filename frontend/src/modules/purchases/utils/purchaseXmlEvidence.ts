import type { PurchaseDraftLineDto } from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

type XmlEvidenceFields = Pick<
  PurchaseLineFormValues,
  | "xmlSupplierCode"
  | "xmlDescription"
  | "xmlSupplierAuxCode"
  | "xmlQuantity"
  | "xmlUnitPrice"
  | "xmlDiscountPct"
  | "xmlDiscount"
  | "xmlLineSubtotal"
  | "xmlTaxCode"
  | "xmlVatPercentage"
  | "xmlTaxValue"
  | "xmlTotalLine"
  | "xmlTaxableBase"
  | "xmlIceAmount"
  | "xmlIrbpnrAmount"
  | "xmlAdditionalFields"
>;

const VAT_TAX_CODE = "2";
const ICE_TAX_CODE = "3";
const IRBPNR_TAX_CODE = "5";

function sumTaxAmount(line: PurchaseDraftLineDto, taxCode: string): number | undefined {
  const rows = line.taxes.filter((tx) => tx.taxCode === taxCode);
  return rows.length > 0 ? rows.reduce((s, tx) => s + tx.taxAmount, 0) : undefined;
}

/**
 * PURCHASE-DRAFT-RECOVERY F1/B2 — campos `xml*` (solo lectura, nunca viajan al API) de una línea a
 * partir de la evidencia original de su línea de recepción. Única proyección, usada al crear la
 * compra desde Recepción y al reabrir un borrador guardado: así `matchesXmlDocument` compara contra
 * los mismos valores que el backend (`PurchaseLineNormalizer`) y, si la línea vuelve exactamente al
 * XML, IVA/ICE/IRBPNR salen de los montos documentales igual que en el backend
 * (`ReceptionTaxHelper`, que usa los impuestos capturados de la recepción).
 */
export function xmlEvidenceFromDraftLine(line: PurchaseDraftLineDto): XmlEvidenceFields {
  const vat = line.taxes.find((tx) => tx.taxCode === VAT_TAX_CODE);
  return {
    xmlSupplierCode: line.supplierCode ?? undefined,
    xmlDescription: line.description,
    xmlSupplierAuxCode: line.supplierAuxCode,
    xmlQuantity: line.quantity,
    xmlUnitPrice: line.unitPrice,
    xmlDiscountPct: line.discountPct ?? 0,
    xmlDiscount: line.discount,
    xmlLineSubtotal: line.lineSubtotal,
    xmlTaxCode: line.taxCode,
    xmlVatPercentage: line.vatPercentage,
    xmlTaxValue: vat?.taxAmount ?? line.taxValue,
    xmlTotalLine: line.totalLine,
    xmlTaxableBase: vat?.taxableBase ?? line.lineSubtotal - line.discount,
    xmlIceAmount: sumTaxAmount(line, ICE_TAX_CODE),
    xmlIrbpnrAmount: sumTaxAmount(line, IRBPNR_TAX_CODE),
    xmlAdditionalFields: line.additionalFields
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((f) => ({ name: f.name, value: f.value })),
  };
}

/**
 * PURCHASE-DRAFT-RECOVERY D1 — match aprendido en la recepción DESPUÉS de guardar el borrador. Solo
 * aplica si la línea de la compra sigue sin producto (nunca sobrescribe una resolución ya
 * persistida) y la recepción tiene un match resuelto (automático o manual — nunca una sugerencia
 * pendiente de revisión). La presentación es la aprendida para Proveedor + Código, la misma que
 * propone "Crear compra" desde Recepción. Devuelve null si no hay nada seguro que recuperar.
 */
export function recoverLearnedMatch(
  line: PurchaseLineFormValues,
  xmlSource: PurchaseDraftLineDto | null | undefined,
): Partial<PurchaseLineFormValues> | null {
  if (line.itemId || !xmlSource?.itemId) return null;
  if (
    xmlSource.itemMatchStatus !== "AUTO_MATCHED" &&
    xmlSource.itemMatchStatus !== "MANUALLY_MATCHED"
  )
    return null;
  return {
    itemId: xmlSource.itemId,
    itemMatchStatus: xmlSource.itemMatchStatus,
    packagingLevelId: xmlSource.packagingLevelId,
    uomCode: xmlSource.uomCode,
    baseUomCode: xmlSource.baseUomCode,
    conversionFactor: xmlSource.conversionFactor,
    quantityInBaseUom: line.quantity * xmlSource.conversionFactor,
    // FASE 5 PURCHASE-UX — la presentación aprendida interpreta el documento del proveedor (la
    // cantidad sigue en sus unidades): es también la presentación de origen.
    xmlOriginPackagingLevelId: xmlSource.packagingLevelId ?? undefined,
    xmlOriginFactor: xmlSource.conversionFactor,
    baseUnitPrice: undefined,
  };
}
