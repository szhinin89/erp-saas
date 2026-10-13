import { describe, expect, it } from "vitest";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { roundToDecimals } from "../../../lib/sanitizers";
import type { PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildPurchaseDraftPayload } from "./purchaseDraftPayload";
import { applyResolvedReceptionLines } from "./pendingProductsResolution";
import {
  applyRegisteredQuantity,
  applyRegisteredUnitCost,
  buildPurchaseLinePresentation,
  headerInputKey,
  isUnchangedAtPrecision,
  resetLineToSupplierOrigin,
  resolvePackagingChange,
  resolveXmlOriginPresentation,
} from "./purchaseLinePresentation";

// FASE 5 PURCHASE-UX — "Cantidad a registrar" y "Costo unitario a registrar" (unidad base) son
// autoritativos. Invariante: guardar → reabrir → guardar nunca los altera por conversión de
// presentación. La presentación y el documento del proveedor son información derivada.

const PURCHASE_PRICE_DECIMALS = 4; // TEST_PRECISION_POLICY.purchaseUnitPriceDecimals
const QUANTITY_DECIMALS = 4; // TEST_PRECISION_POLICY.quantityDecimals

const context: PurchaseItemContextDto = {
  itemId: "item-1",
  sku: "FANTA",
  shortName: "Fanta",
  description: "Fanta naranja",
  baseUomCode: "UNIT",
  participatesInInventory: true,
  supplierCode: "3172",
  packagingLevels: [
    { id: "unit-1", name: "UNIDAD", baseQuantity: 1, uomCode: "UNIT", isBaseUnit: true, isPurchaseDefault: false },
    { id: "paca-12", name: "PACA", baseQuantity: 12, uomCode: "PACA", isBaseUnit: false, isPurchaseDefault: true },
    { id: "six-6", name: "SIXPACK", baseQuantity: 6, uomCode: "SIX", isBaseUnit: false, isPurchaseDefault: false },
  ],
  currentStock: 0,
  availableStock: 0,
  reservedStock: 0,
  averageCost: 0,
  lastPurchaseCost: 0,
  pvp: 1.5,
  previousPrice: 0,
  maxDiscountPercent: 0,
  purchaseVatCode: "2",
  vatPercent: 15,
  exciseTaxCode: null,
  icePercent: 0,
  hasVat: true,
  hasIce: false,
  costMargin: 0,
  costMarginPercent: 0,
};

const factorOf = (packagingLevelId: string | null | undefined) =>
  context.packagingLevels.find((p) => p.id === packagingLevelId)?.baseQuantity ?? 1;

/** Línea manual (sin origen XML) en unidad base. */
function manualLine(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    description: "FANTA",
    quantity: 25,
    unitPrice: 2.1235,
    vatCode: "2",
    discountPct: 0,
    context,
    ...overrides,
  } as PurchaseLineFormValues;
}

/** Línea XML recién creada desde Recepción: el proveedor facturó "2" a $24 (recepción propone PACA x12). */
function xmlLineFromReception(): PurchaseLineFormValues {
  const proposal = { packagingLevelId: "paca-12", conversionFactor: 12 };
  return manualLine({
    quantity: 2,
    unitPrice: 24,
    purchaseReceptionLineId: "rl-1",
    xmlSupplierCode: "3172",
    xmlQuantity: 2,
    xmlUnitPrice: 24,
    xmlTaxableBase: 48,
    ...proposal,
    quantityInBaseUom: 24,
    ...resolveXmlOriginPresentation(proposal),
  });
}

/** Misma secuencia que PurchaseLineCard.handlePackagingChange. */
function selectPresentation(line: PurchaseLineFormValues, packagingLevelId: string): PurchaseLineFormValues {
  const id = packagingLevelId || undefined;
  const next = resolvePackagingChange(line, factorOf(line.packagingLevelId), factorOf(id), id);
  return {
    ...line,
    packagingLevelId: id,
    conversionFactor: factorOf(id),
    quantity: next.quantity,
    unitPrice: next.unitPrice,
    quantityInBaseUom: next.quantityInBaseUom,
    baseUnitPrice: next.baseUnitPrice,
    xmlOriginFactor: next.xmlOriginFactor,
    xmlOriginPackagingLevelId: next.xmlOriginPackagingLevelId,
  };
}

function payloadLine(line: PurchaseLineFormValues) {
  return buildPurchaseDraftPayload(
    {
      supplierId: "s-1",
      docTypeCode: "01",
      invoiceNumber: "001-001-000000001",
      issueDate: "2026-10-12",
      lines: [line],
      freightCost: 0,
      otherCosts: 0,
    } as unknown as Parameters<typeof buildPurchaseDraftPayload>[0],
    undefined,
    { purchaseUnitPriceDecimals: PURCHASE_PRICE_DECIMALS },
  ).lines[0];
}

/**
 * Guardar → reabrir: el backend persiste quantity (numeric(20,6)) y unitPrice, resuelve el factor por
 * PackagingLevelId y deriva QuantityInBaseUom = round(quantity × factor, quantityDecimals); persiste
 * XmlOriginPackagingLevelId con su factor resuelto del ítem (`persistOrigin: false` simula un
 * borrador anterior a la columna). loadForEdit rehidrata la línea con esos valores, el snapshot XML
 * intacto y el origen vía resolveXmlOriginPresentation (recepción: PACA x12 como fallback).
 */
function saveAndReopen(
  line: PurchaseLineFormValues,
  { persistOrigin = true }: { persistOrigin?: boolean } = {},
): PurchaseLineFormValues {
  const sent = payloadLine(line);
  const factor = factorOf(sent.packagingLevelId);
  const storedQuantity = roundToDecimals(sent.quantity, 6);
  const persistedOriginId = persistOrigin ? (sent.xmlOriginPackagingLevelId ?? null) : null;
  const persisted = {
    packagingLevelId: sent.packagingLevelId ?? null,
    conversionFactor: factor,
    xmlOriginPackagingLevelId: persistedOriginId,
    xmlOriginFactor: persistedOriginId ? factorOf(persistedOriginId) : null,
  };
  return {
    ...line,
    quantity: storedQuantity,
    unitPrice: sent.unitPrice,
    packagingLevelId: sent.packagingLevelId,
    conversionFactor: factor,
    quantityInBaseUom: roundToDecimals(storedQuantity * factor, QUANTITY_DECIMALS),
    baseUnitPrice: undefined,
    ...(line.purchaseReceptionLineId
      ? resolveXmlOriginPresentation(persisted, { packagingLevelId: "paca-12", conversionFactor: 12 })
      : { xmlOriginFactor: undefined, xmlOriginPackagingLevelId: undefined }),
  };
}

const vmOf = (line: PurchaseLineFormValues) =>
  buildPurchaseLinePresentation(line, getPrecisionPolicy());

function xmlEditedTo23(): PurchaseLineFormValues {
  const line = xmlLineFromReception();
  return { ...line, ...applyRegisteredQuantity(line, 12, 23)! };
}

describe("1. XML 2 × PACA x12 → 24 unidades", () => {
  it("origen del proveedor + presentación → valores base; se guarda en la unidad del documento", () => {
    const line = xmlLineFromReception();
    const vm = vmOf(line);

    expect(vm.registration.baseQuantityValue).toBe(24);
    expect(vm.registration.sentence).toBe("Se registrarán 24.0000 unidades a $2.000000 c/u");
    expect(vm.registration.originDiffers).toBe(false);
    expect(payloadLine(line)).toMatchObject({ quantity: 2, unitPrice: 24, packagingLevelId: "paca-12" });
  });

  it("elegir la presentación en una XML sin editar también convierte el origen (unidad → x12)", () => {
    const asUnit = selectPresentation(xmlLineFromReception(), "unit-1");
    expect(vmOf(asUnit).registration.baseQuantityValue).toBe(2);

    const asPaca = selectPresentation(asUnit, "paca-12");
    expect(vmOf(asPaca).registration.baseQuantityValue).toBe(24);
    expect(vmOf(asPaca).registration.originDiffers).toBe(false);
  });
});

describe("2. XML editada 24 → 23: guardar/reabrir/guardar varias veces sigue exactamente 23", () => {
  it("23 no es exacto en PACA x12 → se persiste en unidad base (nivel base del ítem) y no deriva", () => {
    let line = xmlEditedTo23();
    expect(payloadLine(line)).toMatchObject({ quantity: 23, unitPrice: 2, packagingLevelId: "unit-1" });

    for (let round = 0; round < 5; round++) {
      line = saveAndReopen(line);
      expect(line.quantityInBaseUom).toBe(23);
      expect(vmOf(line).registration.baseQuantityValue).toBe(23);
      expect(vmOf(line).inventory.baseUnitCostValue).toBe(2);
      expect(payloadLine(line)).toMatchObject({ quantity: 23, unitPrice: 2, packagingLevelId: "unit-1" });
    }
  });

  it("XML editada exacta en la presentación (24 → 36 = 3 × PACA) conserva la presentación", () => {
    const line = xmlLineFromReception();
    const edited = { ...line, ...applyRegisteredQuantity(line, 12, 36)! };

    expect(payloadLine(edited)).toMatchObject({ quantity: 3, unitPrice: 24, packagingLevelId: "paca-12" });
    const reopened = saveAndReopen(edited);
    expect(vmOf(reopened).registration.baseQuantityValue).toBe(36);
    expect(vmOf(reopened).registration.originDiffers).toBe(true);
  });

  it("cambiar de presentación una XML editada no sobrescribe el 23 del usuario", () => {
    let line: PurchaseLineFormValues = saveAndReopen(xmlEditedTo23());
    for (const id of ["six-6", "paca-12", "unit-1"]) {
      line = selectPresentation(line, id);
      expect(vmOf(line).registration.baseQuantityValue).toBe(23);
    }
  });
});

describe("3. el origen XML sigue mostrando 2 × PACA x12", () => {
  it("tras persistir en unidad base, el snapshot y la presentación de origen quedan intactos", () => {
    const line = saveAndReopen(saveAndReopen(xmlEditedTo23()));
    const vm = vmOf(line);

    expect(line.xmlQuantity).toBe(2);
    expect(line.xmlUnitPrice).toBe(24);
    expect(line.xmlOriginFactor).toBe(12);
    expect(vm.xml.quantity).toBe("2.0000");
    expect(vm.registration.originDiffers).toBe(true);
    expect(vm.registration.originDetail).toBe(
      "Documento del proveedor: 2.0000 PACA × $24.0000 (base imp. $48.00) = 24.0000 unidades.",
    );
  });
});

describe("4. Recalcular desde XML → vuelve a 24", () => {
  it("restaura cantidad/costo del proveedor en la presentación de origen", () => {
    const reopened = saveAndReopen(xmlEditedTo23());
    const reset = resetLineToSupplierOrigin(reopened, factorOf(reopened.packagingLevelId))!;
    const line = { ...reopened, ...reset };

    expect(reset).toMatchObject({ quantity: 2, unitPrice: 24, packagingLevelId: "paca-12", conversionFactor: 12 });
    expect(vmOf(line).registration.baseQuantityValue).toBe(24);
    expect(vmOf(line).registration.originDiffers).toBe(false);
    expect(payloadLine(line)).toMatchObject({ quantity: 2, unitPrice: 24, packagingLevelId: "paca-12" });
  });
});

describe("5. manual 25 + PACA x12 → persiste 25 en unidad base", () => {
  it("se registran 25.0000 (nunca 24.9996); equivalencia ≈2.0833 solo informativa; save/reopen estable", () => {
    let line = selectPresentation(manualLine(), "paca-12");
    const vm = vmOf(line);

    expect(vm.registration.sentence).toBe("Se registrarán 25.0000 unidades a $2.123500 c/u");
    expect(vm.registration.presentationDetail).toContain("≈ 2.0833 PACA");
    expect(vm.registration.savedInBaseUnitNote).toContain("se guardará en unidad base");
    expect(payloadLine(line)).toMatchObject({ quantity: 25, packagingLevelId: undefined });

    for (let round = 0; round < 3; round++) {
      line = saveAndReopen(line);
      expect(vmOf(line).registration.baseQuantityValue).toBe(25);
      expect(payloadLine(line).quantity).toBe(25);
      expect(payloadLine(line).unitPrice).toBe(2.1235);
    }
  });
});

describe("6. manual 24 + PACA x12 → conserva la presentación", () => {
  it("exacto: se persiste 2 × 25.482 y al reabrir sigue siendo 24 a 2.1235", () => {
    const line = selectPresentation(manualLine({ quantity: 24 }), "paca-12");

    expect(payloadLine(line)).toMatchObject({ quantity: 2, unitPrice: 25.482, packagingLevelId: "paca-12" });
    expect(vmOf(line).registration.savedInBaseUnitNote).toBe("");
    expect(vmOf(line).registration.presentationDetail).toContain("= 2.0000 PACA");

    const reopened = saveAndReopen(saveAndReopen(line));
    expect(vmOf(reopened).registration.baseQuantityValue).toBe(24);
    expect(vmOf(reopened).inventory.baseUnitCostValue).toBeCloseTo(2.1235, 12);
    expect(payloadLine(reopened)).toMatchObject({ quantity: 2, unitPrice: 25.482, packagingLevelId: "paca-12" });
  });
});

describe("7. cambio repetido de presentación no produce drift", () => {
  it("manual: unidad → x12 → x6 → x12 → unidad × 5 ciclos, cantidad y precio base idénticos", () => {
    let line = manualLine();
    for (let cycle = 0; cycle < 5; cycle++) {
      for (const id of ["paca-12", "six-6", "paca-12", ""]) {
        line = selectPresentation(line, id);
        expect(line.quantityInBaseUom).toBe(25);
        expect(vmOf(line).registration.baseQuantityValue).toBe(25);
      }
    }
    expect(line.quantity).toBe(25);
    expect(line.unitPrice).toBe(2.1235);
    expect(payloadLine(line)).toMatchObject({ quantity: 25, unitPrice: 2.1235 });
  });

  it("XML editada: ciclos de presentación conservan 23 y el costo base 2", () => {
    let line = xmlEditedTo23();
    for (let cycle = 0; cycle < 5; cycle++) {
      for (const id of ["six-6", "unit-1", "paca-12"]) {
        line = selectPresentation(line, id);
        expect(line.quantityInBaseUom).toBe(23);
        expect(line.baseUnitPrice).toBe(2);
      }
    }
  });
});

describe("Costo a registrar y blur", () => {
  it("teclear el costo a registrar fija el precio base; el precio por presentación se deriva", () => {
    const withPaca = selectPresentation(manualLine({ quantity: 24 }), "paca-12");
    const line = { ...withPaca, ...applyRegisteredUnitCost(withPaca, 12, 1.9)! };

    expect(line.baseUnitPrice).toBeCloseTo(1.9, 12);
    expect(line.unitPrice).toBeCloseTo(22.8, 12);
    expect(vmOf(line).registration.sentence).toBe("Se registrarán 24.0000 unidades a $1.900000 c/u");
  });

  it("blur sin edición real (valor visible redondeado) no se aplica", () => {
    expect(isUnchangedAtPrecision(2.1235, 2.12349999, 4)).toBe(true);
    expect(isUnchangedAtPrecision(2.1236, 2.1235, 4)).toBe(false);
  });

  it("aclara el costo cuando incluye descuento y flete asignado", () => {
    const vm = vmOf(manualLine({ quantity: 1, unitPrice: 10, discountPct: 10, freightAllocated: 1 }));
    expect(vm.registration.costNote).toBe("Costo neto de descuento, incluye flete/gastos asignados.");
  });

  it("headerInputKey remonta el Costo cuando cambia el costo base (p. ej. flete distribuido)", () => {
    const before = vmOf(xmlLineFromReception());
    const after = vmOf({ ...xmlLineFromReception(), freightAllocated: 2.4 });
    expect(headerInputKey("cost", before, "paca-12")).not.toBe(headerInputKey("cost", after, "paca-12"));
  });
});

describe("FASE 5 — presentación de origen XML persistida (independiente de la recepción)", () => {
  /** XML sin editar donde el usuario corrige el origen de la recepción: PACA x12 → SIXPACK x6. */
  function xmlWithOriginSix(): PurchaseLineFormValues {
    return selectPresentation(xmlLineFromReception(), "six-6");
  }

  it("1. origen x12 → save/reopen → sigue x12 (el payload persiste el origen)", () => {
    const line = xmlLineFromReception();
    expect(payloadLine(line).xmlOriginPackagingLevelId).toBe("paca-12");

    const reopened = saveAndReopen(saveAndReopen(line));
    expect(reopened.xmlOriginPackagingLevelId).toBe("paca-12");
    expect(reopened.xmlOriginFactor).toBe(12);
  });

  it("2. el usuario cambia el origen x12 → x6 → save/reopen → sigue x6 aunque la recepción diga x12", () => {
    const line = xmlWithOriginSix();
    expect(vmOf(line).registration.baseQuantityValue).toBe(12);

    const reopened = saveAndReopen(saveAndReopen(line));
    expect(reopened.xmlOriginPackagingLevelId).toBe("six-6");
    expect(reopened.xmlOriginFactor).toBe(6);
    expect(vmOf(reopened).registration.baseQuantityValue).toBe(12);
    expect(vmOf(reopened).registration.originDetail).toContain("2.0000 SIXPACK");
  });

  it("3. editada (12 → 11, no exacto en x6) se persiste en unidad base y el origen sigue x6", () => {
    const six = xmlWithOriginSix();
    const edited = { ...six, ...applyRegisteredQuantity(six, 6, 11)! };

    expect(payloadLine(edited)).toMatchObject({
      quantity: 11,
      unitPrice: 4,
      packagingLevelId: "unit-1",
      xmlOriginPackagingLevelId: "six-6",
    });
    let line: PurchaseLineFormValues = edited;
    for (let round = 0; round < 3; round++) {
      line = saveAndReopen(line);
      expect(line.packagingLevelId).toBe("unit-1");
      expect(line.xmlOriginPackagingLevelId).toBe("six-6");
      expect(line.xmlOriginFactor).toBe(6);
      expect(vmOf(line).registration.baseQuantityValue).toBe(11);
      expect(vmOf(line).registration.originDiffers).toBe(true);
    }
  });

  it("4. Recalcular desde XML tras reabrir usa x6 (2 × 6 = 12), no la recepción x12", () => {
    const six = xmlWithOriginSix();
    const reopened = saveAndReopen({ ...six, ...applyRegisteredQuantity(six, 6, 11)! });
    const reset = resetLineToSupplierOrigin(reopened, factorOf(reopened.packagingLevelId))!;
    const line = { ...reopened, ...reset };

    expect(reset).toMatchObject({ quantity: 2, unitPrice: 24, packagingLevelId: "six-6", conversionFactor: 6 });
    expect(vmOf(line).registration.baseQuantityValue).toBe(12);
    expect(vmOf(line).registration.originDiffers).toBe(false);
  });

  it("6. borrador anterior sin origen persistido: fallback compatible a la recepción", () => {
    const reopened = saveAndReopen(xmlEditedTo23(), { persistOrigin: false });

    expect(reopened.xmlOriginPackagingLevelId).toBe("paca-12");
    expect(reopened.xmlOriginFactor).toBe(12);
    expect(resolveXmlOriginPresentation({ packagingLevelId: null, conversionFactor: 1 }, null)).toEqual({
      xmlOriginPackagingLevelId: undefined,
      xmlOriginFactor: 1,
    });
  });

  it("línea manual nunca envía origen XML", () => {
    expect(payloadLine(manualLine({ xmlOriginPackagingLevelId: "six-6" })).xmlOriginPackagingLevelId).toBeUndefined();
  });
});

describe("FASE 5 — advertencia de origen XML solo con cambio económico real", () => {
  /**
   * Caso real: línea XML pendiente "50 SIXPACK × $6 = $300" creada desde Recepción SIN presentación
   * (origen inicial factor 1); "Resolver productos pendientes" la vincula a SIXPACK X6 (factor 6).
   */
  function resolvedSixPack(): PurchaseLineFormValues {
    const pending = manualLine({
      itemId: undefined,
      quantity: 50,
      unitPrice: 6,
      purchaseReceptionLineId: "rl-1",
      xmlSupplierCode: "8433",
      xmlQuantity: 50,
      xmlUnitPrice: 6,
      xmlTaxableBase: 300,
      packagingLevelId: undefined,
      conversionFactor: 1,
      quantityInBaseUom: 50,
      ...resolveXmlOriginPresentation({ packagingLevelId: null, conversionFactor: 1 }),
    });
    const [line] = applyResolvedReceptionLines(
      [pending],
      [
        {
          purchaseReceptionLineId: "rl-1",
          itemId: "item-1",
          itemSku: "8433",
          itemName: "Fanta",
          matchStatus: "MANUALLY_MATCHED",
          packagingLevelId: "six-6",
          uomCode: "SIX",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          equivalenceLearned: true,
        },
      ],
    );
    return { ...line, context };
  }

  it("50 × SIXPACK X6 a $6 → se registran 300 unidades a $1, SIN advertencia", () => {
    const line = resolvedSixPack();
    const vm = vmOf(line);

    expect(line.xmlOriginPackagingLevelId).toBe("six-6");
    expect(line.xmlOriginFactor).toBe(6);
    expect(vm.registration.sentence).toBe("Se registrarán 300.0000 unidades a $1.000000 c/u");
    expect(vm.registration.originDiffers).toBe(false);
    expect(vm.registration.originDetail).toBe(
      "Documento del proveedor: 50.0000 SIXPACK × $6.0000 (base imp. $300.00) = 300.0000 unidades.",
    );
  });

  it("sigue sin advertencia tras guardar y reabrir (origen persistido x6)", () => {
    const reopened = saveAndReopen(resolvedSixPack());

    expect(reopened.xmlOriginFactor).toBe(6);
    expect(vmOf(reopened).registration.originDiffers).toBe(false);
  });

  it("cambio real 300 → 299 unidades SÍ muestra la advertencia", () => {
    const line = resolvedSixPack();
    const edited = { ...line, ...applyRegisteredQuantity(line, 6, 299)! };

    expect(vmOf(edited).registration.baseQuantityValue).toBe(299);
    expect(vmOf(edited).registration.originDiffers).toBe(true);
  });

  it("cambio real de costo ($1 → $0.95 por unidad) SÍ muestra la advertencia", () => {
    const line = resolvedSixPack();
    const edited = { ...line, ...applyRegisteredUnitCost(line, 6, 0.95)! };

    expect(vmOf(edited).registration.originDiffers).toBe(true);
  });
});
