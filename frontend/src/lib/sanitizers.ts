import Decimal from "decimal.js";

/**
 * Único punto de normalización de códigos de catálogo opcionales (tributarios: ICE, IVA
 * opcional, autorización SRI, etc.). Un `<select>`/`<input>` HTML no puede representar `null`
 * — solo `""` (opción "No aplica" o campo vacío) — así que sin esto, `""`/`"   "` viajan
 * intactos al backend y se tratan como código real en vez de "sin valor". Mantiene consistencia
 * con `OptionalCode.Normalize` (ERP.Domain.Common, backend).
 */
export function normalizeOptionalCode(
  value: string | null | undefined,
): string | null {
  if (value == null) return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function trim(s: string): string {
  return s.trim();
}

export function uppercase(s: string): string {
  return s.toUpperCase();
}

export function lowercase(s: string): string {
  return s.toLowerCase();
}

export function removeExtraSpaces(s: string): string {
  return s.replace(/\s+/g, " ").trim();
}

export function removeInvalidChars(s: string, allowed: RegExp): string {
  return s
    .split("")
    .filter((c) => allowed.test(c))
    .join("");
}

export function sanitizeInteger(raw: string, positiveOnly = false): string {
  const negative = !positiveOnly && raw.trimStart().startsWith("-");
  const digits = raw.replace(/[^\d]/g, "");
  return negative && digits.length > 0 ? `-${digits}` : digits;
}

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03C0/03C01 — política léxica ÚNICA de separadores de la entrada (sin
 * locale/Intl/Decimal). Devuelve solo dígitos y a lo sumo un "." canónico, o varios "." si la
 * cadena está malformada (lo resuelve después `sanitizeDecimal`, contrato previo):
 * - "." y "," juntos: el separador MÁS A LA DERECHA es decimal; el otro es agrupación y se elimina
 *   ("1.234,56" / "1,234.56" → "1234.56").
 * - un solo tipo, una aparición: es decimal ("1,5" → "1.5"; "1,234" → "1.234", sin inferir miles).
 * - un solo tipo repetido en grupos de 3 ("1,234,567" / "1.234.567"): agrupación → "1234567".
 * - otro repetido (malformado): comas se descartan, puntos quedan como estaban ("1.2.3").
 */
function normalizeDecimalSeparators(raw: string): string {
  const t = raw.replace(/[^\d.,]/g, "");
  const lastDot = t.lastIndexOf(".");
  const lastComma = t.lastIndexOf(",");
  if (lastDot !== -1 && lastComma !== -1) {
    const [decimal, grouping] = lastDot > lastComma ? [".", ","] : [",", "."];
    return t.split(grouping).join("").replace(decimal, ".");
  }
  if (lastDot === -1 && lastComma === -1) return t;
  const sep = lastComma !== -1 ? "," : ".";
  const count = t.split(sep).length - 1;
  if (count === 1) return t.replace(sep, ".");
  const grouped = new RegExp(`^\\d{1,3}(\\${sep}\\d{3})+$`).test(t);
  if (grouped || sep === ",") return t.split(sep).join("");
  return t;
}

export function sanitizeDecimal(
  raw: string,
  decimals: number,
  positiveOnly = false,
): string {
  const negative = !positiveOnly && raw.trimStart().startsWith("-");
  let s = normalizeDecimalSeparators(raw);
  const parts = s.split(".");
  if (parts.length > 1) {
    const intPart = parts[0]!;
    const decPart = parts
      .slice(1)
      .join("")
      .replace(/[^\d]/g, "")
      .slice(0, decimals);
    s = decPart.length > 0 ? `${intPart}.${decPart}` : intPart;
  }
  return negative && s.length > 0 ? `-${s}` : s;
}

/**
 * Redondea a `decimals` con "mitad hacia arriba" (away from zero), igual que el backend
 * (MidpointRounding.AwayFromZero). `Number.prototype.toFixed` NO sirve para esto: opera sobre el
 * binario, así que (0.495).toFixed(2) === "0.49". Se desplaza el exponente como texto para evitar
 * el error de punto flotante.
 */
export function roundToDecimals(value: number, decimals: number): number {
  if (!Number.isFinite(value)) return value;
  const sign = value < 0 ? -1 : 1;
  // Shift the decimal exponent without rounding significant digits, including e-notation.
  const shift = (n: number, places: number): number => {
    const [coefficient, exponent = "0"] = String(n).split("e");
    return Number(`${coefficient}e${Number(exponent) + places}`);
  };
  const shifted = shift(Math.abs(value), decimals);
  // At this magnitude Number already has no fractional precision at the requested scale.
  if (shifted >= Number.MAX_SAFE_INTEGER) return value;
  // Absorb only floating-point noise around a midpoint (e.g. 1.4849999999999999).
  const whole = Math.floor(shifted);
  const tolerance = Number.EPSILON * shifted;
  const rounded = whole + (shifted - whole >= 0.5 - tolerance ? 1 : 0);
  return sign * shift(rounded, -decimals);
}

/**
 * ZH-DESIGN-SYSTEM-PRECISION-02A: ÚNICO motor de formato numérico de presentación. Redondea con
 * Decimal.js ROUND_HALF_UP (= backend MidpointRounding.AwayFromZero, incluidos los empates) y
 * devuelve punto decimal sin agrupación — el estándar del proyecto.
 *
 * `locale` (solo si se pide explícitamente) aplica ÚNICAMENTE representación (separadores y
 * agrupación): Intl recibe el texto ya fijado a `decimals` como cadena decimal exacta, nunca un
 * Number, y con min = max = `decimals` no tiene nada que redondear. No hay segundo redondeo.
 */
export function formatDecimalDisplay(value: number, decimals: number, locale?: string): string {
  const fixed = new Decimal(value).toFixed(decimals, Decimal.ROUND_HALF_UP);
  if (!locale) return fixed;
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(fixed as Intl.StringNumericLiteral);
}

/**
 * @deprecated ZH-DESIGN-SYSTEM-PRECISION-05B — LEGACY: sin escala usa el default fijo 2. Solo
 * compatibilidad de consumidores legacy baselined (F-PREC-implicit-format). Código nuevo:
 * `formatMoney(value, usePrecisionDecimals(kind))` o ZHMoneyValue/ZHNumberValue con `precision`.
 */
export function formatMoney(value: number): string;
/** Representación con la escala recibida (semántica vía `usePrecisionDecimals`, o contractual). */
export function formatMoney(value: number, decimals: number): string;
export function formatMoney(value: number, decimals = 2): string {
  return formatDecimalDisplay(value, decimals);
}

/**
 * @deprecated ZH-DESIGN-SYSTEM-PRECISION-05B — LEGACY: sin escala usa el default fijo 2. Solo
 * compatibilidad de consumidores legacy baselined (F-PREC-implicit-format). Código nuevo:
 * `formatMoneyWithSymbol(value, usePrecisionDecimals(kind))` o ZHMoneyValue con `precision`.
 */
export function formatMoneyWithSymbol(value: number): string;
/** Representación con símbolo y la escala recibida (semántica vía `usePrecisionDecimals`, o contractual). */
export function formatMoneyWithSymbol(value: number, decimals: number, symbol?: string): string;
export function formatMoneyWithSymbol(
  value: number,
  decimals = 2,
  symbol = "$",
): string {
  return `${symbol}${formatMoney(value, decimals)}`;
}

export function parseDecimal(raw: string): number {
  const clean = raw.replace(/[^\d.-]/g, "");
  return parseFloat(clean) || 0;
}

export function sanitizePhoneLocal(raw: string, maxLength: number): string {
  return raw.replace(/[^\d]/g, "").slice(0, maxLength);
}

export function sanitizeLetters(raw: string): string {
  return raw.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'.-]/g, "");
}

export function sanitizeAlphanumeric(raw: string): string {
  return raw.replace(/[^a-zA-Z0-9]/g, "");
}
