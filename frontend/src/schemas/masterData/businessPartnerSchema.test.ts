import { describe, expect, it } from "vitest";
import {
  businessPartnerSchema,
  isLegalEntityTypeInferable,
} from "./businessPartnerSchema";

const base = {
  identificationNumber: "1790016919001",
  legalName: "Empresa Prueba",
  tradeName: "",
  countryCode: "EC",
  refundProviderTypeCode: "",
  paymentTermId: "",
};

describe("isLegalEntityTypeInferable", () => {
  it("RUC y CI permiten inferencia", () => {
    expect(isLegalEntityTypeInferable("04")).toBe(true);
    expect(isLegalEntityTypeInferable("05")).toBe(true);
  });

  it("Pasaporte, ConsumidorFinal, Exterior y Placa no permiten inferencia", () => {
    expect(isLegalEntityTypeInferable("06")).toBe(false);
    expect(isLegalEntityTypeInferable("07")).toBe(false);
    expect(isLegalEntityTypeInferable("08")).toBe(false);
    expect(isLegalEntityTypeInferable("09")).toBe(false);
  });
});

describe("businessPartnerSchema — legalEntityTypeCode", () => {
  it("RUC sin legalEntityTypeCode es válido (el backend infiere)", () => {
    const result = businessPartnerSchema("customer").safeParse({
      ...base,
      identificationType: "04",
      legalEntityTypeCode: undefined,
    });
    expect(result.success).toBe(true);
  });

  it("CI sin legalEntityTypeCode es válido (el backend infiere)", () => {
    const result = businessPartnerSchema("customer").safeParse({
      ...base,
      identificationType: "05",
      identificationNumber: "0302126842",
      legalEntityTypeCode: undefined,
    });
    expect(result.success).toBe(true);
  });

  it("Pasaporte sin legalEntityTypeCode es inválido (no puede inferirse)", () => {
    const result = businessPartnerSchema("customer").safeParse({
      ...base,
      identificationType: "06",
      identificationNumber: "P12345678",
      legalEntityTypeCode: undefined,
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const issue = result.error.issues.find((i) =>
        i.path.includes("legalEntityTypeCode"),
      );
      expect(issue).toBeDefined();
    }
  });

  it("Pasaporte con legalEntityTypeCode explícito es válido", () => {
    const result = businessPartnerSchema("customer").safeParse({
      ...base,
      identificationType: "06",
      identificationNumber: "P12345678",
      legalEntityTypeCode: 2,
    });
    expect(result.success).toBe(true);
  });
});
