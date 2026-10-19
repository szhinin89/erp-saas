import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

/**
 * LOCAL-ERP-READINESS-01B.2 — la raíz de architecture-grandfather.json debe ser un objeto. pwsh 7
 * desenvuelve una raíz [ {...} ] de un elemento; Windows PowerShell 5.1 no. Ambos deben fallar.
 * -FrontendChunkOnly aísla la carga de configuración: la validación de raíz ocurre antes de esa
 * rama y el resto de reglas (PR-7, handlers, etc.) no se evalúa.
 */
const script = path.resolve(path.dirname(fileURLToPath(import.meta.url)), 'check-architecture-guardrails.ps1');
const realGrandfather = path.resolve(path.dirname(script), 'architecture-grandfather.json');
const shell = process.platform === 'win32' && spawnSync('pwsh', ['-NoProfile', '-Command', 'exit 0']).error ? 'powershell' : 'pwsh';
const shells = [shell, ...(process.platform === 'win32' && shell !== 'powershell' ? ['powershell'] : [])];
const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'erp-guardrails-root-'));
const dist = path.join(scratch, 'dist');
fs.mkdirSync(dist);
fs.writeFileSync(path.join(dist, 'index-fixture.js'), 'export {};\n');

after(() => {
  assert.ok(path.basename(scratch).startsWith('erp-guardrails-root-'));
  fs.rmSync(scratch, { recursive: true, force: true });
});

let sequence = 0;
function run(config, runShell, grandfatherPath) {
  const file = grandfatherPath ?? path.join(scratch, `grandfather-${sequence++}.json`);
  if (config !== undefined) fs.writeFileSync(file, config);
  const result = spawnSync(runShell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script,
    '-FrontendChunkOnly', '-DistAssetsPath', dist, '-GrandfatherPath', file], { encoding: 'utf8', timeout: 60000 });
  if (result.error) throw result.error;
  return { code: result.status, output: result.stdout + result.stderr };
}

test('raíz objeto válida (mínima y la real del repo) → PASS en cada shell disponible', () => {
  for (const runShell of shells) {
    const minimal = run('{"frontendIndexChunkMaxKb":650}', runShell);
    assert.equal(minimal.code, 0, minimal.output);
    const real = run(undefined, runShell, realGrandfather);
    assert.equal(real.code, 0, real.output);
  }
});

test('raíz no objeto → código distinto de cero en cada shell disponible', () => {
  const cases = [
    '[{"frontendIndexChunkMaxKb":650}]',
    '[{"frontendIndexChunkMaxKb":650,"handlerHandleMaxLines150":[{"file":"x","maxHandleLines":160,"baselineCommit":"x","adr":"x"}]}]',
    '[{"frontendIndexChunkMaxKb":650},{"frontendIndexChunkMaxKb":650}]',
    'null',
    '"frontendIndexChunkMaxKb"',
  ];
  for (const runShell of shells) {
    for (const config of cases) {
      const result = run(config, runShell);
      assert.notEqual(result.code, 0, `${runShell} ${config}\n${result.output}`);
      assert.match(result.output, /debe ser un objeto JSON/, `${runShell} ${config}`);
    }
  }
});

test('JSON inválido o archivo inexistente → código distinto de cero', () => {
  for (const runShell of shells) {
    assert.notEqual(run('{', runShell).code, 0);
    assert.notEqual(run(undefined, runShell, path.join(scratch, 'missing.json')).code, 0);
  }
});
