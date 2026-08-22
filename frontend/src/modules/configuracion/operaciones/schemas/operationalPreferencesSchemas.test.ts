import { describe, expect, it } from "vitest";
import {
  cashPreferencesSchema,
  electronicDocumentsPreferencesSchema,
  printingPreferencesSchema,
  salesPosPreferencesSchema,
} from "./operationalPreferencesSchemas";

describe("salesPosPreferencesSchema", () => {
  it("acepta un descuento máximo dentro de rango", () => {
    expect(
      salesPosPreferencesSchema.safeParse({
        allowManualDiscount: true,
        maxDiscountPercent: 15,
      }).success,
    ).toBe(true);
  });

  it("rechaza un descuento máximo mayor a 100", () => {
    expect(
      salesPosPreferencesSchema.safeParse({
        allowManualDiscount: true,
        maxDiscountPercent: 150,
      }).success,
    ).toBe(false);
  });

  it("rechaza un descuento máximo negativo", () => {
    expect(
      salesPosPreferencesSchema.safeParse({
        allowManualDiscount: true,
        maxDiscountPercent: -1,
      }).success,
    ).toBe(false);
  });
});

describe("cashPreferencesSchema", () => {
  it("acepta un booleano válido", () => {
    expect(
      cashPreferencesSchema.safeParse({ requireReasonForDifference: false }).success,
    ).toBe(true);
  });

  it("rechaza un valor no booleano", () => {
    expect(
      cashPreferencesSchema.safeParse({ requireReasonForDifference: "yes" }).success,
    ).toBe(false);
  });
});

describe("printingPreferencesSchema", () => {
  it("acepta un modo y copias válidos", () => {
    expect(
      printingPreferencesSchema.safeParse({
        salesReceiptMode: "AlwaysPrint",
        salesReceiptCopies: 2,
      }).success,
    ).toBe(true);
  });

  it("rechaza un modo desconocido", () => {
    expect(
      printingPreferencesSchema.safeParse({
        salesReceiptMode: "SometimesPrint",
        salesReceiptCopies: 1,
      }).success,
    ).toBe(false);
  });

  it("rechaza copias fuera de rango (1-3)", () => {
    expect(
      printingPreferencesSchema.safeParse({
        salesReceiptMode: "AskBeforePrint",
        salesReceiptCopies: 5,
      }).success,
    ).toBe(false);
  });
});

describe("electronicDocumentsPreferencesSchema", () => {
  it("acepta un booleano válido", () => {
    expect(
      electronicDocumentsPreferencesSchema.safeParse({ emailOnAuthorization: true })
        .success,
    ).toBe(true);
  });
});
