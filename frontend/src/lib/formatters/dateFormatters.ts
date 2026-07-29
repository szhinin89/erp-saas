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
