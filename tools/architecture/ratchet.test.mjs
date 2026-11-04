import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { CHECKS, runCheckDocumentation } from './run-all.mjs';
import { toJsonReport } from './shared/report-utils.mjs';
import {
  compareArchitectureBaseline,
  loadArchitectureBaseline,
  parseArchitectureBaseline,
} from './ratchet.mjs';

function finding(file, message, line) {
  return {
    rule: 'F-module-boundary',
    file,
    message,
    ...(line == null ? {} : { line }),
  };
}

function results(violations) {
  return [{ name: 'module-boundaries', violations, warnings: [] }];
}

function baseline(violations) {
  const identities = new Map();
  for (const item of violations) {
    const key = JSON.stringify(['module-boundaries', item.rule, item.file, item.message]);
    const previous = identities.get(key);
    if (previous) previous.count++;
    else identities.set(key, {
      check: 'module-boundaries',
      rule: item.rule,
      file: item.file,
      message: item.message,
      count: 1,
    });
  }
  return {
    schemaVersion: 1,
    summary: {
      violations: violations.length,
      byCheck: violations.length ? { 'module-boundaries': violations.length } : {},
    },
    violations: [...identities.values()],
  };
}

function documentationFixture(t, files) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'erp-docs-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  for (const [file, source] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, file)), { recursive: true });
    fs.writeFileSync(path.join(root, file), source);
  }
  return root;
}

test('documentation: rutas relativas, raíz, UTF-8, espacios, paréntesis y anclas GitHub', (t) => {
  const root = documentationFixture(t, {
    'nested/index.md': '[Á](../Target.md#precisión) [B](/Target.md#repetido-1)\n'
      + '[C](<../con espacios.md>) [D](../archivo(v1).md) [E](../Target.md#propio)\n'
      + '[ref][destino]\n[destino]: ../Target.md#subtítulo\n[atajo]\n[atajo]: ../Target.md#precisi%C3%B3n\n',
    'Target.md': '# Precisión\n## Repetido\n## Repetido\nSubtítulo\n---\n<a id="propio"></a>\n',
    'con espacios.md': '# OK', 'archivo(v1).md': '# OK',
  });
  const result = runCheckDocumentation({ root, files: ['nested/index.md'], adapters: [] });
  assert.deepEqual(result.violations, []);
});

test('documentation: destinos, anclas, mayúsculas y escapes inválidos fallan con archivo y línea', (t) => {
  const root = documentationFixture(t, {
    'index.md': '[A](Missing.md)\n[B](target.md)\n[C](Target.md#ausente)\n[D](%ZZ.md)\n[E](../outside.md)\n',
    'Target.md': '# Existe\n',
  });
  const result = runCheckDocumentation({ root, files: ['index.md'], adapters: [] });
  assert.equal(result.violations.length, 5);
  assert.deepEqual(result.violations.map((v) => v.line), [1, 2, 3, 4, 5]);
  assert.ok(result.violations.every((v) => v.file === 'index.md'));
});

test('documentation: ignora red, comentarios, bloques e inline code; conserva línea real', (t) => {
  const root = documentationFixture(t, {
    'index.md': '[web](https://example.invalid/path)\n<!-- [A](Missing.md) -->\n'
      + '```md\n[A](Missing.md)\n```\n~~~md\n[B](Missing.md)\n~~~\n'
      + '`[C](Missing.md)`\n[D](Missing.md)\n',
  });
  const result = runCheckDocumentation({ root, files: ['index.md'], adapters: [] });
  assert.equal(result.violations.length, 1);
  assert.equal(result.violations[0].line, 10);
});

test('documentation: las entradas deben enlazar CLAUDE y no aceptar su nombre en un ejemplo', (t) => {
  const root = documentationFixture(t, { 'AGENTS.md': '`[reglas](CLAUDE.md)`\n', 'CLAUDE.md': '# Reglas\n' });
  const run = () => runCheckDocumentation({ root, files: ['AGENTS.md'], adapters: ['AGENTS.md'] });
  assert.equal(run().violations[0].rule, 'DOC-authority');
  fs.writeFileSync(path.join(root, 'AGENTS.md'), '[reglas](CLAUDE.md)\n');
  assert.deepEqual(run().violations, []);
});

test('documentation: fuente global incompleta y documento oficial ausente fallan cerrado', (t) => {
  const root = documentationFixture(t, { 'CLAUDE.md': '# Reglas\n' });
  const result = runCheckDocumentation({ root, files: ['CLAUDE.md', 'AGENTS.md'], adapters: [] });
  assert.ok(result.violations.some((v) => v.rule === 'DOC-file'));
  assert.ok(result.violations.some((v) => v.rule === 'DOC-authority' && v.message.includes('FEATURES.md')));
});

test('documentation: una jerarquía duplicada falla aunque enlace la fuente oficial', (t) => {
  const root = documentationFixture(t, {
    'AGENTS.md': '[reglas](CLAUDE.md)\n## Jerarquía\n1. Reglas locales\n2. CLAUDE.md\n',
    'CLAUDE.md': '# Reglas\n',
  });
  const result = runCheckDocumentation({ root, files: ['AGENTS.md'], adapters: ['AGENTS.md'] });
  assert.ok(result.violations.some((v) => v.message.includes('otra lista ordenada de precedencia')));
});

test('documentation: fuentes y entradas reales del repositorio pasan', () => {
  assert.ok(CHECKS.some((check) => check.name === 'documentation' && check.run === runCheckDocumentation));
  const result = runCheckDocumentation();
  assert.deepEqual(result.violations, []);
  assert.equal(compareArchitectureBaseline([result], loadArchitectureBaseline()).passed, true);
});

test('baseline idéntico pasa y los cambios de línea no alteran la identidad', () => {
  const old = finding('frontend/src/modules/a/file.tsx', 'cross-module import', 10);
  const current = finding('frontend/src/modules/a/file.tsx', 'cross-module import', 45);
  const comparison = compareArchitectureBaseline(results([current]), baseline([old]));

  assert.equal(comparison.passed, true);
  assert.equal(comparison.baselineViolations, 1);
  assert.equal(comparison.historicalViolations, 1);
  assert.equal(comparison.newViolationCount, 0);
  assert.equal(comparison.resolvedViolationCount, 0);
});

test('violación nueva falla aunque el resto coincida', () => {
  const old = finding('frontend/src/modules/a/file.tsx', 'old boundary');
  const added = finding('frontend/src/modules/b/file.tsx', 'new boundary');
  const comparison = compareArchitectureBaseline(results([old, added]), baseline([old]));

  assert.equal(comparison.passed, false);
  assert.equal(comparison.newViolationCount, 1);
  assert.equal(comparison.newViolations[0].file, added.file);
});

test('deuda reducida pasa e informa la violación resuelta', () => {
  const first = finding('frontend/src/modules/a/file.tsx', 'first');
  const second = finding('frontend/src/modules/a/other.tsx', 'second');
  const comparison = compareArchitectureBaseline(results([first]), baseline([first, second]));

  assert.equal(comparison.passed, true);
  assert.equal(comparison.historicalViolations, 1);
  assert.equal(comparison.resolvedViolationCount, 1);
  assert.equal(comparison.resolvedViolations[0].file, second.file);
});

test('reemplazar una vieja por una nueva falla aunque el total no cambie', () => {
  const old = finding('frontend/src/modules/a/file.tsx', 'old rule hit');
  const replacement = finding('frontend/src/modules/a/file.tsx', 'different rule hit');
  const comparison = compareArchitectureBaseline(results([replacement]), baseline([old]));

  assert.equal(comparison.currentViolations, comparison.baselineViolations);
  assert.equal(comparison.passed, false);
  assert.equal(comparison.newViolationCount, 1);
  assert.equal(comparison.resolvedViolationCount, 1);
});

test('una violación en archivo nuevo falla y no se confunde con una histórica', () => {
  const old = finding('frontend/src/modules/a/file.tsx', 'same rule');
  const added = finding('frontend/src/modules/a/NewFile.tsx', 'same rule');
  const comparison = compareArchitectureBaseline(results([added]), baseline([old]));

  assert.equal(comparison.passed, false);
  assert.equal(comparison.newViolations[0].file, added.file);
  assert.equal(comparison.resolvedViolations[0].file, old.file);
});

test('baseline malformado falla cerrado', () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'architecture-ratchet-'));
  const file = path.join(directory, 'baseline.json');
  try {
    fs.writeFileSync(file, '{ malformed', 'utf8');
    assert.throws(() => loadArchitectureBaseline(file), /invalid JSON/);
    assert.throws(() => parseArchitectureBaseline('{"schemaVersion":2}'), /schemaVersion must be 1/);
    const missingIdentity = baseline([]);
    assert.throws(() => parseArchitectureBaseline(JSON.stringify(missingIdentity)), /identity must declare/);
  } finally {
    fs.rmSync(directory, { recursive: true, force: true });
  }
});

test('baseline versionado vacío: deuda histórica en 0 y desglose vacío', () => {
  const checkedIn = loadArchitectureBaseline();
  assert.equal(checkedIn.summary.violations, 0);
  assert.deepEqual(checkedIn.summary.byCheck, {});
  assert.deepEqual(checkedIn.violations, []);
});

test('con baseline vacío: sin hallazgos pasa y cualquier hallazgo es nuevo', () => {
  // El baseline versionado real (vacío), no un fixture: el gate debe funcionar con 0 identidades.
  const empty = loadArchitectureBaseline();
  assert.equal(compareArchitectureBaseline(results([]), empty).passed, true);

  const comparison = compareArchitectureBaseline(results([finding('frontend/src/modules/a/file.tsx', 'new')]), empty);
  assert.equal(comparison.passed, false);
  assert.equal(comparison.newViolationCount, 1);
});

test('un duplicado idéntico cuenta como ocurrencia nueva', () => {
  const old = finding('frontend/src/modules/a/file.tsx', 'duplicate selector');
  const comparison = compareArchitectureBaseline(results([old, { ...old }]), baseline([old]));

  assert.equal(comparison.passed, false);
  assert.equal(comparison.newViolationCount, 1);
});

test('JSON separa el gate ratchet del resultado arquitectónico bruto', () => {
  const violation = finding('frontend/src/modules/a/file.tsx', 'historical');
  const comparison = compareArchitectureBaseline(results([violation]), baseline([violation]));
  const report = toJsonReport(results([violation]), { architectureScore: 0, status: 'critical' }, comparison);

  assert.equal(report.passed, true);
  assert.equal(report.rawPassed, false);
  assert.equal(report.ratchet.historicalViolations, 1);
  assert.equal(report.ratchet.newViolationCount, 0);
});
