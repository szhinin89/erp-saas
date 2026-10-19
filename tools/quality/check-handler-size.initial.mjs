// One-time ADR-046 activation check. Not named *.test.mjs or included in architecture:check:
// normal remediation may reduce the active list and need not retain eight handlers.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

test('initial ADR-046 activation: exact approved inventory and eight real warnings', () => {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
  const adr = 'docs/decisions/ADR-046-historical-handler-size-debt.md';
  const text = fs.readFileSync(path.join(root, adr), 'utf8');
  const baselineCommit = JSON.parse(text.match(/```json\s*([\s\S]*?)```/)[1]).baselineCommit;
  const rows = [...text.matchAll(/\| `(backend\/src\/ERP.Application\/[^`]+)` \| (\d+) \|/g)];
  assert.equal(rows.length, 8);
  const expected = rows.map(([, file, count]) => ({ file, maxHandleLines: Number(count), baselineCommit, adr }));
  const active = JSON.parse(fs.readFileSync(path.join(root, 'tools/architecture/architecture-grandfather.json'), 'utf8')).handlerHandleMaxLines150;
  assert.deepEqual(active, expected);
  const shell = process.platform === 'win32' && spawnSync('pwsh', ['-NoProfile', '-Command', 'exit 0']).error ? 'powershell' : 'pwsh';
  const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(root, 'tools/quality/check-handler-size.ps1')], { cwd: root, encoding: 'utf8', timeout: 30000 });
  if (result.error) throw result.error;
  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.equal((result.stdout.match(/WARN \(grandfather\)/g) ?? []).length, 8);
  for (const e of expected) assert.ok(result.stdout.includes(`${e.file} Handle=${e.maxHandleLines} ceiling=${e.maxHandleLines}`));
});
