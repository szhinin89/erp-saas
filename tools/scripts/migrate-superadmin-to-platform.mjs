#!/usr/bin/env node
/**
 * One-shot migration: superadmin → platform (UI layer only).
 * Run from repo root: node tools/scripts/migrate-superadmin-to-platform.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

const LOCALE_FILES = [
  'frontend/src/i18n/locales/es.json',
  'frontend/src/i18n/locales/en.json',
  'frontend/src/i18n/locales/qu.json',
];

const I18N_KEY_REPLACEMENTS = [
  ['"app.nav.group.superadmin"', '"app.nav.group.platform"'],
  ['"app.nav.superadmin.', '"app.nav.platform.'],
  ['"login.superadmin"', '"login.platform"'],
  ['"superadmin.', '"platform.'],
];

const I18N_VALUE_REPLACEMENTS = [
  [/SuperAdmin/g, 'Platform'],
  [/Super Admin/g, 'Platform Admin'],
  [/superadmin/g, 'platform'],
  [/\/superadmin\//g, '/platform/'],
];

function walk(dir, ext, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const st = fs.statSync(full);
    if (st.isDirectory()) {
      if (['node_modules', 'dist', 'bin', 'obj'].includes(name)) continue;
      walk(full, ext, out);
    } else if (ext.some((e) => name.endsWith(e))) {
      out.push(full);
    }
  }
  return out;
}

function migrateLocales() {
  for (const rel of LOCALE_FILES) {
    const file = path.join(ROOT, rel);
    let text = fs.readFileSync(file, 'utf8');
    for (const [from, to] of I18N_KEY_REPLACEMENTS) {
      text = text.split(from).join(to);
    }
    for (const [re, to] of I18N_VALUE_REPLACEMENTS) {
      text = text.replace(re, to);
    }
    fs.writeFileSync(file, text, 'utf8');
    console.log('locale:', rel);
  }
}

function migrateSourceFiles() {
  const roots = [
    path.join(ROOT, 'frontend/src'),
    path.join(ROOT, 'frontend/e2e'),
  ];
  const files = roots.flatMap((r) => walk(r, ['.ts', '.tsx', '.css', '.mjs']));
  const skipFiles = new Set([
    'frontend/src/deployment/deploymentConfig.ts',
    'frontend/src/routes/platformRoutes.tsx',
  ]);
  const replacements = [
    ["t('superadmin.", "t('platform."],
    ['t("superadmin.', 't("platform.'],
    ["t('app.nav.superadmin", "t('app.nav.platform'"],
    ['t("app.nav.superadmin"', 't("app.nav.platform"'],
    ["t('app.nav.group.superadmin'", "t('app.nav.group.platform'"],
    ['t("app.nav.group.superadmin"', 't("app.nav.group.platform"'],
    ["t('login.superadmin'", "t('login.platform'"],
    ['t("login.superadmin"', 't("login.platform"'],
    ['/superadmin/', '/platform/'],
    ["path=\"/superadmin", "path=\"/platform"],
    ["path='/superadmin", "path='/platform"],
    ["startsWith('/superadmin')", "startsWith('/platform')"],
    ["startsWith(\"/superadmin\")", "startsWith(\"/platform\")"],
    ['SUPERADMIN_IMPERSONATION_NAME_KEY', 'PLATFORM_IMPERSONATION_NAME_KEY'],
    ['superadmin-impersonation-subscriber-name', 'platform-impersonation-subscriber-name'],
    ['superadmin-banner', 'platform-impersonation-banner'],
    ['isGlobalSuperAdmin', 'isGlobalPlatformOperator'],
    ['getSuperAdminPanelNavExtras', 'getPlatformPanelNavExtras'],
    ['mergeSuperAdminNavExtrasIntoHome', 'mergePlatformNavExtrasIntoHome'],
    ['buildGlobalSuperAdminNavGroups', 'buildGlobalPlatformNavGroups'],
    ["id: 'superadmin'", "id: 'platform'"],
    ["label: t('app.nav.superadmin')", "label: t('app.nav.platform')"],
    ['superAdminPanelEnabled', 'platformPanelEnabled'],
  ];

  for (const file of files) {
    const rel = path.relative(ROOT, file).replace(/\\/g, '/');
    if (file.includes('migrate-superadmin-to-platform.mjs')) continue;
    if (skipFiles.has(rel)) continue;
    let text = fs.readFileSync(file, 'utf8');
    let changed = false;
    for (const [from, to] of replacements) {
      if (text.includes(from)) {
        text = text.split(from).join(to);
        changed = true;
      }
    }
    if (changed) {
      fs.writeFileSync(file, text, 'utf8');
      console.log('updated:', path.relative(ROOT, file));
    }
  }
}

migrateLocales();
migrateSourceFiles();
console.log('Migration script complete.');
