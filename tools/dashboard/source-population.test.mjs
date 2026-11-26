// DH-006 / DH-007: reproducibilidad y enumeración sobre un repositorio Git temporal.
// Ejecuta los analizadores reales (copiados al fixture); no toca el repositorio.
// Uso: node --test tools/dashboard/source-population.test.mjs
import test, { after, before } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const tools = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(tools, '../..');
const shell = process.platform === 'win32' ? 'powershell' : 'pwsh';
const analyzers = [
  'analyze-backend', 'analyze-frontend', 'analyze-tests', 'analyze-architecture', 'analyze-dependencies',
  'analyze-database', 'analyze-migrations', 'analyze-technical-debt', 'analyze-security',
];
const moduleAnalyzers = ['analyze-features', 'analyze-processes', 'analyze-module-graph'];
let root;

const write = (file, content) => {
  fs.mkdirSync(path.dirname(path.join(root, file)), { recursive: true });
  fs.writeFileSync(path.join(root, file), content);
};
const git = (...args) => execFileSync('git', ['-C', root, '-c', 'user.name=t', '-c', 'user.email=t@t.invalid', '-c', 'core.autocrlf=false', ...args]);
const run = (script, cwd = root) => spawnSync(shell,
  ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(cwd, 'tools/dashboard', `${script}.ps1`)],
  { encoding: 'utf8' });
const runOk = (script) => {
  const result = run(script);
  assert.equal(result.status, 0, `${script}\n${result.stdout}\n${result.stderr}`);
};
const read = (name) => JSON.parse(fs.readFileSync(path.join(root, 'docs/ProgressDashboard/data', `${name}.json`), 'utf8').replace(/^﻿/, ''));
const outputs = () => Object.fromEntries([
  'backend-analysis', 'frontend-analysis', 'tests-analysis', 'architecture-analysis', 'dependency-analysis',
  'database-analysis', 'migration-analysis', 'technical-debt', 'security-analysis', 'features', 'processes',
].map((name) => {
  const value = read(name);
  delete value.Generated;
  return [name, value];
}));
const analyzeAll = () => { [...analyzers, ...moduleAnalyzers].forEach(runOk); return outputs(); };
const hash = (file) => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');

const app = 'backend/src/ERP.Application/Modules/Sales';
const infra = 'backend/src/ERP.Infrastructure';

before(() => {
  root = fs.realpathSync.native(fs.mkdtempSync(path.join(os.tmpdir(), 'zh-pop-')));
  for (const file of fs.readdirSync(tools).filter((name) => name.endsWith('.ps1'))) {
    fs.mkdirSync(path.join(root, 'tools/dashboard'), { recursive: true });
    fs.copyFileSync(path.join(tools, file), path.join(root, 'tools/dashboard', file));
  }
  write('.gitignore', 'node_modules/\ndist/\nbin/\nobj/\n');
  write(`${app}/UseCases/CreateSale/CreateSaleCommand.cs`, 'public record CreateSaleCommand; // Invoice\n');
  write(`${app}/Deep/Er/Est/Customer.cs`, '// TODO\nclass Customer { }\n');
  write('backend/src/ERP.Domain/Modules/Sales/Entities/Sale.cs', [
    '// using Npgsql;',
    '/*',
    'using Microsoft.EntityFrameworkCore;',
    '*/',
    '/// <summary>No depende de Microsoft.AspNetCore ni Npgsql.</summary>',
    'using System;',
    'class Sale {',
    '  string a = "using Npgsql;";',
    '  string b = @"',
    'using Microsoft.EntityFrameworkCore;',
    '";',
    '  void M() { using var Npgsql = Open(); }',
    '}',
    '',
  ].join('\n'));
  write(`${infra}/Persistence/Configurations/Sales/Deep/SaleConfiguration.cs`,
    'class SaleConfiguration : IEntityTypeConfiguration<Sale> { }\n');
  write(`${infra}/Persistence/AppDbContext.cs`, 'class AppDbContext : DbContext {\n DbSet<Sale> Sales; }\n');
  write(`${infra}/Migrations/001_Init.cs`, 'class Init { }\n');
  write('backend/src/ERP.API/Controllers/SalesController.cs', 'class SalesController { }\n');
  write('backend/src/ERP.Domain.Tests/Modules/SaleTests.cs', 'class SaleTests { }\n');
  write('frontend/src/modules/sales/SalesPage.tsx', 'export const SalesPage = 1; // TODO\n');
  write('frontend/src/modules/sales/SalesPage.test.tsx', 'test();\n');
  write('docs/ProgressDashboard/data/modules.json', '[{"id":"Sales","domainId":"sales"},{"id":"Ghost","domainId":"sales"}]\n');
  git('init', '-q');
  git('config', 'core.autocrlf', 'false');
  git('add', '-A');
  git('commit', '-q', '-m', 'fixture');
});

after(() => fs.rmSync(root, { recursive: true, force: true }));

let clean;

test('enumera en todos los niveles y registra la procedencia', () => {
  clean = analyzeAll();
  const layers = clean['architecture-analysis'].Layers;
  assert.equal(layers.Domain.Files, 1);
  assert.ok(layers.Application.Commands >= 1, 'comandos en niveles profundos');
  assert.equal(clean['database-analysis'].Database.EntityConfigurations, 1);
  assert.equal(clean['database-analysis'].Database.DbSets, 1);
  assert.equal(clean['database-analysis'].Database.Migrations, 1);
  assert.equal(clean['technical-debt'].TODO, 2);
  assert.match(clean['migration-analysis'].Migrations[0].Modified, /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/);
  assert.ok(!JSON.stringify(clean).includes(path.basename(root)), 'sin rutas absolutas');
  const manifest = read('source-population');
  assert.equal(manifest.Method, 'git-tracked-v1');
  assert.equal(manifest.Commit, git('rev-parse', 'HEAD').toString().trim());
  assert.deepEqual([manifest.TreeDirty, manifest.TreeModified], [false, []]);
  assert.equal(manifest.Analyzers['analyze-database'].FilesAnalyzed, 3);
});

test('comentarios, literales y "using var" no son importaciones prohibidas', () => {
  assert.equal(clean['dependency-analysis'].Violations.length, 0);
  assert.equal(clean['architecture-analysis'].Violations.length, 0);
});

test('dist, bin, obj y node_modules no cambian ningún indicador', () => {
  const noise = 'using Npgsql;\nusing Microsoft.EntityFrameworkCore;\n// TODO FIXME password = x AllowAnonymous ConnectionStrings DbContext Command\n' + 'x\n'.repeat(600);
  for (const file of [
    'frontend/node_modules/pkg/index.test.ts', 'frontend/dist/bundle.spec.js', 'frontend/src/dist/Out.tsx',
    'backend/src/ERP.Domain/obj/Debug/net/NoiseTests.cs', 'backend/src/ERP.Domain/bin/Noise.cs',
    `${app}/obj/NoiseCommand.cs`, `${infra}/Migrations/obj/Noise.cs`, 'backend/src/ERP.Phantom.Tests/bin/NoiseTest.cs',
    'backend/src/ERP.API/bin/appsettings.json',
  ]) write(file, noise);
  assert.deepEqual(analyzeAll(), clean);
  assert.deepEqual(read('source-population').Analyzers['analyze-security:frontend'].ExcludedUntracked, []);
});

test('una fuente nueva sin git add se excluye y se informa', () => {
  write(`${app}/NewCommand.cs`, '// TODO\n');
  assert.deepEqual(analyzeAll(), clean);
  assert.deepEqual(read('source-population').Analyzers['analyze-technical-debt:backend'].ExcludedUntracked,
    [path.normalize(`${app}/NewCommand.cs`)]);
  fs.rmSync(path.join(root, `${app}/NewCommand.cs`));
});

test('directivas prohibidas reales se detectan en cualquier forma y nivel', () => {
  write('backend/src/ERP.Domain/Modules/Sales/Entities/Deep/Bad.cs', [
    '﻿using Npgsql;',
    'global using Microsoft.AspNetCore.Mvc;',
    'using static NpgsqlTypes.NpgsqlDbType;',
    'namespace X {',
    '  using Db = Microsoft.EntityFrameworkCore.DbContext;',
    '}',
    '',
  ].join('\n'));
  write('backend/src/ERP.Application/Modules/Sales/Deep/Bad.cs', 'using global::Microsoft.AspNetCore.Http; // comentario\n');
  git('add', '-A', '--', 'backend/src');
  runOk('analyze-dependencies');
  runOk('analyze-architecture');
  const found = read('dependency-analysis').Violations.map((v) => `${v.Project}|${v.Reference}`).sort();
  assert.deepEqual(found, [
    'ERP.Application|Microsoft.AspNetCore.Http',
    'ERP.Domain|Microsoft.AspNetCore',
    'ERP.Domain|Microsoft.EntityFrameworkCore',
    'ERP.Domain|Npgsql',
    'ERP.Domain|Npgsql',
  ]);
  assert.deepEqual(read('architecture-analysis').Violations.map((v) => `${v.Rule}:${v.Count}`),
    ['EF Core dependency:1', 'ASP.NET dependency:1']);
  assert.equal(read('source-population').TreeDirty, true);
  git('reset', '-q', '--hard');
});

test('modificaciones y eliminaciones de fuentes versionadas cambian el resultado y quedan registradas', () => {
  write('frontend/src/modules/sales/SalesPage.tsx', 'export const SalesPage = 1; // TODO TODO\n');
  fs.rmSync(path.join(root, `${app}/Deep/Er/Est/Customer.cs`));
  runOk('analyze-technical-debt');
  assert.equal(read('technical-debt').TODO, 2);
  assert.equal(read('technical-debt').FilesAnalyzed, clean['technical-debt'].FilesAnalyzed - 1);
  const entry = read('source-population').Analyzers['analyze-technical-debt:backend'];
  assert.deepEqual(entry.MissingTracked, [path.normalize(`${app}/Deep/Er/Est/Customer.cs`)]);
  assert.deepEqual(entry.ModifiedInScope, [path.normalize(`${app}/Deep/Er/Est/Customer.cs`)]);
  git('reset', '-q', '--hard');
});

test('sin población determinable falla sin escribir ni escanear el disco', () => {
  const output = path.join(root, 'docs/ProgressDashboard/data/technical-debt.json');
  const previous = hash(output);
  fs.renameSync(path.join(root, '.git'), path.join(root, '.git-off'));
  try {
    const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(root, 'tools/dashboard/analyze-technical-debt.ps1')],
      { encoding: 'utf8', env: { ...process.env, GIT_CEILING_DIRECTORIES: path.dirname(root) } });
    assert.notEqual(result.status, 0);
    assert.match(result.stdout + result.stderr, /Source population/);
    assert.equal(hash(output), previous);
  } finally {
    fs.renameSync(path.join(root, '.git-off'), path.join(root, '.git'));
  }
});

test('el historial anterior se conserva y el cambio de método es visible', () => {
  const history = path.join(root, 'docs/ProgressDashboard/history');
  fs.mkdirSync(history, { recursive: true });
  const legacy = path.join(history, 'dashboard-2026-01-01-0000-v2.json');
  fs.writeFileSync(legacy, '{"Generated":"2026-01-01 00:00:00","EngineeringScore":{"Overall":50}}');
  const previous = hash(legacy);
  const data = path.join(root, 'docs/ProgressDashboard/data');
  // Insumos del resumen: copia de las salidas reales; el historial es el del fixture.
  for (const file of fs.readdirSync(path.join(repo, 'docs/ProgressDashboard/data'))) {
    if (!fs.existsSync(path.join(data, file))) fs.copyFileSync(path.join(repo, 'docs/ProgressDashboard/data', file), path.join(data, file));
  }
  fs.writeFileSync(path.join(data, 'health-score.json'), '[]');
  fs.writeFileSync(path.join(data, 'engineering-score.json'), '{"Overall":70}');
  analyzers.forEach(runOk);
  runOk('Manage-EngineeringHistory');
  const trend = read('engineering-trend');
  assert.deepEqual(trend.History.map((h) => h.Method), ['disk-scan-legacy', 'git-tracked-v1']);
  assert.deepEqual([trend.MethodologyChanges.length, trend.MethodologyChanges[0].From, trend.MethodologyChanges[0].To],
    [1, 'disk-scan-legacy', 'git-tracked-v1']);
  assert.equal(hash(legacy), previous);
  const snapshot = fs.readdirSync(history).find((name) => name !== path.basename(legacy));
  const saved = JSON.parse(fs.readFileSync(path.join(history, snapshot), 'utf8').replace(/^﻿/, ''));
  assert.equal(saved.Methodology.Method, 'git-tracked-v1');
  assert.ok(saved.Methodology.Analyzers['analyze-security:frontend'].FilesAnalyzed > 0);

  const model = read('dashboard-model-v12');
  model.Trend = trend;
  fs.writeFileSync(path.join(data, 'dashboard-model-v12.json'), JSON.stringify(model));
  runOk('analyze-dashboard-summary');
  assert.equal(read('dashboard-summary').trend.comparable, false);
  assert.equal(read('dashboard-summary').trend.status, 'Not comparable (methodology change)');

  model.Trend.History = [trend.History[1], trend.History[1]];
  fs.writeFileSync(path.join(data, 'dashboard-model-v12.json'), JSON.stringify(model));
  runOk('analyze-dashboard-summary');
  assert.deepEqual([read('dashboard-summary').trend.comparable, read('dashboard-summary').trend.status], [true, 'Stable']);
});
