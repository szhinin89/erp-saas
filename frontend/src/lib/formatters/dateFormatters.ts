/**
 * Estándar corporativo de fechas ERP ZH.
 * Formato: dd/MM/yyyy | dd/MM/yyyy HH:mm | dd/MM/yyyy HH:mm:ss
 * Todas las fechas ISO del backend son UTC — se formatean directamente sin conversión TZ.
 */

function pad(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

function parseIso(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const d = new Date(iso);
  return isNaN(d.getTime()) ? null : d;
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (m) return `${m[3]}/${m[2]}/${m[1]}`;
  const d = parseIso(iso);
  if (!d) return "—";
  return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`;
}

export function formatDateTime(iso: string | null | undefined): string {
  const d = parseIso(iso);
  if (!d) return "—";
  return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()} ${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
}

export function formatDateTimeSeconds(iso: string | null | undefined): string {
  const d = parseIso(iso);
  if (!d) return "—";
  return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()} ${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
}

/**
 * Fecha larga en el idioma de la UI (ej. "lunes, 21 de agosto de 2026"), para
 * saludos/encabezados — no para datos tabulares (usar `formatDate` para eso).
 * Único punto autorizado para `Intl`/`toLocaleDateString` en el proyecto:
 * locale español fijo en `es-EC` (nunca `es-ES` como fallback genérico).
 */
export function formatLongDate(date: Date, uiLocale: "es" | "en" | "qu"): string {
  const intlLocale = uiLocale === "en" ? "en-US" : "es-EC";
  return date.toLocaleDateString(intlLocale, {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

/**
 * Fecha calendario "hoy" en la hora LOCAL del dispositivo — nunca UTC.
 * `toISOString()` siempre convierte a UTC: entre las 19:00 y 23:59 hora Ecuador
 * (UTC-5), UTC ya cruzó a mañana, así que devolvía la fecha equivocada (causa
 * confirmada del rechazo SRI [65] FECHA EMISIÓN EXTEMPORÁNEA — ver ADR de
 * corrección). `getFullYear()/getMonth()/getDate()` sí usan la zona horaria
 * local del navegador.
 */
export function toLocalIsoDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function todayIso(): string {
  return toLocalIsoDate(new Date());
}

export function isValidIsoDate(value: string): boolean {
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    !isNaN(new Date(value + "T00:00:00Z").getTime())
  );
}

/**
 * Normaliza un valor de fecha/hora al formato exigido por
 * `<input type="datetime-local">`: `yyyy-MM-ddTHH:mm`. Acepta ISO del backend
 * (UTC o sin offset) y el formato SRI/XML `dd/MM/yyyy HH:mm[:ss]`. Usa los
 * mismos getters UTC* que formatDate/formatDateTime (fechas ISO del backend
 * son UTC — sin conversión a la zona horaria del navegador).
 * Devuelve "" si no hay dato o no se puede interpretar — nunca inventa una
 * fecha; el input queda vacío en vez de romper.
 */
export function toDateTimeLocalInputValue(
  value: string | null | undefined,
): string {
  if (!value) return "";
  const trimmed = value.trim();
  if (!trimmed) return "";

  const sriMatch = trimmed.match(
    /^(\d{2})\/(\d{2})\/(\d{4})[ T](\d{2}):(\d{2})(?::\d{2})?$/,
  );
  if (sriMatch) {
    const [, dd, mm, yyyy, hh, min] = sriMatch;
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  }

  const d = parseIso(trimmed);
  if (!d) return "";
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
}
