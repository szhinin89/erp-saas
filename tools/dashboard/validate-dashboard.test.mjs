// DH-008: capacidades transversales sin carpeta bajo Modules/ en el Quality Gate.
// Ejecuta el validador real sobre un repositorio Git temporal con copias de los datos.
// Uso: node --test tools/dashboard/validate-dashboard.test.mjs
import test, { after, before } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const tools = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(tools, '../..');
const shell = process.platform === 'win32' ? 'powershell' : 'pwsh';
const evidence = 'backend/src/ERP.Domain/Kernel/Security/SecurityRoles.cs';
let root;
let original;

const file = (relative) => path.join(root, relative);
const write = (relative, content) => {
  fs.mkdirSync(path.dirname(file(relative)), { recursive: true });
  fs.writeFileSync(file(relative), content);
};
const git = (...args) => execFileSync('git', ['-C', root, '-c', 'user.name=t', '-c', 'user.email=t@t.invalid', ...args]);
const data = (name) => `docs/ProgressDashboard/data/${name}.json`;
const read = (name) => JSON.parse(fs.readFileSync(file(data(name)), 'utf8').replace(/^﻿/, ''));
const save = (name, value) => write(data(name), JSON.stringify(value));

// Datos de partida de cada caso: sin aristas técnicas de Security, más las que se pidan.
const prepare = ({ securityEdge = false, unknown = false, code = evidence, tracked = true } = {}) => {
  fs.rmSync(file('backend'), { recursive: true, force: true });
  write('backend/src/ERP.Domain/Modules/Access/Access.cs', 'class Access { }\n');
  if (code) write(code, 'class SecurityRoles { }\n');
  git('add', '-A', '--', ...(tracked ? ['backend'] : ['backend/src/ERP.Domain/Modules']));

  const dependencies = structuredClone(original.dependencies);
  dependencies.edges = dependencies.edges.filter((e) => ![e.sourceModule, e.targetModule].includes('Security'));
  if (securityEdge) {
    dependencies.edges.push({ sourceModule: 'Security', targetModule: 'Access', dependencyType: 'Core', critical: false, observaciones: 'fixture' });
  }
  save('architecture-dependencies', dependencies);

  const roadmap = structuredClone(original.roadmap);
  if (unknown) roadmap.stages[0].modulos = [...roadmap.stages[0].modulos, 'Fantasma'];
  save('roadmap', roadmap);

  const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', file('tools/dashboard/validate-dashboard.ps1')],
    { encoding: 'utf8' });
  const checks = read('dashboard-validation').checksCompletos;
  const detail = (prefix) => checks.find((c) => c.check.startsWith(prefix));
  return { status: result.status, refs: detail('6.'), modules: detail('7.'), exceptions: detail('7b.') };
};

before(() => {
  root = fs.realpathSync.native(fs.mkdtempSync(path.join(os.tmpdir(), 'zh-val-')));
  fs.mkdirSync(file('tools/dashboard'), { recursive: true });
  for (const name of fs.readdirSync(tools).filter((n) => n.endsWith('.ps1'))) {
    fs.copyFileSync(path.join(tools, name), file(`tools/dashboard/${name}`));
  }
  fs.cpSync(path.join(repo, 'docs/ProgressDashboard/data'), file('docs/ProgressDashboard/data'), { recursive: true });
  fs.cpSync(path.join(repo, 'docs/decisions'), file('docs/decisions'), { recursive: true });
  original = { dependencies: read('architecture-dependencies'), roadmap: read('roadmap') };
  assert.ok(original.roadmap.stages.some((s) => s.modulos.includes('Security')), 'el roadmap sigue citando Security');
  assert.ok(!read('explorer-index').modules.some((m) => m.id === 'Security'), 'Security no es un módulo del explorador');
  git('init', '-q');
  git('add', '-A');
  git('commit', '-q', '-m', 'fixture');
});

after(() => fs.rmSync(root, { recursive: true, force: true }));

test('referencias de gobierno a Security con código transversal versionado no son críticas', () => {
  const result = prepare();
  assert.equal(result.modules.ok, true, result.modules.detail);
  assert.match(result.exceptions.detail, /Security/);
  assert.doesNotMatch(result.refs.detail, /'Security'/);
  assert.equal(result.status, 0);
});

test('una arista técnica de Security sigue siendo un error', () => {
  const result = prepare({ securityEdge: true });
  assert.equal(result.modules.ok, false);
  assert.match(result.modules.detail, /Security/);
  assert.match(result.refs.detail, /architecture-dependencies\.json -> sourceModule inexistente 'Security'/);
  assert.doesNotMatch(result.refs.detail, /(roadmap|blockers)\.json[^;]*'Security'/);
  assert.notEqual(result.status, 0);
});

for (const [name, options] of [
  ['sin código transversal', { code: null }],
  ['con código solo en pruebas', { code: 'backend/src/ERP.Domain.Tests/Security/SecurityTests.cs' }],
  ['con código sin versionar', { tracked: false }],
]) {
  test(`${name} la excepción no aplica`, () => {
    const result = prepare(options);
    assert.equal(result.modules.ok, false);
    assert.match(result.modules.detail, /Security/);
    assert.notEqual(result.status, 0);
  });
}

test('un módulo desconocido sigue siendo un error', () => {
  const result = prepare({ unknown: true });
  assert.equal(result.modules.ok, false);
  assert.match(result.modules.detail, /Fantasma/);
  assert.doesNotMatch(result.modules.detail, /Security/);
  assert.notEqual(result.status, 0);
});
