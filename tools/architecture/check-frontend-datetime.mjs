import path from 'node:path';
import { REPO_ROOT, loadConfig, toRepoRel, walkFiles, readText } from './shared/fs-utils.mjs';
import { createCheckResult } from './shared/report-utils.mjs';
import { stripCommentsKeepLines, isInScope, evaluateHits } from './check-frontend-precision.mjs';

/**
 * F-DT — ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02. Canónico: docs/architecture/data-standards.md
 * § Contrato temporal. Único punto autorizado para zona horaria / "hoy" / conversión de instantes:
 * frontend/src/lib/formatters/dateFormatters.ts. Reutiliza el motor del guard de precisión
 * (comentarios eliminados, alcance por globs, excepciones con conteo exacto) — sin maquinaria propia.
 *   F-DT-utc-day         `toISOString().slice(0,10)` / `.split("T")` — día UTC usado como fecha de
 *                        negocio (entre 19:00 y 23:59 Ecuador ya es "mañana"). Usar `todayIso()`.
 *   F-DT-date-parse      `new Date("YYYY-MM-DD")` o `new Date(x + "T00:00[:00]")` — parsear una
 *                        fecha de negocio como instante (UTC o zona del navegador). Usar strings +
 *                        `addDaysIso`/`formatDate`.
 *   F-DT-manual-tz       `Intl.DateTimeFormat`, `timeZone:`, `getTimezoneOffset()`,
 *                        `toLocale(Date|Time)String`, `toTimeString/toDateString/toUTCString`, opciones
 *                        Intl `hour/minute/second` — conversión o formato de hora fuera del helper oficial.
 *   F-DT-local-calendar  `getFullYear/getMonth/getDate/getHours…` (también `getUTC*`) y `set*` sobre
 *                        `Date` — calendario/hora armados a mano fuera del helper oficial.
 *   F-DT-instant-as-date `formatDate(x.createdAt|…At|…Utc|authorizationDate)` — un INSTANTE se
 *                        presenta solo con `formatDateTime` (dd/MM/yyyy HH:mm:ss, Company.Timezone;
 *                        ZH-TEMPORAL-DATETIME-SECONDS-02I). `formatDate` es para fechas de negocio.
 */
export const CHECK_NAME = 'frontend-datetime';

export const RULES = {
  utcDay: 'F-DT-utc-day',
  dateParse: 'F-DT-date-parse',
  manualTz: 'F-DT-manual-tz',
  localCalendar: 'F-DT-local-calendar',
  instantAsDate: 'F-DT-instant-as-date',
};

const MESSAGES = {
  [RULES.utcDay]:
    'día UTC usado como fecha de negocio — usar `todayIso()` de lib/formatters/dateFormatters (hoy de Company.Timezone).',
  [RULES.dateParse]:
    'fecha de negocio parseada como instante — mantener "YYYY-MM-DD" como string (`addDaysIso`, `formatDate`).',
  [RULES.manualTz]:
    'conversión de zona horaria fuera de lib/formatters/dateFormatters — usar formatDateTime/toDateTimeLocalInputValue/fromDateTimeLocalInputValue.',
  [RULES.localCalendar]:
    'calendario/hora armados a mano — usar los helpers de lib/formatters/dateFormatters (Company.Timezone).',
  [RULES.instantAsDate]:
    'instante presentado como fecha — usar `formatDateTime` (única salida visual de instantes: dd/MM/yyyy HH:mm:ss).',
};

const PATTERNS = [
  [RULES.utcDay, /toISOString\s*\(\s*\)\s*\.\s*(?:slice|substring|substr)\s*\(\s*0\s*,\s*10\s*\)/g],
  [RULES.utcDay, /toISOString\s*\(\s*\)\s*\.\s*split\s*\(\s*["'`]T["'`]\s*\)/g],
  [RULES.dateParse, /new\s+Date\s*\(\s*["'`]\d{4}-\d{2}-\d{2}["'`]\s*\)/g],
  [RULES.dateParse, /new\s+Date\s*\([^()]*\+\s*["'`]T\d{2}:\d{2}(?::\d{2})?["'`]\s*\)/g],
  [RULES.dateParse, /new\s+Date\s*\(\s*`[^`]*\}T\d{2}:\d{2}(?::\d{2})?`\s*\)/g],
  [RULES.manualTz, /\bIntl\s*\.\s*DateTimeFormat\b/g],
  [RULES.manualTz, /\btimeZone\s*:/g],
  [RULES.manualTz, /\.getTimezoneOffset\s*\(/g],
  [RULES.manualTz, /\.toLocale(?:Date|Time)?String\s*\(/g],
  [RULES.manualTz, /\.to(?:Time|Date|UTC)String\s*\(/g],
  [RULES.manualTz, /\b(?:hour|minute|second)\s*:\s*["'`](?:numeric|2-digit)["'`]/g],
  [RULES.localCalendar, /\.(?:get|set)(?:UTC)?(?:FullYear|Month|Date|Day|Hours|Minutes|Seconds)\s*\(/g],
  [RULES.instantAsDate, /\bformatDate\s*\(\s*[\w.?!]*(?:At|Utc|authorizationDate)\s*\)/g],
];

function lineOf(content, index) {
  return content.slice(0, index).split('\n').length;
}

/** Hallazgos F-DT de un archivo (sin aplicar excepciones). */
export function findDateTimeHits(rel, raw) {
  const content = stripCommentsKeepLines(raw);
  const hits = [];
  for (const [rule, re] of PATTERNS) {
    for (const m of content.matchAll(re)) {
      hits.push({ rule, file: rel, line: lineOf(content, m.index), message: MESSAGES[rule] });
    }
  }
  return hits;
}

function scanRepo(cfg) {
  const hits = [];
  const files = walkFiles(path.join(REPO_ROOT, 'frontend/src'), { extensions: ['.ts', '.tsx'] });
  for (const abs of files) {
    const rel = toRepoRel(abs);
    if (!isInScope(cfg, rel)) continue;
    hits.push(...findDateTimeHits(rel, readText(rel)));
  }
  return hits;
}

export function runCheckFrontendDateTime() {
  const cfg = loadConfig('frontend-datetime.json');
  return evaluateHits(scanRepo(cfg), { exceptions: cfg.exceptions }, createCheckResult(CHECK_NAME));
}

if (process.argv[1]?.endsWith('check-frontend-datetime.mjs')) {
  if (process.argv.includes('--list')) {
    const cfg = loadConfig('frontend-datetime.json');
    for (const h of scanRepo(cfg)) console.log(`${h.file}:${h.line}\t${h.rule}`);
  } else {
    const result = runCheckFrontendDateTime();
    const { printCheckResult } = await import('./shared/report-utils.mjs');
    process.exit(printCheckResult(result) ? 0 : 1);
  }
}
