import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadConfig, loadGrandfather } from './shared/fs-utils.mjs';
import {
  RULES,
  evaluateHits,
  findPrecisionHits,
  isInScope,
  runCheckFrontendPrecision,
} from './check-frontend-precision.mjs';

/**
 * ZH-DESIGN-SYSTEM-PRECISION-05 — el guard F-PREC se prueba por su RESULTADO: alcance y
 * tolerancias reales (config/frontend-precision.json + architecture-grandfather.json) aplicados a
 * fixtures mínimos, y el run-all real sobre el repositorio.
 */

const cfg = loadConfig('frontend-precision.json');
const allowances = {
  legacy: loadGrandfather().frontendPrecisionGrandfathered,
  exceptions: cfg.exceptions,
};
const OPEN_FILE = 'frontend/src/modules/sales/components/NewFeature.tsx';

/** Violaciones reales del guard para `source` ubicado en `file`. */
function guard(source, file = OPEN_FILE) {
  if (!isInScope(cfg, file)) return [];
  return evaluateHits(findPrecisionHits(file, source), allowances).violations.map((v) => v.rule);
}

// ── DEBE FALLAR ──────────────────────────────────────────────────────────────────────────────

test('falla: ZhDecimalInput decimals={2} nuevo (sin override contractual)', () => {
  assert.deepEqual(guard('<ZhDecimalInput decimals={2} positiveOnly />'), [RULES.decimals]);
});

test('falla: ZHMoneyValue decimals={policy.moneyDecimals} / dc.x / getPrecisionPolicy()', () => {
  assert.deepEqual(guard('<ZHMoneyValue value={x} decimals={policy.moneyDecimals} />'), [RULES.decimals]);
  assert.deepEqual(guard('<ZHNumberValue value={x} decimals={dc.quantityDecimals} />'), [RULES.decimals]);
  assert.deepEqual(
    guard('<ZHMoneyValue value={x} decimals={getPrecisionPolicy().moneyDecimals} />'),
    [RULES.decimals, RULES.policyRead],
  );
});

test('falla: formatMoney(value) / formatMoneyWithSymbol(value) sin escala', () => {
  assert.deepEqual(guard('const t = <span>{formatMoney(total)}</span>;'), [RULES.implicitFormat]);
  assert.deepEqual(
    guard('const t = `Total ${formatMoneyWithSymbol(\n  a + b,\n)}`;'),
    [RULES.implicitFormat],
  );
});

test('falla: componente numérico sin precision ni decimals (default legacy 2)', () => {
  assert.deepEqual(guard('<ZHMoneyValue value={total} emphasis="strong" />'), [RULES.implicitValue]);
  assert.deepEqual(guard('<ZhDecimalInput {...register("amount")} positiveOnly />'), [RULES.implicitValue]);
});

test('falla: value.toFixed(2) en JSX, Intl.NumberFormat local, getPrecisionPolicy() para display', () => {
  assert.deepEqual(guard('<td>{row.amount.toFixed(2)}</td>'), [RULES.toFixed]);
  assert.deepEqual(guard('const f = new Intl.NumberFormat("es-EC", { minimumFractionDigits: 2 });'), [RULES.intl]);
  assert.deepEqual(
    guard('const d = getPrecisionPolicy().moneyDecimals; <span>{formatMoney(x, d)}</span>'),
    [RULES.policyRead],
  );
});

test('falla: la deuda de un módulo cerrado NO puede crecer (una ocurrencia más que el baseline)', () => {
  const entry = allowances.legacy.find((e) => e.rule === RULES.decimals);
  assert.ok(entry, 'baseline de módulos cerrados presente');
  const source = Array.from({ length: entry.count + 1 }, () => '<ZhDecimalInput decimals={4} />').join('\n');
  const violations = guard(source, entry.file);
  assert.equal(violations.length, entry.count + 1);
  assert.ok(violations.every((r) => r === RULES.decimals));
});

test('falla: una excepción abierta tampoco puede crecer (conteo exacto, no wildcard)', () => {
  const exc = cfg.exceptions.find((e) => e.rule === RULES.toFixed);
  const source = Array.from({ length: exc.count + 1 }, (_, i) => `const v${i} = x.toFixed(1);`).join('\n');
  assert.equal(guard(source, exc.file).length, exc.count + 1);
  assert.deepEqual(guard('const v = x.toFixed(1);', OPEN_FILE), [RULES.toFixed]);
});

// ── DEBE PASAR ───────────────────────────────────────────────────────────────────────────────

test('pasa: precision semántica en read-only e input', () => {
  assert.deepEqual(guard('<ZHMoneyValue value={x} precision="money" />'), []);
  assert.deepEqual(guard('<ZHNumberValue value={q} precision="quantity" suffix=" und" />'), []);
  assert.deepEqual(guard('<ZhDecimalInput precision="purchaseUnitPrice" {...register("p")} />'), []);
});

test('pasa: usePrecisionDecimals("money") + formatMoney(value, decimals)', () => {
  const src = [
    'const moneyDecimals = usePrecisionDecimals("money");',
    'const s = `Total: ${formatMoney(total, moneyDecimals)}`;',
    'const t = formatMoneyWithSymbol(a, moneyDecimals);',
  ].join('\n');
  assert.deepEqual(guard(src), []);
});

test('pasa: override contractual con constante nombrada *_DECIMALS', () => {
  assert.deepEqual(guard('<ZhDecimalInput decimals={WAREHOUSE_CAPACITY_DECIMALS} positiveOnly />'), []);
  assert.deepEqual(guard('<ZhDecimalInput decimals={INSTALLMENT_PERCENTAGE_DECIMALS} />'), []);
});

test('pasa: cálculo sin API de precisión, metadata *Decimals y texto en comentarios', () => {
  const src = [
    'const rounded = roundToDecimals(value, decimals);',
    'const cfg = { quantityDecimals: 4, moneyDecimals: 2 };',
    '<ZhNumberInput {...register("quantityDecimals")} />',
    '// antes: formatMoney(x) y x.toFixed(2)',
    '/* <ZHMoneyValue value={x} decimals={2} /> */',
  ].join('\n');
  assert.deepEqual(guard(src), []);
});

test('pasa: tests, infraestructura del Design System y deuda legacy ya baselined', () => {
  assert.deepEqual(guard('<ZhDecimalInput decimals={2} />', 'frontend/src/modules/sales/Foo.test.tsx'), []);
  assert.deepEqual(guard('return value.toFixed(2);', 'frontend/src/components/zh/inputs/ZhDecimalInput.tsx'), []);
  const entry = allowances.legacy.find((e) => e.rule === RULES.decimals);
  const source = Array.from({ length: entry.count }, () => '<ZhDecimalInput decimals={4} />').join('\n');
  assert.deepEqual(guard(source, entry.file), []);
});

test('pasa: excepciones abiertas auditadas en su conteo exacto', () => {
  for (const exc of cfg.exceptions) {
    assert.ok(exc.reason?.length > 10, `excepción sin justificación: ${exc.file}`);
    assert.ok(!exc.file.includes('*'), `excepción con wildcard: ${exc.file}`);
  }
});

// ── Resultado real sobre el repositorio ──────────────────────────────────────────────────────

test('repositorio actual: 0 violaciones F-PREC y sin tolerancias sobrantes', () => {
  const result = runCheckFrontendPrecision();
  assert.deepEqual(result.violations, []);
  assert.deepEqual(result.warnings, []);
});

test('run-all --only frontend-precision reporta PASS', () => {
  const runAll = path.join(path.dirname(fileURLToPath(import.meta.url)), 'run-all.mjs');
  const out = spawnSync(process.execPath, [runAll, '--only', 'frontend-precision'], { encoding: 'utf8' });
  assert.match(out.stdout, /\[PASS\] frontend-precision/);
  assert.equal(out.status, 0);
});
