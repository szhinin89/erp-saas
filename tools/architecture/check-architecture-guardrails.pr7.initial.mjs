// One-time ADR-047 activation check. Not named *.test.mjs or included in architecture:check:
// normal remediation may reduce the active list and need not retain the 36 files.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

test('initial ADR-047 activation: exact approved inventory, 36 real warnings, zero PR-7 violations', () => {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
  const adr = 'docs/decisions/ADR-047-historical-tsx-size-debt.md';
  const text = fs.readFileSync(path.join(root, adr), 'utf8');
  const baselineCommit = JSON.parse(text.match(/```json\s*([\s\S]*?)```/)[1]).baselineCommit;
  const rows = [...text.matchAll(/\| `(frontend\/src\/[^`]+\.tsx)` \| (\d+) \|/g)];
  assert.equal(rows.length, 36);
  const expected = rows.map(([, file, count]) => ({ file, maxLines: Number(count), baselineCommit, adr }));
  const active = JSON.parse(fs.readFileSync(path.join(root, 'tools/architecture/architecture-grandfather.json'), 'utf8')).tsxMaxLines500;
  assert.deepEqual(active, expected);
  const shell = process.platform === 'win32' && spawnSync('pwsh', ['-NoProfile', '-Command', 'exit 0']).error ? 'powershell' : 'pwsh';
  const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
    path.join(root, 'tools/architecture/check-architecture-guardrails.ps1'), '-SkipFrontendChunk'], { cwd: root, encoding: 'utf8', timeout: 120000 });
  if (result.error) throw result.error;
  const output = result.stdout + result.stderr;
  assert.doesNotMatch(output, /PR-7:/, output);
  assert.equal((output.match(/WARN \(grandfather\): frontend\//g) ?? []).length, 36);
  for (const e of expected) assert.ok(output.includes(`${e.file} lines=${e.maxLines} ceiling=${e.maxLines}`), e.file);
});
