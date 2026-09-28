import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
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

test('baseline versionado conserva los 187 hallazgos y el desglose aprobado', () => {
  const checkedIn = loadArchitectureBaseline();
  assert.equal(checkedIn.summary.violations, 187);
  assert.deepEqual(checkedIn.summary.byCheck, {
    'backend-subscriber-rules': 1,
    'css-prefixes': 112,
    'design-system': 26,
    'module-boundaries': 48,
  });
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