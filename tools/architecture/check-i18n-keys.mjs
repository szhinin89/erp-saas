/**
 * check-i18n-keys.mjs
 * Verifica consistencia de claves i18n entre locales (es/en).
 *
 * Reglas:
 *   - Toda clave en es.json debe existir en en.json
 *   - Toda clave en en.json debe existir en es.json (es es la fuente de verdad)
 *   - Detecta claves vacías en español
 */
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { inspectLocale } from './shared/locale-integrity.mjs';

export const CHECK_NAME = 'i18n-keys';

const LOCALES_DIR = path.join(REPO_ROOT, 'frontend/src/i18n/locales');

function loadLocale(name, result) {
  const p = path.join(LOCALES_DIR, `${name}.json`);
  if (!fs.existsSync(p)) {
    addViolation(result, {
      rule: 'F-i18n-missing-locale',
      file: p,
      message: `locale ${name}.json not found`,
    });
    return null;
  }

  const { dictionary, duplicates } = inspectLocale(fs.readFileSync(p, 'utf8'));

  for (const key of duplicates) {
    addViolation(result, {
      rule: 'F-i18n-duplicate',
      file: p,
      message: `duplicate key in ${name}: ${key}`,
    });
  }

  return dictionary;
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

  const es = loadLocale('es', result);
  const en = loadLocale('en', result);

  if (!es) {
    addViolation(result, {
      rule: 'F-i18n-missing-locale',
      file: 'frontend/src/i18n/locales/es.json',
      message: 'locale es.json not found',
    });
    return result;
  }

  const esKeys = flatKeys(es);
  const enKeys = en ? flatKeys(en) : new Map();

  for (const [key, val] of esKeys) {
    if (val.trim() === '') {
      addWarning(result, {
        rule: 'F-i18n-empty',
        file: 'frontend/src/i18n/locales/es.json',
        message: `empty value for key: ${key}`,
      });
    }

    if (en && !enKeys.has(key)) {
      addViolation(result, {
        rule: 'F-i18n-missing-en',
        file: 'frontend/src/i18n/locales/en.json',
        message: `key missing in en: ${key}`,
      });
    }
  }

  for (const key of enKeys.keys()) {
    if (!esKeys.has(key)) {
      addWarning(result, {
        rule: 'F-i18n-orphan-en',
        file: 'frontend/src/i18n/locales/en.json',
        message: `orphan key in en (not in es): ${key}`,
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-i18n-keys.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckI18nKeys()) ? 0 : 1);
}