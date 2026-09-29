import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  collectDynamicCustomProperties,
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
