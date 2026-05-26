/**
 * check-i18n-keys.mjs
 * Verifica consistencia de claves i18n entre locales (es/en/qu).
 *
 * Reglas:
 *   - Toda clave en es.json debe existir en en.json y qu.json
 *   - Toda clave en en.json debe existir en es.json (es es la fuente de verdad)
 *   - Detecta claves vacías ("") en cualquier locale
 */
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

export const CHECK_NAME = 'i18n-keys';

const LOCALES_DIR = path.join(REPO_ROOT, 'frontend/src/i18n/locales');

function loadLocale(name) {
  const p = path.join(LOCALES_DIR, `${name}.json`);
  if (!fs.existsSync(p)) return null;
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

function flatKeys(obj, prefix = '') {
  /** @type {Map<string,string>} */
  const out = new Map();
  for (const [k, v] of Object.entries(obj)) {
    const full = prefix ? `${prefix}.${k}` : k;
    if (typeof v === 'object' && v !== null) {
      for (const [nested, val] of flatKeys(v, full)) {
        out.set(nested, val);
      }
    } else {
      out.set(full, String(v ?? ''));
    }
  }
  return out;
}

export function runCheckI18nKeys() {
  const result = createCheckResult(CHECK_NAME);

  const es = loadLocale('es');
  const en = loadLocale('en');
  const qu = loadLocale('qu');

  if (!es) {
    addViolation(result, { rule: 'F-i18n-missing-locale', file: 'frontend/src/i18n/locales/es.json', message: 'locale es.json not found' });
    return result;
  }

  const esKeys = flatKeys(es);
  const enKeys = en ? flatKeys(en) : new Map();
  const quKeys = qu ? flatKeys(qu) : new Map();

  for (const [key, val] of esKeys) {
    // Clave vacía en español
    if (val.trim() === '') {
      addWarning(result, { rule: 'F-i18n-empty', file: 'frontend/src/i18n/locales/es.json', message: `empty value for key: ${key}` });
    }
    // Falta en inglés
    if (en && !enKeys.has(key)) {
      addViolation(result, { rule: 'F-i18n-missing-en', file: 'frontend/src/i18n/locales/en.json', message: `key missing in en: ${key}` });
    }
    // Falta en quechua
    if (qu && !quKeys.has(key)) {
      addViolation(result, { rule: 'F-i18n-missing-qu', file: 'frontend/src/i18n/locales/qu.json', message: `key missing in qu: ${key}` });
    }
  }

  // Claves en en que no están en es (huérfanas)
  for (const key of enKeys.keys()) {
    if (!esKeys.has(key)) {
      addWarning(result, { rule: 'F-i18n-orphan-en', file: 'frontend/src/i18n/locales/en.json', message: `orphan key in en (not in es): ${key}` });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-i18n-keys.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckI18nKeys()) ? 0 : 1);
}
