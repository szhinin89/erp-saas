import { describe, expect, it } from "vitest";
import type { PurchaseDraftLineDto } from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { matchesXmlDocument, resolveLineTaxes } from "./purchaseCalc";
import { recoverLearnedMatch, xmlEvidenceFromDraftLine } from "./purchaseXmlEvidence";

function source(overrides: Partial<PurchaseDraftLineDto> = {}): PurchaseDraftLineDto {
  return {
    purchaseReceptionLineId: "rl-1",
    itemId: null,
    itemMatchStatus: "PENDING",
    description: "GALLETA XML",
    quantity: 3,
    unitPrice: 1.333333,
    vatCode: "4",
    warehouseId: null,
    notes: null,
    discountPct: 5,
    iceCode: null,
    supplierCode: "PROV-001",
    supplierAuxCode: null,
    discount: 0.2,
    lineSubtotal: 4,
    taxCode: "2",
    vatPercentage: 15,
    taxValue: 0.57,
    totalLine: 3.8,
    packagingLevelId: null,
    uomCode: "UNIT",
    baseUomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 3,
    taxes: [
      { taxCode: "2", taxRateCode: "4", tarifa: 15, taxableBase: 3.8, taxAmount: 0.57 },
      { taxCode: "5", taxRateCode: "5001", tarifa: 0, taxableBase: 3, taxAmount: 0.06 },
    ],
    additionalFields: [],
    ...overrides,
  } as PurchaseDraftLineDto;
}

function formLine(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 1,
    description: "GALLETA XML",
    quantity: 3,
    unitPrice: 1.333333,
    vatCode: "4",
    discountPct: 5,
    purchaseReceptionLineId: "rl-1",
    ...overrides,
  };
}

describe("PURCHASE-DRAFT-RECOVERY B2 — evidencia XML original", () => {
  it("editar y volver exactamente al XML recupera los montos documentales (mismo criterio que backend)", () => {
    const evidence = xmlEvidenceFromDraftLine(source());
    const edited = { ...formLine({ quantity: 4 }), ...evidence };
    const restored = { ...formLine(), ...evidence };

    expect(matchesXmlDocument(edited)).toBe(false);
    expect(matchesXmlDocument(restored)).toBe(true);
    expect(resolveLineTaxes(restored, { "4": 15 })).toEqual({ vat: 0.57, ice: 0, irbpnr: 0.06 });
  });

  it("toma IVA/base de los impuestos capturados de la recepción", () => {
    const evidence = xmlEvidenceFromDraftLine(source({ taxValue: 0.99 }));

    expect(evidence.xmlTaxValue).toBe(0.57);
    expect(evidence.xmlTaxableBase).toBe(3.8);
    expect(evidence.xmlIrbpnrAmount).toBe(0.06);
    expect(evidence.xmlIceAmount).toBeUndefined();
  });
});

describe("PURCHASE-DRAFT-RECOVERY D1 — match aprendido", () => {
  it("solo recupera matches resueltos sobre líneas aún sin producto", () => {
    const learned = source({
      itemId: "item-1",
      itemMatchStatus: "AUTO_MATCHED",
      packagingLevelId: "pkg-1",
      conversionFactor: 12,
    });

    expect(recoverLearnedMatch(formLine(), learned)).toMatchObject({
      itemId: "item-1",
      packagingLevelId: "pkg-1",
      quantityInBaseUom: 36,
    });
    expect(recoverLearnedMatch(formLine({ itemId: "item-guardado" }), learned)).toBeNull();
    expect(
      recoverLearnedMatch(
        formLine(),
        source({ itemId: "item-1", itemMatchStatus: "NEEDS_REVIEW" }),
      ),
    ).toBeNull();
    expect(recoverLearnedMatch(formLine(), null)).toBeNull();
  });
});
