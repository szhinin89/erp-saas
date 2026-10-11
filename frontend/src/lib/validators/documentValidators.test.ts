import { describe, expect, it } from "vitest";
import { validateIdentification } from "./documentValidators";
import { SRI_ID_TYPE_RUC } from "../../modules/masterData/constants/sriIdentificationCodes";

describe("validateIdentification — RUC (RUC-SOCIETIES-VALIDATION-01)", () => {
  it.each([
    ["0391034777001"], // COVAPACK S.A. — real, módulo 11 daba residuo 1
    ["0990789061001"],
    ["1791352688001"],
    ["1790016918001"], // sociedad: sin dígito verificador
    ["1760000070001"], // entidad pública real
    ["1760016919001"], // entidad pública: sin dígito verificador
  ])("sociedad/pública %s es válida por estructura", (ruc) => {
    expect(validateIdentification(SRI_ID_TYPE_RUC, ruc)).toBe(true);
  });

  it.each([
    ["0391034777000"], // establecimiento distinto de 001
    ["3191034777001"], // provincia 31
    ["1781034777001"], // 3.er dígito 8 no asignado
    ["039103477700"], // 12 dígitos
  ])("RUC %s es inválido por estructura", (ruc) => {
    expect(validateIdentification(SRI_ID_TYPE_RUC, ruc)).toBe(false);
  });

  it("persona natural mantiene módulo 10", () => {
    expect(validateIdentification(SRI_ID_TYPE_RUC, "1713175071001")).toBe(true);
    expect(validateIdentification(SRI_ID_TYPE_RUC, "1713175072001")).toBe(false);
  });
});
