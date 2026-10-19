import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync, execFileSync } from 'node:child_process';

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

test('-FrontendChunkOnly: chunk index sobre el límite → FAIL que nombra P-01, tamaño y límite; dentro → PASS', () => {
  const bigDist = path.join(scratch, 'dist-big');
  fs.mkdirSync(bigDist);
  fs.writeFileSync(path.join(bigDist, 'index-big.js'), Buffer.alloc(651 * 1024, 0x20));
  for (const runShell of shells) {
    const config = path.join(scratch, `grandfather-${sequence++}.json`);
    fs.writeFileSync(config, '{"frontendIndexChunkMaxKb":650}');
    const result = spawnSync(runShell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script,
      '-FrontendChunkOnly', '-DistAssetsPath', bigDist, '-GrandfatherPath', config], { encoding: 'utf8', timeout: 60000 });
    if (result.error) throw result.error;
    const output = result.stdout + result.stderr;
    assert.notEqual(result.status, 0, output);
    assert.match(output, /P-01: chunk index-big\.js = 651KB \(max 650KB\)/, `${runShell}\n${output}`);
    const ok = run('{"frontendIndexChunkMaxKb":650}', runShell);
    assert.equal(ok.code, 0, ok.output);
    assert.match(ok.output, /Frontend chunk guardrail OK/);
  }
});

/**
 * ADR-047 — PR-7 (TSX > 500 líneas no vacías) con techos individuales. Fixtures en un repositorio
 * temporal: los scripts reales se copian; ningún archivo del ERP se lee ni se modifica.
 */
const repo = path.resolve(path.dirname(script), '../..');
const adr047 = 'docs/decisions/ADR-047-historical-tsx-size-debt.md';
const tsx = 'frontend/src/modules/fixture/pages/FixturePage.tsx';
const otherTsx = 'frontend/src/modules/fixture/components/FixtureOther.tsx';
const gitEnv = {
  ...process.env, GIT_CONFIG_NOSYSTEM: '1',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.invalid',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.invalid',
};
// Metadatos de las fixtures en una base de objetos aislada, independiente del HEAD/historial del repo.
const sharedGit = path.join(scratch, 'fixture.git');
execFileSync('git', ['init', '--bare', sharedGit], { stdio: 'pipe' });
const emptyTree = execFileSync('git', ['hash-object', '-t', 'tree', '-w', '--stdin'], { cwd: sharedGit, input: '', encoding: 'utf8' }).trim();
const fixtureCommit = execFileSync('git', ['commit-tree', emptyTree, '-m', 'Fixture'], { cwd: sharedGit, env: gitEnv, encoding: 'utf8' }).trim();

/** `count` líneas no vacías intercaladas con líneas vacías (que la métrica vigente no cuenta). */
function tsxSource(count, eol = '\n') {
  const lines = [];
  for (let i = 0; i < count; i++) lines.push(`export const v${i} = ${i};`, ...(i % 3 === 0 ? [''] : []));
  return lines.join(eol) + eol;
}
// El script real exige las demás listas presentes (pwsh 7 falla con una lista ausente).
const baseLists = { frontendIndexChunkMaxKb: 650, handlerHandleMaxLines150: [], tsxPageWrapperMaxLines15: [], modulesLegacyRootServiceImports: [] };
const ceiling = (changes = {}) => ({ file: tsx, maxLines: 520, baselineCommit: fixtureCommit, adr: adr047, ...changes });

function pr7Fixture(entries, files = { [tsx]: tsxSource(500) }) {
  const dir = path.join(scratch, `pr7-${sequence++}`);
  const write = (rel, value) => {
    const full = path.join(dir, rel);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, value);
  };
  for (const rel of ['tools/architecture/check-architecture-guardrails.ps1', 'tools/quality/check-handler-size.ps1']) {
    write(rel, fs.readFileSync(path.join(repo, rel)));
  }
  fs.mkdirSync(path.join(dir, 'backend/src/ERP.Application'), { recursive: true });
  write(adr047, '# Fixture ADR\n');
  const config = (value) => write('tools/architecture/architecture-grandfather.json', typeof value === 'string' ? value
    : JSON.stringify({ ...baseLists, tsxMaxLines500: value }));
  config(entries);
  for (const [rel, text] of Object.entries(files)) write(rel, text);
  return {
    dir, write, config,
    run(runShell = shell, { extra = [], env = { ...gitEnv, GIT_DIR: sharedGit } } = {}) {
      const result = spawnSync(runShell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
        path.join(dir, 'tools/architecture/check-architecture-guardrails.ps1'), '-SkipFrontendChunk', ...extra],
      { cwd: dir, env, encoding: 'utf8', timeout: 60000 });
      if (result.error) throw result.error;
      return { code: result.status, output: result.stdout + result.stderr };
    },
  };
}
function expectRun(result, success, pattern) {
  assert.equal(result.code === 0, success, result.output);
  if (pattern) assert.match(result.output, pattern);
}

// Sin excepciones no se ejecuta ningún comando nativo antes del handler-size: regresión del fail-open
// por $LASTEXITCODE nulo, que salía 0 sin evaluar las reglas frontend.
test('PR-7 general: archivo nuevo 500 PASS, 501 FAIL; líneas vacías no cuentan (LF/CRLF)', () => {
  for (const runShell of shells) {
    for (const eol of ['\n', '\r\n']) {
      expectRun(pr7Fixture([], { [tsx]: tsxSource(500, eol) }).run(runShell), true);
      expectRun(pr7Fixture([], { [tsx]: tsxSource(501, eol) }).run(runShell), false, /PR-7: .*FixturePage\.tsx has 501 lines \(max 500\)/);
    }
  }
});

test('PR-7 excepción: techo exacto y 501 PASS con advertencia; techo + 1 FAIL; ≤ 500 FAIL por obsoleta', () => {
  for (const runShell of shells) {
    for (const count of [520, 501]) {
      expectRun(pr7Fixture([ceiling()], { [tsx]: tsxSource(count, '\r\n') }).run(runShell), true,
        new RegExp(`WARN \\(grandfather\\): ${tsx} lines=${count} ceiling=520 ADR=${adr047}`));
    }
    expectRun(pr7Fixture([ceiling()], { [tsx]: tsxSource(521) }).run(runShell), false, /has 521 lines, exceeds ceiling=520; ADR=docs\/decisions\/ADR-047/);
    for (const count of [500, 499]) {
      expectRun(pr7Fixture([ceiling()], { [tsx]: tsxSource(count) }).run(runShell), false, /excepcion obsoleta: \d+ lines \(max 500\); retirar entrada/);
    }
  }
});

test('PR-7 sin compensación: crecer sobre un techo falla aunque otro archivo exceptuado disminuya', () => {
  const f = pr7Fixture([ceiling(), ceiling({ file: otherTsx, maxLines: 700 })], { [tsx]: tsxSource(521), [otherTsx]: tsxSource(600) });
  expectRun(f.run(), false, /FixturePage\.tsx has 521 lines, exceeds ceiling=520/);
  f.write(tsx, tsxSource(520));
  expectRun(f.run(), true);
});

test('PR-7 retirada: sin la entrada, el archivo vuelve al límite general (500 PASS, 501 FAIL)', () => {
  const f = pr7Fixture([ceiling()], { [tsx]: tsxSource(500) });
  expectRun(f.run(), false, /excepcion obsoleta/);
  f.config([]);
  expectRun(f.run(), true);
  f.write(tsx, tsxSource(501));
  expectRun(f.run(), false, /has 501 lines \(max 500\)/);
});

test('PR-7 configuración inválida → FAIL antes de contar, en cada shell', () => {
  const { file: _file, ...withoutFile } = ceiling();
  const { maxLines: _max, ...withoutMax } = ceiling();
  const invalid = [
    [tsx], [null], [{}], [withoutFile], [withoutMax], [{ ...ceiling(), extra: true }],
    [ceiling({ maxLines: 500 })], [ceiling({ maxLines: 520.5 })], [ceiling({ maxLines: '520' })],
    [ceiling({ baselineCommit: 'main' })], [ceiling({ baselineCommit: 'f'.repeat(40) })],
    [ceiling({ adr: 'docs/decisions/ADR-047-missing.md' })], [ceiling({ adr: 'README.md' })],
    [ceiling(), ceiling()],
    [ceiling({ file: tsx.replace('FixturePage', 'fixturepage') })],
    [ceiling({ file: tsx.replaceAll('/', '\\') })],
    [ceiling({ file: 'frontend/src/modules/../modules/fixture/pages/FixturePage.tsx' })],
    [ceiling({ file: 'frontend/src/modules/fixture/pages/Missing.tsx' })],
    [ceiling({ file: 'frontend/src/modules/fixture/pages/fixture.ts' })],
    [ceiling({ file: 'tools/architecture/check-architecture-guardrails.ps1' })],
    [ceiling({ file: 'frontend/src/modules/fixture/pages/*.tsx' })],
  ];
  const files = { [tsx]: tsxSource(510), 'frontend/src/modules/fixture/pages/fixture.ts': 'export {};\n' };
  for (const runShell of shells) {
    for (const entries of invalid) {
      const result = pr7Fixture(entries, files).run(runShell);
      // Windows PowerShell 5.1 aborta antes con NativeCommandError de git para un commit inexistente: también falla.
      expectRun(result, false, /PR-7|legacy|Ruta|cat-file/);
      assert.doesNotMatch(result.output, /WARN \(grandfather\): frontend/, JSON.stringify(entries));
    }
    for (const list of ['', ',"tsxMaxLines500":null', ',"tsxMaxLines500":{}', ',"tsxMaxLines500":"x"']) {
      const f = pr7Fixture([], files);
      f.config(`${JSON.stringify(baseLists).slice(0, -1)}${list}}`);
      expectRun(f.run(runShell), false, /tsxMaxLines500 debe ser una lista/);
    }
  }
});

test('PR-7 modo diff: exceptuado cambiado que crece FAIL; configuración inválida FAIL aunque su archivo no cambie', () => {
  const f = pr7Fixture([], { [tsx]: tsxSource(520), [otherTsx]: tsxSource(10) });
  const git = (...args) => execFileSync('git', args, { cwd: f.dir, env: gitEnv, encoding: 'utf8', stdio: 'pipe' }).trim();
  git('init', '-q');
  git('add', '-A');
  git('commit', '-q', '-m', 'base');
  f.config([ceiling({ baselineCommit: git('rev-parse', 'HEAD') })]);
  git('add', '-A');
  git('commit', '-q', '-m', 'exceptions');
  const diff = { extra: ['-DiffBase', 'HEAD~1'], env: gitEnv };
  f.write(otherTsx, tsxSource(11));
  f.write(tsx, tsxSource(521));
  git('commit', '-q', '-am', 'grow');
  expectRun(f.run(shell, diff), false, /Modo diff[\s\S]*FixturePage\.tsx has 521 lines, exceeds ceiling=520/);
  f.write(tsx, tsxSource(520));
  git('commit', '-q', '-am', 'restore');
  expectRun(f.run(shell, diff), true, /Modo diff/);
  f.write(otherTsx, tsxSource(12));
  git('commit', '-q', '-am', 'only other file changed');
  f.config([ceiling({ baselineCommit: git('rev-parse', 'HEAD~3'), maxLines: 500 })]);
  git('commit', '-q', '-am', 'invalid ceiling');
  expectRun(f.run(shell, diff), false, /Techo PR-7/);
});
