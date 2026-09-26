/**
 * Estándar corporativo de fechas ERP ZH — ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02.
 * Canónico: docs/architecture/data-standards.md § Contrato temporal.
 *
 * Dos tipos de dato temporal, nunca mezclados:
 *
 * 1. FECHA DE NEGOCIO — string "YYYY-MM-DD" (backend DateOnly / PostgreSQL date).
 *    Es un día calendario, no un instante: nunca pasa por `Date`, `toISOString()` ni zona
 *    horaria. "2026-09-25" es "2026-09-25" en cualquier navegador y se muestra 25/09/2026.
 *
 * 2. INSTANTE — ISO-8601 UTC terminado en "Z" (backend DateTime Kind=Utc / timestamptz).
 *    Se presenta SIEMPRE en la zona de la empresa (`Company.Timezone`, recibida en
 *    GET /session/context), nunca en la zona del navegador. Una hora ingresada por el usuario
 *    (datetime-local) es hora de la empresa y se convierte a UTC UNA sola vez
 *    (`fromDateTimeLocalInputValue`); al cargar, UTC → hora de empresa
 *    (`toDateTimeLocalInputValue`). Editar/guardar N veces no desplaza el instante.
 *
 * Este archivo es el único punto autorizado para `Intl.DateTimeFormat` con `timeZone` y
 * para aritmética de zona horaria en el frontend (guard: tools/architecture frontend-datetime).
 */

/** Zona fiscal nacional — default de Company.Timezone (mismo valor que el backend). */
export const DEFAULT_COMPANY_TIME_ZONE = "America/Guayaquil";

let companyTimeZone = DEFAULT_COMPANY_TIME_ZONE;

/**
 * Fija la zona de la empresa activa (SSOT: `session.tenant.timezone`). Solo la invoca el store
 * de sesión; una zona inválida o vacía cae al default nacional en vez de romper la UI.
 */
export function setCompanyTimeZone(timeZone: string | null | undefined): void {
  const candidate = timeZone?.trim();
  companyTimeZone =
    candidate && isSupportedTimeZone(candidate)
      ? candidate
      : DEFAULT_COMPANY_TIME_ZONE;
}

export function getCompanyTimeZone(): string {
  return companyTimeZone;
}

function isSupportedTimeZone(timeZone: string): boolean {
  try {
    new Intl.DateTimeFormat("en-US", { timeZone });
    return true;
  } catch {
    return false;
  }
}

function pad(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

const BUSINESS_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;
/** ISO con hora y SIN zona ("2026-09-25T14:38[:00[.000]]") = hora de pared, no un instante. */
const WALL_CLOCK = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?$/;
/** Formato SRI/XML "dd/MM/yyyy HH:mm[:ss]" = hora de pared Ecuador/empresa. */
const SRI_WALL_CLOCK = /^(\d{2})\/(\d{2})\/(\d{4})[ T](\d{2}):(\d{2})(?::(\d{2}))?$/;
/** Instante: ISO con hora y zona explícita ("Z" u offset ±hh:mm). */
const INSTANT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})$/i;

interface WallClock {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
}

const partsFormatterCache = new Map<string, Intl.DateTimeFormat>();

function partsFormatter(timeZone: string): Intl.DateTimeFormat {
  let formatter = partsFormatterCache.get(timeZone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat("en-US", {
      timeZone,
      hourCycle: "h23",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
    partsFormatterCache.set(timeZone, formatter);
  }
  return formatter;
}

/** Hora de pared, en `timeZone`, del instante `epochMs`. */
function wallClockAt(epochMs: number, timeZone: string): WallClock {
  const values: Record<string, number> = {};
  for (const part of partsFormatter(timeZone).formatToParts(new Date(epochMs))) {
    if (part.type !== "literal") values[part.type] = Number(part.value);
  }
  return {
    year: values.year,
    month: values.month,
    day: values.day,
    hour: values.hour === 24 ? 0 : values.hour,
    minute: values.minute,
    second: values.second,
  };
}

function wallClockAsUtcMs(w: WallClock): number {
  return Date.UTC(w.year, w.month - 1, w.day, w.hour, w.minute, w.second);
}

function sameWallClock(a: WallClock, b: WallClock): boolean {
  return (
    a.year === b.year &&
    a.month === b.month &&
    a.day === b.day &&
    a.hour === b.hour &&
    a.minute === b.minute &&
    a.second === b.second
  );
}

/**
 * Hora de pared de la empresa → instante UTC (ms). Mismo criterio que el backend
 * (CompanyTimeZone.ToUtc): hora ambigua (retroceso DST) → ocurrencia en horario estándar
 * (la de offset menor); hora inexistente (salto DST) → null, nunca se inventa un instante.
 */
function wallClockToEpochMs(w: WallClock, timeZone: string): number | null {
  const asUtc = wallClockAsUtcMs(w);
  const DAY = 86_400_000;
  const offsets = new Set<number>();
  for (const probe of [asUtc - DAY, asUtc, asUtc + DAY]) {
    offsets.add(wallClockAsUtcMs(wallClockAt(probe, timeZone)) - probe);
  }
  const matches = [...offsets]
    .map((offset) => ({ offset, epoch: asUtc - offset }))
    .filter(({ epoch }) => sameWallClock(wallClockAt(epoch, timeZone), w));
  if (matches.length === 0) return null;
  matches.sort((a, b) => a.offset - b.offset);
  return matches[0].epoch;
}

function parseInstantMs(value: string): number | null {
  if (!INSTANT.test(value)) return null;
  const ms = Date.parse(value);
  return Number.isNaN(ms) ? null : ms;
}

function formatWallClockDate(w: WallClock): string {
  return `${pad(w.day)}/${pad(w.month)}/${w.year}`;
}

/**
 * Presentación dd/MM/yyyy. Fecha de negocio "YYYY-MM-DD" → sin conversión alguna. Instante
 * (createdAt, authorizedAt…) → día calendario en la zona de la empresa (un instante nunca se
 * trata como fecha de negocio recortando sus 10 primeros caracteres: eso es el día UTC).
 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const trimmed = value.trim();
  const business = trimmed.match(BUSINESS_DATE);
  if (business) return `${business[3]}/${business[2]}/${business[1]}`;
  const epoch = parseInstantMs(trimmed);
  if (epoch === null) return "—";
  return formatWallClockDate(wallClockAt(epoch, companyTimeZone));
}

/**
 * ÚNICA salida visual de un INSTANTE (ZH-TEMPORAL-DATETIME-SECONDS-02I): instante UTC →
 * "dd/MM/yyyy HH:mm:ss" en la zona de la empresa, con segundos SIEMPRE (también ":00" cuando el
 * instante realmente los tiene en cero). No existe variante sin segundos ni "de auditoría".
 * Una fecha de negocio ("YYYY-MM-DD") no tiene hora: se muestra solo el día, sin zona.
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  const trimmed = value.trim();
  if (BUSINESS_DATE.test(trimmed)) return formatDate(trimmed);
  const epoch = parseInstantMs(trimmed);
  if (epoch === null) return "—";
  const w = wallClockAt(epoch, companyTimeZone);
  return `${formatWallClockDate(w)} ${pad(w.hour)}:${pad(w.minute)}:${pad(w.second)}`;
}

/**
 * Fecha larga en el idioma de la UI (ej. "lunes, 21 de agosto de 2026"), para
 * saludos/encabezados — no para datos tabulares (usar `formatDate` para eso).
 * Único punto autorizado para `toLocaleDateString` en el proyecto: locale español fijo en
 * `es-EC` (nunca `es-ES` como fallback genérico), zona de la empresa.
 */
export function formatLongDate(date: Date, uiLocale: "es" | "en"): string {
  const intlLocale = uiLocale === "en" ? "en-US" : "es-EC";
  return date.toLocaleDateString(intlLocale, {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
    timeZone: companyTimeZone,
  });
}

/**
 * ÚNICO helper oficial de "hoy" para fechas de negocio: día calendario actual en la zona de la
 * empresa. Nunca `new Date().toISOString().slice(0, 10)` — eso es el día UTC, que entre las
 * 19:00 y 23:59 hora Ecuador (UTC-5) ya es "mañana" (causa del rechazo SRI [65]).
 */
export function todayIso(): string {
  const w = wallClockAt(Date.now(), companyTimeZone);
  return `${w.year}-${pad(w.month)}-${pad(w.day)}`;
}

/** Primer día del mes de "hoy" (zona de la empresa), "YYYY-MM-01". */
export function firstDayOfMonthIso(): string {
  return `${todayIso().slice(0, 8)}01`;
}

/**
 * Aritmética de calendario pura sobre una fecha de negocio: "YYYY-MM-DD" + n días. No depende
 * de zona horaria (opera en UTC sobre el día, sin horas), así que un vencimiento nunca se corre.
 * Devuelve "" si la entrada no es una fecha de negocio válida.
 */
export function addDaysIso(isoDate: string, days: number): string {
  const m = isoDate.trim().match(BUSINESS_DATE);
  if (!m) return "";
  const d = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]) + days));
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
}

export function isValidIsoDate(value: string): boolean {
  const m = value.match(BUSINESS_DATE);
  if (!m) return false;
  const d = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])));
  return (
    d.getUTCFullYear() === Number(m[1]) &&
    d.getUTCMonth() === Number(m[2]) - 1 &&
    d.getUTCDate() === Number(m[3])
  );
}

/**
 * Instante del backend → valor de `<input type="datetime-local">` en la hora de la EMPRESA:
 * `yyyy-MM-ddTHH:mm`, o `yyyy-MM-ddTHH:mm:ss` cuando el dato tiene segundos reales (el input los
 * conserva y los muestra; la edición sigue siendo a minutos). Así guardar sin tocar el campo no
 * trunca los segundos persistidos, y nunca se inventan. Acepta también una hora de pared ya local
 * (ISO sin zona, o el formato SRI/XML `dd/MM/yyyy HH:mm[:ss]`, que el SRI publica en hora
 * Ecuador): se devuelve tal cual, sin reinterpretarla. Devuelve "" si no hay dato o no se puede
 * interpretar.
 */
export function toDateTimeLocalInputValue(value: string | null | undefined): string {
  if (!value) return "";
  const trimmed = value.trim();
  if (!trimmed) return "";

  const sri = trimmed.match(SRI_WALL_CLOCK);
  if (sri) {
    const [, dd, mm, yyyy, hh, min, ss] = sri;
    return localInputValue(yyyy, mm, dd, hh, min, ss);
  }

  const wall = trimmed.match(WALL_CLOCK);
  if (wall) {
    const [, yyyy, mm, dd, hh, min, ss] = wall;
    return localInputValue(yyyy, mm, dd, hh, min, ss);
  }

  const epoch = parseInstantMs(trimmed);
  if (epoch === null) return "";
  const w = wallClockAt(epoch, companyTimeZone);
  return localInputValue(
    String(w.year),
    pad(w.month),
    pad(w.day),
    pad(w.hour),
    pad(w.minute),
    pad(w.second),
  );
}

function localInputValue(
  yyyy: string,
  mm: string,
  dd: string,
  hh: string,
  min: string,
  ss: string | undefined,
): string {
  const base = `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  return ss && ss !== "00" ? `${base}:${ss}` : base;
}

/**
 * Valor de `<input type="datetime-local">` (hora de la EMPRESA) → instante ISO UTC
 * `yyyy-MM-ddTHH:mm:ssZ` para el backend. Única conversión hora-empresa → UTC del frontend.
 * Round-trip exacto con `toDateTimeLocalInputValue` (cero drift). Vacío o inválido → null;
 * hora inexistente por cambio de horario → null (nunca se inventa un instante).
 */
export function fromDateTimeLocalInputValue(value: string | null | undefined): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  const sri = trimmed.match(SRI_WALL_CLOCK);
  const m = sri
    ? [sri[0], sri[3], sri[2], sri[1], sri[4], sri[5], sri[6]]
    : trimmed.match(WALL_CLOCK);
  if (!m) return null;
  const w: WallClock = {
    year: Number(m[1]),
    month: Number(m[2]),
    day: Number(m[3]),
    hour: Number(m[4]),
    minute: Number(m[5]),
    second: m[6] ? Number(m[6]) : 0,
  };
  const epoch = wallClockToEpochMs(w, companyTimeZone);
  if (epoch === null) return null;
  return new Date(epoch).toISOString().replace(/\.\d{3}Z$/, "Z");
}

/**
 * Filtro "día de empresa" sobre un instante: día calendario "YYYY-MM-DD" → rango UTC
 * semiabierto `[startUtc, endUtcExclusive)` (mismo contrato que ICompanyClock.DayUtcRangeAsync).
 */
export function companyDayUtcRange(
  isoDate: string,
): { startUtc: string; endUtcExclusive: string } | null {
  const next = addDaysIso(isoDate, 1);
  if (!next) return null;
  const startOf = (day: string): string | null => {
    const [y, mo, d] = day.split("-").map(Number);
    for (let minute = 0; minute < 24 * 60; minute += 1) {
      const epoch = wallClockToEpochMs(
        { year: y, month: mo, day: d, hour: Math.floor(minute / 60), minute: minute % 60, second: 0 },
        companyTimeZone,
      );
      if (epoch !== null) return new Date(epoch).toISOString().replace(/\.\d{3}Z$/, "Z");
    }
    return null;
  };
  const startUtc = startOf(isoDate);
  const endUtcExclusive = startOf(next);
  return startUtc && endUtcExclusive ? { startUtc, endUtcExclusive } : null;
}
