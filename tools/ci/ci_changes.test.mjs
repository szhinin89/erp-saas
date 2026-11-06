import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, sep } from 'node:path';
import { test } from 'node:test';
import { SELECTIVE_ENABLED, classifyFiles, createPlan, detectChanges, evaluateGate } from './ci_changes.mjs';

const sha = 'a'.repeat(40);
const options = { eventName: 'push', ref: 'refs/heads/development', sha, selectiveEnabled: true };
const plan = (files, extra = {}) => createPlan({ ...options, files, ...extra });
const outcomes = p => Object.fromEntries(Object.entries({ changes: true, architecture: true,
  backend: p.backend, frontend: p.frontend, security: true })
  .map(([job, required]) => [job, { result: required ? 'success' : 'skipped' }]));

test('stage 2 enables selection by default and preserves explicit full validation', () => {
  assert.equal(SELECTIVE_ENABLED, true);
  const p = createPlan({ files: ['STATUS.md'], eventName: 'push', sha });
  assert.equal(p.proposed, 'documentation');
  assert.equal(p.profile, 'documentation');
  assert.equal(p.backend || p.frontend, false);
  assert.equal(p.security, true);
  for (const extra of [{ selectiveEnabled: false }, { forceFull: true }]) {
    const full = createPlan({ files: ['STATUS.md'], eventName: 'push', sha, ...extra });
    assert.equal(full.profile, 'full');
    assert.equal(full.backend && full.frontend && full.security, true);
  }
});

for (const [name, files, profile] of [
  ['STATUS only', ['STATUS.md'], 'documentation'],
  ['document references', ['docs/architecture/enforcement.md', 'docs/ci/BACKEND_TESTS.md'], 'documentation'],
  ['frontend', ['frontend/src/modules/purchases/Invoice.tsx'], 'frontend'],
  ['backend', ['backend/src/ERP.Application/Modules/Sales/Invoice.cs'], 'backend'],
  ['mixed', ['frontend/src/main.tsx', 'backend/src/ERP.Domain/Invoice.cs', 'STATUS.md'], 'mixed'],
  ['identity', ['backend/src/ERP.Infrastructure/Identity/AccessTokenService.cs'], 'full'],
  ['frontend security', ['frontend/src/access/permissionUi.ts'], 'full'],
  ['session state', ['frontend/src/store/sessionStore.ts'], 'full'],
  ['migration', ['backend/src/ERP.Infrastructure/Migrations/Baseline.cs'], 'full'],
  ['workflow', ['.github/workflows/ci.yml'], 'full'],
  ['guardrail', ['tools/architecture/run-all.mjs'], 'full'],
  ['CI selector', ['tools/ci/ci_changes.mjs'], 'full'],
  ['infrastructure', ['infrastructure/docker/compose.base.yml'], 'full'],
  ['SDK', ['global.json'], 'full'],
  ['dependency', ['frontend/package-lock.json'], 'full'],
  ['unknown', ['new-subsystem/file.txt'], 'full'],
  ['executable docs', ['docs/ProgressDashboard/index.html'], 'full'],
  ['dashboard data', ['docs/ProgressDashboard/data/production.json'], 'full'],
  ['runtime help', ['frontend/src/help/content.md'], 'full'],
  ['agent policy', ['CLAUDE.md'], 'full'],
  ['empty diff', [], 'full'],
  ['case mismatch', ['status.md'], 'full'],
]) test(`classification: ${name}`, () => assert.equal(plan(files).profile, profile));

test('malformed paths never qualify for documentation', () => {
  for (const file of ['docs/ci/../file.md', '/STATUS.md', 'docs\\ci\\file.md', '', 'docs//ci/file.md']) {
    assert.deepEqual(classifyFiles([file]), ['full']);
  }
});

test('publication requires API impact and full validation on eligible events', () => {
  for (const event of [{ eventName: 'push', ref: 'refs/heads/main' },
    { eventName: 'push', ref: 'refs/heads/develop' },
    { eventName: 'pull_request', baseRef: 'main' }]) {
    for (const files of [['STATUS.md'], ['frontend/src/main.tsx'], ['.github/workflows/ci.yml']]) {
      assert.equal(plan(files, event).publish, false);
    }
    const p = plan(['backend/src/ERP.Domain/Invoice.cs'], event);
    assert.equal(p.publish, true);
    assert.equal(p.profile, 'full');
    assert.equal(evaluateGate(p, outcomes(p), sha).length, 0);
  }
  assert.equal(plan(['backend/src/ERP.Domain/Invoice.cs']).publish, false);
});

test('manual validation is full and publication is opt-in on main/develop only', () => {
  const manual = { eventName: 'workflow_dispatch', ref: 'refs/heads/main' };
  assert.equal(plan(['STATUS.md'], manual).profile, 'full');
  assert.equal(plan(['STATUS.md'], manual).publish, false);
  assert.equal(plan([], { ...manual, publishRequested: true }).publish, true);
  assert.equal(plan([], { ...manual, ref: 'refs/heads/feature', publishRequested: true }).publish, false);
});

test('release, merge queue, unknown events and uncertainty require full validation', () => {
  for (const extra of [{ ref: 'refs/heads/release/v1' }, { baseRef: 'release/v1' },
    { eventName: 'merge_group' }, { eventName: 'unknown' }, { uncertain: true }, { forceFull: true }]) {
    assert.equal(plan(['STATUS.md'], extra).profile, 'full');
  }
});

test('gate accepts only the exact planned outcomes for every profile', () => {
  for (const files of [['STATUS.md'], ['frontend/src/main.tsx'], ['backend/src/ERP.Domain/Invoice.cs'],
    ['frontend/src/main.tsx', 'backend/src/ERP.Domain/Invoice.cs'], ['global.json']]) {
    const p = plan(files);
    assert.deepEqual(evaluateGate(p, outcomes(p), sha), []);
    for (const [job, outcome] of Object.entries(outcomes(p))) {
      for (const result of ['failure', 'cancelled', 'skipped', 'success', undefined]) {
        if (result === outcome.result) continue;
        const needs = outcomes(p);
        needs[job] = { result };
        assert.ok(evaluateGate(p, needs, sha).length, `${p.profile}/${job}/${result}`);
      }
    }
  }
});

test('gate rejects missing, corrupt, downgraded, foreign-SHA and forged plans', () => {
  const p = plan(['STATUS.md']);
  for (const bad of [null, {}, { ...p, version: 2 }, { ...p, sha: 'b'.repeat(40) },
    { ...p, backend: undefined }, { ...p, publish: true }, { ...p, security: false },
    { ...p, selectiveEnabled: false }, { ...p, files: [null] },
    { ...p, files: ['global.json'] }, { ...p, ref: 'refs/heads/release/v1' }]) {
    assert.ok(evaluateGate(bad, outcomes(p), sha).length);
  }
});

function fixture(t) {
  const cwd = mkdtempSync(join(tmpdir(), 'erp-ci-changes-'));
  const git = (args, input) => execFileSync('git', args, { cwd, input, encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'] }).trim();
  git(['init', '--initial-branch=main']);
  let previous;
  // Synthetic history only in a disposable repo. Never stages/commits the workspace.
  const commit = (files, { parent, merge } = {}) => {
    let stream = 'commit refs/heads/main\ncommitter CI fixture <ci@example.invalid> 1700000000 +0000\ndata 7\nfixture\n';
    if (parent || previous) stream += `from ${parent || previous}\n`;
    if (merge) stream += `merge ${merge}\n`;
    for (const [file, content] of Object.entries(files)) {
      stream += content === null ? `D ${JSON.stringify(file)}\n`
        : `M 100644 inline ${JSON.stringify(file)}\ndata ${Buffer.byteLength(content)}\n${content}\n`;
    }
    git(['fast-import', '--quiet', '--force'], `${stream}\ndone\n`);
    previous = git(['rev-parse', 'HEAD']);
    return previous;
  };
  t.after(() => {
    const target = resolve(cwd);
    assert.ok(target.startsWith(resolve(tmpdir()) + sep) && target.split(sep).at(-1).startsWith('erp-ci-changes-'));
    rmSync(target, { recursive: true, force: true });
  });
  return { cwd, git, commit };
}

test('real Git: push includes all commits, deletes and both rename paths with spaces/newlines', t => {
  const { cwd, commit } = fixture(t);
  const before = commit({ 'old name.md': 'old', 'removed.md': 'remove', 'STATUS.md': 'initial' });
  commit({ 'frontend/src/some file.ts': 'code', 'removed.md': null });
  const head = commit({ 'old name.md': null, 'backend/src/new name.cs': 'old',
    'STATUS.md': 'final', 'frontend/src/line\nbreak.ts': 'code' });
  const result = detectChanges({ cwd, eventName: 'push', event: { before }, sha: head });
  assert.equal(result.uncertain, false);
  assert.deepEqual(new Set(result.files), new Set(['old name.md', 'removed.md', 'STATUS.md',
    'frontend/src/some file.ts', 'backend/src/new name.cs', 'frontend/src/line\nbreak.ts']));
});

test('real Git: PR uses fixed merge base and validates the synthetic merge SHA', t => {
  const { cwd, commit } = fixture(t);
  const ancestor = commit({ 'STATUS.md': 'initial' });
  const head = commit({ 'frontend/src/main.tsx': 'feature' });
  const base = commit({ 'backend/src/base.cs': 'base-only' }, { parent: ancestor });
  const merge = commit({ 'frontend/src/main.tsx': 'feature' }, { parent: base, merge: head });
  const result = detectChanges({ cwd, eventName: 'pull_request', sha: merge,
    event: { pull_request: { base: { sha: base }, head: { sha: head } } } });
  assert.equal(result.diffBase, ancestor);
  assert.deepEqual(result.files, ['frontend/src/main.tsx']);
});

test('real Git: merge and squash pushes include the resulting tree changes', t => {
  const { cwd, commit } = fixture(t);
  const before = commit({ 'STATUS.md': 'initial' });
  const feature = commit({ 'backend/src/Invoice.cs': 'feature' });
  const base = commit({ 'STATUS.md': 'base' }, { parent: before });
  const merge = commit({ 'backend/src/Invoice.cs': 'feature' }, { parent: base, merge: feature });
  assert.deepEqual(detectChanges({ cwd, eventName: 'push', sha: merge,
    event: { before: base } }).files, ['backend/src/Invoice.cs']);
  const squash = commit({ 'backend/src/Invoice.cs': 'feature' }, { parent: base });
  assert.deepEqual(detectChanges({ cwd, eventName: 'push', sha: squash,
    event: { before: base } }).files, ['backend/src/Invoice.cs']);
});

test('real Git: missing/shallow history, new branches, invalid SHA and checkout mismatch fail closed', t => {
  const { cwd, commit } = fixture(t);
  const initial = commit({ 'STATUS.md': 'initial' });
  const head = commit({ 'STATUS.md': 'changed' });
  // A shallow boundary makes merge-base unable to walk to the event base.
  writeFileSync(join(cwd, '.git', 'shallow'), `${head}\n`);
  const pr = detectChanges({ cwd, eventName: 'pull_request', sha: head,
    event: { pull_request: { base: { sha: initial }, head: { sha: head } } } });
  assert.equal(pr.uncertain, true);
  for (const before of ['b'.repeat(40), '0'.repeat(40), '--help', undefined]) {
    const detected = detectChanges({ cwd, eventName: 'push', sha: head, event: { before } });
    assert.equal(detected.uncertain, true);
    assert.equal(createPlan({ ...options, ...detected }).profile, 'full');
  }
  assert.equal(detectChanges({ cwd, eventName: 'push', sha: initial, event: { before: head } }).uncertain, true);
});

test('CLI selects documentation, ignores activation overrides and enforces the Actions gate', t => {
  const { cwd, commit } = fixture(t);
  const before = commit({ 'STATUS.md': 'initial\n' });
  const head = commit({ 'STATUS.md': 'changed\n' });
  const eventPath = join(cwd, 'event.json');
  const output = join(cwd, 'output.txt');
  writeFileSync(eventPath, JSON.stringify({ before, inputs: { selective_enabled: 'false' } }));
  const script = resolve('tools/ci/ci_changes.mjs');
  const env = { ...process.env, GITHUB_EVENT_PATH: eventPath, GITHUB_EVENT_NAME: 'push',
    GITHUB_SHA: head, GITHUB_REF: 'refs/heads/main', GITHUB_OUTPUT: output,
    CI_SELECTIVE_ENABLED: 'false',
    GITHUB_STEP_SUMMARY: join(cwd, 'summary.md') };
  const p = JSON.parse(execFileSync(process.execPath, [script], { cwd, env, encoding: 'utf8' }));
  assert.equal(p.profile, 'documentation');
  assert.equal(p.proposed, 'documentation');
  assert.equal(p.publish, false);
  assert.match(readFileSync(output, 'utf8'), /backend=false/);
  assert.match(readFileSync(output, 'utf8'), /frontend=false/);
  assert.match(readFileSync(output, 'utf8'), /architecture_profile=documentation/);
  const gateEnv = { ...env, CI_PLAN: JSON.stringify(p), CI_NEEDS: JSON.stringify(outcomes(p)) };
  execFileSync(process.execPath, [script, '--gate'], { cwd, env: gateEnv });
  assert.throws(() => execFileSync(process.execPath, [script, '--gate'], { cwd,
    env: { ...gateEnv, CI_NEEDS: '{}' }, stdio: 'pipe' }));
  assert.throws(() => execFileSync(process.execPath, [script, '--gate'], { cwd,
    env: { ...gateEnv, CI_PLAN: JSON.stringify({ ...p, selectiveEnabled: false }) }, stdio: 'pipe' }));
  writeFileSync(eventPath, JSON.stringify({ inputs: { publish_api: 'false' } }));
  const manual = JSON.parse(execFileSync(process.execPath, [script], { cwd,
    env: { ...env, GITHUB_EVENT_NAME: 'workflow_dispatch' }, encoding: 'utf8' }));
  assert.equal(manual.profile, 'full');
  assert.equal(manual.publish, false);
});

test('active CLI: push/PR selection matrix, gate outcomes and publication eligibility', t => {
  const { cwd, commit } = fixture(t);
  let before = commit({ 'STATUS.md': 'initial\n' });
  const script = resolve('tools/ci/ci_changes.mjs');
  const eventPath = join(cwd, 'event.json');
  const cases = [
    [['STATUS.md'], 'documentation', false],
    [['docs/ci/BACKEND_TESTS.md'], 'documentation', false],
    [['frontend/src/main.tsx'], 'frontend', false],
    [['backend/src/ERP.Domain/Invoice.cs'], 'full', true],
    [['frontend/src/main.tsx', 'backend/src/ERP.Domain/Invoice.cs'], 'full', true],
    [['frontend/src/access/permissionUi.ts'], 'full', false],
    [['backend/src/ERP.Infrastructure/Identity/AccessTokenService.cs'], 'full', true],
    [['.github/workflows/ci.yml'], 'full', false],
    [['infrastructure/docker/compose.base.yml'], 'full', true],
    [['new-subsystem/file.txt'], 'full', true],
  ];
  let sequence = 0;
  for (const eventName of ['push', 'pull_request']) {
    for (const [files, profile, publish] of cases) {
      const head = commit(Object.fromEntries(files.map(file => [file, `${++sequence}\n`])));
      writeFileSync(eventPath, JSON.stringify(eventName === 'push' ? { before }
        : { pull_request: { base: { sha: before }, head: { sha: head } } }));
      const env = { ...process.env, GITHUB_EVENT_PATH: eventPath, GITHUB_EVENT_NAME: eventName,
        GITHUB_SHA: head, GITHUB_REF: eventName === 'push' ? 'refs/heads/main' : 'refs/pull/1/merge',
        GITHUB_BASE_REF: eventName === 'push' ? '' : 'main',
        GITHUB_OUTPUT: join(cwd, 'output.txt'), GITHUB_STEP_SUMMARY: join(cwd, 'summary.md') };
      const p = JSON.parse(execFileSync(process.execPath, [script], { cwd, env, encoding: 'utf8' }));
      assert.equal(p.profile, profile, `${eventName}: ${files}`);
      assert.equal(p.publish, publish, `${eventName}: ${files}`);
      assert.deepEqual(new Set(p.files), new Set(files));
      execFileSync(process.execPath, [script, '--gate'], { cwd,
        env: { ...env, CI_PLAN: JSON.stringify(p), CI_NEEDS: JSON.stringify(outcomes(p)) } });
      before = head;
    }
  }
});

test('valid admitted documentation never automatically publishes on any eligible event', () => {
  const docs = ['STATUS.md', 'README.md', 'FEATURES.md', 'CONTEXT.md', 'CONTRIBUTING.md',
    'ARCHITECTURE_GATES.md', 'ERP_CORE_FREEZE.md', 'docs/architecture/security.md',
    'docs/decisions/ADR-example.md', 'docs/agent/note.md', 'docs/QA/check.md',
    'docs/evidence/check.md', 'docs/ci/BACKEND_TESTS.md'];
  for (const event of [{ eventName: 'push', ref: 'refs/heads/main' },
    { eventName: 'push', ref: 'refs/heads/develop' },
    { eventName: 'pull_request', ref: 'refs/pull/1/merge', baseRef: 'main' }]) {
    for (const files of [...docs.map(file => [file]), docs]) {
      const p = createPlan({ ...options, ...event, files });
      assert.equal(p.profile, 'documentation');
      assert.equal(p.publish, false);
      assert.deepEqual(evaluateGate(p, outcomes(p), sha), []);
    }
  }
});
