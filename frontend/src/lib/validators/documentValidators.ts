import {
  SRI_ID_TYPE_RUC,
  SRI_ID_TYPE_CEDULA,
  SRI_ID_TYPE_PASSPORT,
  SRI_ID_TYPE_CONSUMIDOR_FINAL,
  SRI_ID_TYPE_EXTERIOR,
  SRI_ID_TYPE_PLACA,
} from "../../modules/masterData/constants/sriIdentificationCodes";

/** Módulo 10 — cédula ecuatoriana (10 dígitos). */
export function isValidCedula(num: string): boolean {
  if (!/^\d{10}$/.test(num)) return false;

  const province = parseInt(num.slice(0, 2), 10);
  if (province < 1 || province > 24) return false;

  const digits = num.split("").map(Number);
  const coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];

  const sum = coefficients.reduce((acc, coef, i) => {
    let val = coef * digits[i]!;
    if (val >= 10) val -= 9;
    return acc + val;
  }, 0);

  const verifier = (10 - (sum % 10)) % 10;

  return verifier === digits[9];
}
/**
 * Validador único de identificaciones Ecuador
 */
export function validateIdentification(
  identificationType: string,
  identificationNumber: string,
): boolean {
  const value = identificationNumber.trim();

  switch (identificationType) {
    // RUC
    case SRI_ID_TYPE_RUC:
      return isValidRuc(value);

    // Cédula
    case SRI_ID_TYPE_CEDULA:
      return isValidCedula(value);

    // Pasaporte
    case SRI_ID_TYPE_PASSPORT:
      return value.length > 0;

    // Consumidor final
    case SRI_ID_TYPE_CONSUMIDOR_FINAL:
      return value.length > 0;

    // Exterior
    case SRI_ID_TYPE_EXTERIOR:
      return value.length > 0;

    // Placa
    case SRI_ID_TYPE_PLACA:
      return value.length > 0;

    default:
      return false;
  }
}

/**
 * RUC Ecuador — espejo de `RucValidator` (backend).
 *
 * RUC-SOCIETIES-VALIDATION-01: el SRI no define algoritmo de validación para
 * sociedades privadas (3.er dígito 9) ni públicas (3.er dígito 6); solo se valida
 * estructura. Persona natural (0-5) mantiene módulo 10 sobre la cédula.
 */
function isValidRuc(num: string): boolean {
  if (!/^\d{13}$/.test(num)) return false;

  if (!num.endsWith("001")) return false;

  const province = parseInt(num.slice(0, 2), 10);
  const thirdDigit = Number(num[2]);

  // Natural
  if (thirdDigit >= 0 && thirdDigit <= 5) {
    return isValidCedula(num.substring(0, 10));
  }

  if (province < 1 || (province > 24 && province !== 30)) return false;

  // Gobierno / Sociedad — sin dígito verificador
  return thirdDigit === 6 || thirdDigit === 9;
}
