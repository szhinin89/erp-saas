import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync, execFileSync } from 'node:child_process';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const script = 'tools/quality/check-handler-size.ps1';
const adr = 'docs/decisions/ADR-046-historical-handler-size-debt.md';
const handler = 'backend/src/ERP.Application/Modules/FixtureHandler.cs';
const approved = JSON.parse(fs.readFileSync(path.join(repo, 'tools/architecture/architecture-grandfather.json'), 'utf8')).handlerHandleMaxLines150;
const shell = process.platform === 'win32' && spawnSync('pwsh', ['-NoProfile', '-Command', 'exit 0']).error ? 'powershell' : 'pwsh';
const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'erp-handler-size-'));
// Fixture metadata resolves in an isolated object database, independent of repository HEAD/history.
const gitDir = path.join(scratch, 'fixture.git');
execFileSync('git', ['init', '--bare', gitDir], { stdio: 'pipe' });
const tree = execFileSync('git', ['hash-object', '-t', 'tree', '-w', '--stdin'], { cwd: gitDir, input: '', encoding: 'utf8' }).trim();
const commit = execFileSync('git', ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit-tree', tree, '-m', 'Fixture'], { cwd: gitDir, encoding: 'utf8' }).trim();
const adrText = fs.readFileSync(path.join(repo, adr), 'utf8');
const inventory = [...adrText.matchAll(/\| `(backend\/src\/ERP.Application\/[^`]+)` \| (\d+) \|/g)]
  .map(([, file, lines]) => ({ file, maxHandleLines: Number(lines), baselineCommit: JSON.parse(adrText.match(/```json\s*([\s\S]*?)```/)[1]).baselineCommit, adr }));
function assertAuthorized(entries) {
  const seen = new Set();
  for (const e of entries) {
    const original = inventory.find(i => i.file === e.file);
    assert.ok(original, `Unapproved exception: ${e.file}`);
    assert.ok(!seen.has(e.file), `Duplicate exception: ${e.file}`);
    seen.add(e.file);
    assert.ok(Number.isInteger(e.maxHandleLines) && e.maxHandleLines > 150 && e.maxHandleLines <= original.maxHandleLines, `Unapproved ceiling: ${e.file}`);
    assert.equal(e.baselineCommit, original.baselineCommit);
    assert.equal(e.adr, original.adr);
  }
}

after(() => {
  assert.ok(path.resolve(scratch).startsWith(path.resolve(os.tmpdir()) + path.sep));
  assert.ok(path.basename(scratch).startsWith('erp-handler-size-'));
  fs.rmSync(scratch, { recursive: true, force: true });
});
let sequence = 0;
function source(count, eol = '\n') {
  return ['public Task<int> Handle(', '  Request request, CancellationToken token)', '{',
    ...Array.from({ length: count - 2 }, (_, i) => i % 2 ? '' : '// fixture'), '}'].join(eol);
}
function fixture(entries = [], files = { [handler]: source(150) }) {
  const dir = path.join(scratch, String(sequence++));
  const write = (rel, value) => {
    const full = path.join(dir, rel);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, value);
  };
  write(script, fs.readFileSync(path.join(repo, script)));
  write(adr, '# Fixture ADR\n');
  write('tools/architecture/architecture-grandfather.json', JSON.stringify({ handlerHandleMaxLines150: entries }));
  for (const [name, text] of Object.entries(files)) write(name, text);
  return { dir, write, run(extra = []) {
    // The script, sources, config and Git objects all live in the isolated fixture.
    const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(dir, script), ...extra], {
      cwd: dir, env: { ...process.env, GIT_DIR: gitDir }, encoding: 'utf8', timeout: 30000,
    });
    if (result.error) throw result.error;
    return { code: result.status, output: result.stdout + result.stderr };
  } };
}
const entry = (changes = {}) => ({ file: handler, maxHandleLines: 160, baselineCommit: commit, adr, ...changes });
function expect(result, success, message) {
  assert.equal(result.code === 0, success, result.output);
  if (message) assert.match(result.output, message);
}

test('general C-03: 150 PASS, 151 FAIL; no parameter can increase 150', () => {
  expect(fixture().run(), true);
  expect(fixture([], { [handler]: source(151) }).run(), false, /Handle=151/);
  expect(fixture().run(['-MaxHandleLines', '151']), false);
});

test('active exceptions may shrink or disappear, but cannot expand the ADR authorization', () => {
  assertAuthorized(approved);
  for (let remaining = inventory.length; remaining >= 0; remaining--) assertAuthorized(inventory.slice(0, remaining));
  assertAuthorized(inventory.map(e => ({ ...e, maxHandleLines: 151 })));
  assert.throws(() => assertAuthorized([{ ...inventory[0], maxHandleLines: inventory[0].maxHandleLines + 1 }]));
  assert.throws(() => assertAuthorized([{ ...inventory[0], file: handler }]));
});

test('progressive remediation: reduce body and ceiling, retire all exceptions', () => {
  const files = Object.fromEntries(inventory.map(e => [e.file, source(e.maxHandleLines)]));
  const entries = inventory.map(e => ({ ...e, baselineCommit: commit }));
  const f = fixture(entries, files);
  for (const e of entries) {
    e.maxHandleLines = 151;
    f.write(e.file, source(151));
  }
  f.write('tools/architecture/architecture-grandfather.json', JSON.stringify({ handlerHandleMaxLines150: entries }));
  expect(f.run(), true);
  for (let remaining = entries.length - 1; remaining >= 0; remaining--) {
    f.write(entries[remaining].file, source(150));
    f.write('tools/architecture/architecture-grandfather.json', JSON.stringify({ handlerHandleMaxLines150: entries.slice(0, remaining) }));
    expect(f.run(), true);
  }
});

for (const e of inventory) test(`ceiling + 1 fails independently: ${path.basename(e.file)}`, () => {
  const f = fixture(inventory.map(e => ({ ...e, baselineCommit: commit })), Object.fromEntries(inventory.map(other => [other.file, source(other.maxHandleLines + (other === e ? 1 : 0))])));
  expect(f.run(), false, new RegExp(`Handle=${e.maxHandleLines + 1} exceeds ceiling=${e.maxHandleLines}`));
});

for (const count of [151, 160]) test(`exception ${count} passes with warning (CRLF/multiline signature/comments/blanks)`, () => {
  expect(fixture([entry()], { [handler]: source(count, '\r\n') }).run(), true, /WARN \(grandfather\)/);
});
for (const count of [149, 150]) test(`obsolete exception at ${count} fails, removal restores general limit`, () => {
  expect(fixture([entry()], { [handler]: source(count) }).run(), false, /obsoleta/);
  expect(fixture([], { [handler]: source(count) }).run(), true);
});

const malformed = [
  ['legacy string', handler], ['null', null], ['missing metadata', { file: handler }],
  ['limit 150', entry({ maxHandleLines: 150 })], ['fraction', entry({ maxHandleLines: 160.5 })],
  ['string limit', entry({ maxHandleLines: '160' })], ['unknown field', entry({ extra: true })],
  ['bad commit', entry({ baselineCommit: 'main' })], ['missing commit', entry({ baselineCommit: '0'.repeat(40) })],
  ['bad ADR', entry({ adr: 'no.md' })], ['missing ADR', entry({ adr: 'docs/decisions/ADR-046-missing.md' })],
  ['wrong case', entry({ file: handler.replace('Fixture', 'fixture') })],
  ['traversal', entry({ file: 'backend/src/ERP.Application/../Modules/FixtureHandler.cs' })],
  ['backslash', entry({ file: handler.replaceAll('/', '\\') })],
  ['wildcard', entry({ file: handler.replace('Fixture', '*') })],
  ['out of scope', entry({ file: 'frontend/FixtureHandler.cs' })],
];
for (const [name, value] of malformed) test(`invalid exception fails before size check: ${name}`, () => {
  expect(fixture([value]).run(), false);
});
test('duplicate exception fails', () => expect(fixture([entry(), entry()]).run(), false, /duplicada/));
test('deleted handler fails', () => expect(fixture([entry()], {}).run(), false));
test('second Handle fails even when both fit ceiling', () => {
  expect(fixture([entry()], { [handler]: source(151) + '\n' + source(151) }).run(), false, /exactamente un Handle/);
});
test('regression: Handle 151 plus public static Task<int> Handle 200, ceiling 160 fails', () => {
  const second = source(200).replace('public Task<int> Handle', 'public static Task<int> Handle');
  expect(fixture([entry()], { [handler]: source(151) + '\n' + second }).run(), false, /exactamente un Handle/);
});
for (const signature of [
  'public static Task<int> Handle(',
  'protected internal override async System.Threading.Tasks.Task<Result<int>> Handle(',
  'public\nstatic\nSystem.Threading.Tasks.Task<int>\nHandle\n(',
  'public Task<int> Handle<T>(',
]) test(`recognized declaration retains physical body size: ${signature.replaceAll('\n', ' ')}`, () => {
  const text = source(151).replace('public Task<int> Handle(', signature);
  expect(fixture([], { [handler]: text }).run(), false, /Handle=151/);
  expect(fixture([entry()], { [handler]: text }).run(), true, /Handle=151 ceiling=160/);
});
test('unsupported declaration in an excepted file fails conservatively', () => {
  const second = 'Task<int> IHandler.Handle(OtherRequest request)\n{\nreturn default;\n}';
  expect(fixture([entry()], { [handler]: source(151) + '\n' + second }).run(), false, /ambiguo/);
});
test('known forwarding call is not mistaken for another method', () => {
  const wrapper = 'public Task<int> HandleAsync(Request r) => Handle(r);\n';
  expect(fixture([entry()], { [handler]: wrapper + source(151) }).run(), true);
});
test('commented and quoted Handle signatures do not introduce methods', () => {
  const text = source(151) + '\n// public static Task<int> Handle(Request r) {}\n/* public Task<int> Handle(Request r) {} */\nstring text = "Handle(Request r)";';
  expect(fixture([entry()], { [handler]: text }).run(), true);
});
test('missing and uncountable Handle fail', () => {
  expect(fixture([entry()], { [handler]: 'public class FixtureHandler {}' }).run(), false, /exactamente un Handle/);
  expect(fixture([entry()], { [handler]: 'public Task<int> Handle(Request r)\n{\n' }).run(), false, /incompleto/);
});
test('JSON invalid, missing list, non-array and missing configuration fail closed', () => {
  for (const config of ['{', '{}', '{"handlerHandleMaxLines150":null}', '{"handlerHandleMaxLines150":{}}', '[{"handlerHandleMaxLines150":[]}]']) {
    const f = fixture(); f.write('tools/architecture/architecture-grandfather.json', config);
    expect(f.run(), false);
  }
  expect(fixture().run(['-GrandfatherPath', path.join(scratch, 'missing.json')]), false);
});
