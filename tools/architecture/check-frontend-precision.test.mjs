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
 * ZH-DESIGN-SYSTEM-PRECISION-05/06 — el guard F-PREC se prueba por su RESULTADO: alcance y
 * excepciones reales (config/frontend-precision.json) aplicados a fixtures mínimos, y el run-all
 * real sobre el repositorio. Arquitectura final (06): sin deuda legacy tolerada.
 */

const cfg = loadConfig('frontend-precision.json');
const allowances = { exceptions: cfg.exceptions };
const OPEN_FILE = 'frontend/src/modules/sales/components/NewFeature.tsx';

/** Violaciones reales del guard para `source` ubicado en `file`. */
function guard(source, file = OPEN_FILE) {
  if (!isInScope(cfg, file)) return [];
  return evaluateHits(findPrecisionHits(file, source), allowances).violations.map((v) => v.rule);
}

// ── DEBE FALLAR ──────────────────────────────────────────────────────────────────────────────

test('falla: cualquier `decimals` en JSX — literal, policy o constante *_DECIMALS (06: sin overrides)', () => {
  assert.deepEqual(guard('<ZhDecimalInput precision="money" decimals={2} />'), [RULES.decimals]);
  assert.deepEqual(guard('<ZHMoneyValue value={x} precision="money" decimals={policy.moneyDecimals} />'), [RULES.decimals]);
  assert.deepEqual(guard('<ZhDecimalInput precision="money" decimals={WAREHOUSE_CAPACITY_DECIMALS} />'), [RULES.decimals]);
});

test('falla: componente numérico sin precision', () => {
  assert.deepEqual(guard('<ZHMoneyValue value={total} emphasis="strong" />'), [RULES.implicitValue]);
  assert.deepEqual(guard('<ZhDecimalInput {...register("amount")} positiveOnly />'), [RULES.implicitValue]);
  assert.deepEqual(guard('<ZhCurrencyInput value={v} />'), [RULES.implicitValue]);
});

test('falla: formatMoney(value) / formatMoneyWithSymbol(value) sin escala', () => {
  assert.deepEqual(guard('const t = <span>{formatMoney(total)}</span>;'), [RULES.implicitFormat]);
  assert.deepEqual(guard('const t = `Total ${formatMoneyWithSymbol(\n  a + b,\n)}`;'), [RULES.implicitFormat]);
});

test('falla: lectura directa de la policy (getPrecisionPolicy / getPrecisionPolicySnapshot)', () => {
  assert.deepEqual(
    guard('const d = getPrecisionPolicy().moneyDecimals; <span>{formatMoney(x, d)}</span>'),
    [RULES.policyRead],
  );
  assert.deepEqual(guard('const snap = getPrecisionPolicySnapshot();'), [RULES.policyRead]);
});

test('falla: toFixed / Intl.NumberFormat locales de presentación', () => {
  assert.deepEqual(guard('<td>{row.amount.toFixed(2)}</td>'), [RULES.toFixed]);
  assert.deepEqual(guard('const f = new Intl.NumberFormat("es-EC", { minimumFractionDigits: 2 });'), [RULES.intl]);
});

test('falla: nueva ruta alternativa — segunda tabla semántica → campo …Decimals', () => {
  assert.deepEqual(
    guard('const MY_MAP = { money: "moneyDecimals", quantity: "quantityDecimals" };'),
    [RULES.altMapping, RULES.altMapping],
  );
});

test('falla: una excepción no puede crecer (conteo exacto, no wildcard)', () => {
  const exc = cfg.exceptions.find((e) => e.rule === RULES.toFixed);
  const source = Array.from({ length: exc.count + 1 }, (_, i) => `const v${i} = x.toFixed(1);`).join('\n');
  assert.equal(guard(source, exc.file).length, exc.count + 1);
});

test('falla: Purchases/Items/Pricing ya no tienen tolerancia legacy (06)', () => {
  assert.deepEqual(guard('<ZhDecimalInput decimals={4} />', 'frontend/src/modules/purchases/pages/PurchasesPage.tsx'), [
    RULES.decimals,
    RULES.implicitValue,
  ]);
  assert.deepEqual(guard('<ZHMoneyValue value={x} />', 'frontend/src/modules/items/components/X.tsx'), [RULES.implicitValue]);
  assert.deepEqual(guard('getPrecisionPolicy();', 'frontend/src/modules/pricing/pages/X.tsx'), [RULES.policyRead]);
});

// ── DEBE PASAR ───────────────────────────────────────────────────────────────────────────────

test('pasa: precision semántica en read-only e inputs (incluidos los contratos fijos del backend)', () => {
  assert.deepEqual(guard('<ZHMoneyValue value={x} precision="money" />'), []);
  assert.deepEqual(guard('<ZHNumberValue value={q} precision="quantity" suffix=" und" />'), []);
  assert.deepEqual(guard('<ZhDecimalInput precision="purchaseUnitPrice" {...register("p")} />'), []);
  assert.deepEqual(guard('<ZhDecimalInput precision="warehouseCapacity" {...register("capacity")} />'), []);
  assert.deepEqual(guard('<ZhCurrencyInput precision="salesUnitPrice" value={v} />'), []);
});

test('pasa: usePrecisionDecimals + formatter con escala; usePrecisionPolicy + resolver en utilidades', () => {
  const src = [
    'const moneyDecimals = usePrecisionDecimals("money");',
    'const s = `Total: ${formatMoney(total, moneyDecimals)}`;',
    'const policy = usePrecisionPolicy();',
    'const d = resolvePrecisionDecimals(policy, "quantity");',
  ].join('\n');
  assert.deepEqual(guard(src), []);
});

test('pasa: cálculo sin API de precisión, metadata de configuración y comentarios', () => {
  const src = [
    'const rounded = roundToDecimals(value, decimals);',
    'const field = { name: "quantityDecimals", i18nKey: "quantity" };',
    '<ZhNumberInput {...register("quantityDecimals")} />',
    '// antes: formatMoney(x) y x.toFixed(2)',
    '/* <ZHMoneyValue value={x} decimals={2} /> */',
  ].join('\n');
  assert.deepEqual(guard(src), []);
});

test('pasa: tests e internos del Design System', () => {
  assert.deepEqual(guard('<ZhDecimalInput decimals={2} />', 'frontend/src/modules/sales/Foo.test.tsx'), []);
  assert.deepEqual(guard('<ZhDecimalInputCore decimals={resolved} />', 'frontend/src/components/zh/inputs/ZhDecimalInput.tsx'), []);
});

test('pasa: excepciones auditadas en su conteo exacto, con justificación y sin wildcard', () => {
  for (const exc of cfg.exceptions) {
    assert.ok(exc.reason?.length > 10, `excepción sin justificación: ${exc.file}`);
    assert.ok(!exc.file.includes('*'), `excepción con wildcard: ${exc.file}`);
    assert.ok(
      [RULES.policyRead, RULES.toFixed].includes(exc.rule),
      `una excepción solo cubre cálculo/métrica/payload, no presentación: ${exc.rule}`,
    );
  }
});

// ── Resultado real sobre el repositorio ──────────────────────────────────────────────────────

test('repositorio actual: 0 violaciones, 0 excepciones sobrantes y 0 grandfather de precisión', () => {
  const result = runCheckFrontendPrecision();
  assert.deepEqual(result.violations, []);
  assert.deepEqual(result.warnings, []);
  assert.equal(loadGrandfather().frontendPrecisionGrandfathered, undefined);
  assert.equal(cfg.legacyGlobs, undefined);
});

test('run-all --only frontend-precision reporta PASS', () => {
  const runAll = path.join(path.dirname(fileURLToPath(import.meta.url)), 'run-all.mjs');
  const out = spawnSync(process.execPath, [runAll, '--only', 'frontend-precision'], { encoding: 'utf8' });
  assert.match(out.stdout, /\[PASS\] frontend-precision/);
  assert.equal(out.status, 0);
});
