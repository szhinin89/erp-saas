import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadConfig } from './shared/fs-utils.mjs';
import { createCheckResult } from './shared/report-utils.mjs';
import { evaluateHits, isInScope } from './check-frontend-precision.mjs';
import {
  CHECK_NAME,
  RULES,
  findDateTimeHits,
  runCheckFrontendDateTime,
} from './check-frontend-datetime.mjs';

/**
 * ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — el guard F-DT se prueba por su RESULTADO: alcance y
 * excepciones reales (config/frontend-datetime.json) aplicados a fixtures mínimos, y el scan real
 * del repositorio (nuevas violaciones = 0).
 */
const cfg = loadConfig('frontend-datetime.json');
const OPEN_FILE = 'frontend/src/modules/finance/components/NewFeature.tsx';

function guard(source, file = OPEN_FILE) {
  if (!isInScope(cfg, file)) return [];
  return evaluateHits(
    findDateTimeHits(file, source),
    { exceptions: cfg.exceptions },
    createCheckResult(CHECK_NAME),
  ).violations.map((v) => v.rule);
}

test('falla: "hoy" derivado del día UTC', () => {
  assert.deepEqual(guard('const d = new Date().toISOString().slice(0, 10);'), [RULES.utcDay]);
  assert.deepEqual(guard('const d = new Date().toISOString().split("T")[0];'), [RULES.utcDay]);
});

test('falla: fecha de negocio parseada como instante', () => {
  assert.deepEqual(guard('const d = new Date("2026-09-25");'), [RULES.dateParse]);
  assert.deepEqual(guard('const d = new Date(issueDate + "T00:00:00");'), [RULES.dateParse]);
  assert.deepEqual(guard('const d = new Date(`${issueDate}T00:00`);'), [RULES.dateParse]);
});

test('falla: conversión manual de zona horaria fuera del helper oficial', () => {
  assert.deepEqual(guard('new Intl.DateTimeFormat("es-EC", { hour: "2-digit" });'), [RULES.manualTz, RULES.manualTz]);
  assert.deepEqual(guard('d.toLocaleDateString("es-EC", { timeZone: "UTC" });'), [RULES.manualTz, RULES.manualTz]);
  assert.deepEqual(guard('const off = d.getTimezoneOffset();'), [RULES.manualTz]);
});

test('falla: calendario en la zona del navegador', () => {
  assert.deepEqual(guard('due.setDate(due.getDate() + 30);'), [RULES.localCalendar, RULES.localCalendar]);
});

test('falla: instante presentado como fecha o con hora armada a mano', () => {
  assert.deepEqual(guard('render: (row) => formatDate(row.createdAt),'), [RULES.instantAsDate]);
  assert.deepEqual(guard('formatDate(doc?.authorizationDate)'), [RULES.instantAsDate]);
  assert.deepEqual(guard('formatDate(session.startedAtUtc)'), [RULES.instantAsDate]);
  assert.deepEqual(guard('const t = `${d.getUTCHours()}`;'), [RULES.localCalendar]);
  assert.deepEqual(guard('d.toLocaleString("es-EC")'), [RULES.manualTz]);
  assert.deepEqual(guard('<span>{new Date().toTimeString().slice(0, 8)}</span>'), [RULES.manualTz]);
  assert.deepEqual(guard('fmt({ hour: "2-digit", minute: "2-digit" })'), [RULES.manualTz, RULES.manualTz]);
});

test('pasa: fechas de negocio con formatDate e instantes con formatDateTime', () => {
  assert.deepEqual(guard('formatDate(row.issueDate); formatDate(inv.dueDate);'), []);
  assert.deepEqual(guard('formatDateTime(row.createdAt); formatDateTime(doc.authorizationDate);'), []);
});

test('pasa: helpers oficiales, instantes ISO y comentarios', () => {
  assert.deepEqual(guard('const d = todayIso(); const due = addDaysIso(d, 30);'), []);
  assert.deepEqual(guard('const savedAt = new Date().toISOString();'), []);
  assert.deepEqual(guard('// new Date().toISOString().slice(0, 10) prohibido'), []);
  assert.deepEqual(guard('const d = new Date().toISOString().slice(0, 10);', 'frontend/src/lib/formatters/dateFormatters.ts'), []);
  assert.deepEqual(guard('const d = new Date("2026-09-25");', 'frontend/src/modules/x/Foo.test.ts'), []);
});

test('repositorio real: nuevas violaciones = 0', () => {
  const result = runCheckFrontendDateTime();
  assert.deepEqual(result.violations, []);
  assert.deepEqual(result.warnings, []);
});
