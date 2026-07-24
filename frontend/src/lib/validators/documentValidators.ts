/** Módulo 10 — cédula ecuatoriana (10 dígitos). */
export function isValidCedula(num: string): boolean {
  if (!/^\d{10}$/.test(num)) return false;
  const province = parseInt(num.slice(0, 2), 10);
  if (province < 1 || province > 24) return false;
  const digits = num.split('').map(Number);
  const coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];
  const sum = coefficients.reduce((acc, coef, i) => {
    let val = coef * digits[i]!;
    if (val >= 10) val -= 9;
    return acc + val;
  }, 0);
  const verifier = (10 - (sum % 10)) % 10;
  return verifier === digits[9];
}

/** RUC Ecuador (13 dígitos). Los primeros 10 deben ser cédula válida; últimos 3 = "001". */
export function isValidRuc(num: string): boolean {
  if (!/^\d{13}$/.test(num)) return false;
  if (!num.endsWith('001')) return false;
  return isValidCedula(num.slice(0, 10));
}
