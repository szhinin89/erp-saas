import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  collectDynamicCustomProperties,
  findCssLiteralViolations,
  findStaleLiteralAllowances,
  findUndefinedTokenViolations,
  runCheckDesignSystem,
} from './check-design-system.mjs';

/**
 * ZH-ARCH-DESIGN-SYSTEM-02 — F-04-token: un var(--x) es válido si --x existe en
 * design-tokens.css, está declarado en el mismo .css o lo fija en runtime un componente que
 * importa ese .css. Todo lo demás sigue fallando.
 */
const GLOBAL = new Set(['color-primary', 'space-3', 'radius-sm']);
const CSS = 'frontend/src/modules/demo/demo.css';

function tokens(content, dynamic) {
  return findUndefinedTokenViolations(GLOBAL, CSS, content, dynamic).map((v) => v.message.match(/'--([^']+)'/)[1]);
}

test('custom property local declarada y usada en el mismo archivo → PASS', () => {
  const css = `.grid { --demo-cols: 1fr 2fr; grid-template-columns: var(--demo-cols); }`;
  assert.deepEqual(tokens(css), []);
});

test('token global existente → PASS', () => {
  assert.deepEqual(tokens(`.a { color: var(--color-primary); gap: var(--space-3); }`), []);
});

test('token inexistente → FAIL', () => {
  assert.deepEqual(tokens(`.a { gap: var(--space-16); }`), ['space-16']);
});

test('typo de token global → FAIL', () => {
  assert.deepEqual(tokens(`.a { color: var(--color-primry); }`), ['color-primry']);
});

test('declaración solo en comentario no cuenta como local → FAIL', () => {
  assert.deepEqual(tokens(`/* --demo-gap: 4px; */ .a { gap: var(--demo-gap); }`), ['demo-gap']);
});

test('variable declarada solo en OTRO .css → FAIL (no hay referencias cross-file)', () => {
  // other.css declara --demo-cols, pero este archivo solo la consume.
  assert.deepEqual(tokens(`.row { grid-template-columns: var(--demo-cols); }`), ['demo-cols']);
});

test('valor dinámico fijado por el componente que importa el .css → PASS', () => {
  const dynamic = collectDynamicCustomProperties([
    {
      rel: 'frontend/src/modules/demo/Demo.tsx',
      content: `import "./demo.css";\nexport const D = (p: number) => <div style={{ "--demo-progress": \`\${p}%\` } as React.CSSProperties} />;`,
    },
  ]);
  assert.deepEqual([...(dynamic.get(CSS) ?? [])], ['demo-progress']);
  assert.deepEqual(tokens(`.fill { width: var(--demo-progress, 0%); }`, dynamic.get(CSS)), []);
});

test('valor dinámico fijado por un componente que NO importa ese .css → FAIL', () => {
  const dynamic = collectDynamicCustomProperties([
    {
      rel: 'frontend/src/modules/other/Other.tsx',
      content: `import "./other.css";\nel.style.setProperty("--demo-progress", "50%");`,
    },
  ]);
  assert.equal(dynamic.get(CSS), undefined);
  assert.deepEqual(tokens(`.fill { width: var(--demo-progress, 0%); }`, dynamic.get(CSS)), ['demo-progress']);
});

test('repo real: --batch-progress (ZhBatchProgress) y las locales ya no son F-04-token', () => {
  const tokenMessages = runCheckDesignSystem()
    .violations.filter((v) => v.rule === 'F-04-token')
    .map((v) => v.message);
  for (const name of ['batch-progress', 'account-tree-step', 'account-tree-elbow', 'account-tree-line-color', 'sfl-cols', 'sfl-col-gap']) {
    assert.ok(!tokenMessages.some((m) => m.includes(`'--${name}'`)), `--${name} no debería reportarse`);
  }
});

/**
 * ZH-DS-VISUAL-DISCIPLINE-01 — F-04-primitive / F-04-rgb / F-04-motion con allowance por conteo.
 */
const MOD_CSS = 'frontend/src/modules/demo/demo.css';
const literalRules = (content, allowances = {}, rel = MOD_CSS) =>
  findCssLiteralViolations(allowances, rel, content).map((v) => v.rule);

test('F-04-primitive: primitiva de color en módulo → FAIL; semántico → PASS', () => {
  assert.deepEqual(literalRules(`.a { color: var(--color-neutral-700); background: var(--color-white); }`), [
    'F-04-primitive',
    'F-04-primitive',
  ]);
  assert.deepEqual(literalRules(`.a { color: var(--color-text-body); background: var(--color-neutral-soft); }`), []);
});

test('F-04-rgb: rgba literal → FAIL; color-mix sobre tokens → PASS', () => {
  assert.deepEqual(literalRules(`.a { background: rgba(0, 0, 0, 0.1); }`), ['F-04-rgb']);
  assert.deepEqual(literalRules(`.a { background: color-mix(in srgb, var(--color-primary) 10%, transparent); }`), []);
});

test('F-04-motion: duración literal (también multilínea) → FAIL; tokens → PASS', () => {
  assert.deepEqual(literalRules(`.a { transition: color 0.15s; }`), ['F-04-motion']);
  assert.deepEqual(literalRules(`.a {\n  transition:\n    color 100ms ease,\n    opacity var(--transition);\n}`), ['F-04-motion']);
  assert.deepEqual(literalRules(`.a { transition: var(--transition-control); animation: x var(--motion-duration-normal) both; }`), []);
});

test('F-04-motion: exentos bucles infinite y reset reduced-motion', () => {
  assert.deepEqual(literalRules(`.s { animation: spin 0.7s linear infinite; }`), []);
  assert.deepEqual(literalRules(`* { animation-duration: 0.01ms !important; transition-duration: 0.01ms !important; }`), []);
});

test('literales en comentarios y en design-tokens.css no cuentan', () => {
  assert.deepEqual(literalRules(`/* antes: rgba(0,0,0,.1) y 0.2s */ .a { color: var(--color-text-body); }`), []);
  assert.deepEqual(literalRules(`:root { --x: rgba(0,0,0,.1); }`, {}, 'frontend/src/styles/design-tokens.css'), []);
});

test('allowance por conteo: igual → PASS; más → FAIL (deuda nueva); menos → FAIL (bajar allowance)', () => {
  const css = `.a { background: rgba(1,1,1,.1); } .b { color: rgba(2,2,2,.2); }`;
  assert.deepEqual(literalRules(css, { 'F-04-rgb': { [MOD_CSS]: 2 } }), []);
  const more = findCssLiteralViolations({ 'F-04-rgb': { [MOD_CSS]: 1 } }, MOD_CSS, css);
  assert.equal(more.length, 1);
  assert.match(more[0].message, /deuda nueva/);
  const less = findCssLiteralViolations({ 'F-04-rgb': { [MOD_CSS]: 3 } }, MOD_CSS, css);
  assert.equal(less.length, 1);
  assert.match(less[0].message, /bajar cssLiteralAllowances a 2/);
});

test('allowance a archivo inexistente → FAIL; claves $note se ignoran', () => {
  const stale = findStaleLiteralAllowances(
    { $note: 'doc', 'F-04-rgb': { 'frontend/src/gone.css': 1, [MOD_CSS]: 1 } },
    (rel) => rel === MOD_CSS,
  );
  assert.equal(stale.length, 1);
  assert.match(stale[0].message, /gone\.css/);
});

test('repo real: sin violaciones F-04-primitive/-rgb/-motion (deuda histórica congelada por allowance)', () => {
  const literal = runCheckDesignSystem().violations.filter((v) =>
    ['F-04-primitive', 'F-04-rgb', 'F-04-motion'].includes(v.rule),
  );
  assert.deepEqual(literal, []);
});
