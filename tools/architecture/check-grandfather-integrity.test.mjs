import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  defaultListSpecs,
  fileExistsExactCase,
  findGrandfatherIntegrityViolations,
  runCheckGrandfatherIntegrity,
} from './check-grandfather-integrity.mjs';
import { CHECKS } from './run-all.mjs';

/**
 * ZH-ARCH-GRANDFATHER-INTEGRITY-01 — toda entrada de architecture-grandfather.json debe apuntar a
 * un archivo existente y dentro del alcance de la regla que la consume.
 */
const EXISTING = new Set([
  'backend/src/ERP.Application/Modules/Sales/UseCases/CreateSale/CreateSaleHandler.cs',
  'backend/src/ERP.API/Controllers/SalesController.cs',
  'frontend/src/modules/sales/pages/SalesPage.tsx',
  'frontend/src/modules/sales/sales.css',
  'frontend/src/lib/formatters/dateFormatters.ts',
]);
const HANDLER = 'backend/src/ERP.Application/Modules/Sales/UseCases/CreateSale/CreateSaleHandler.cs';
const CONTROLLER = 'backend/src/ERP.API/Controllers/SalesController.cs';
const DEAD = 'backend/src/ERP.Application/Modules/Ventas/UseCases/CrearVenta/CrearVentaHandler.cs';

const specs = defaultListSpecs();
const run = (grandfather) =>
  findGrandfatherIntegrityViolations(grandfather, { specs, fileExists: (rel) => EXISTING.has(rel) });

test('grandfather con todas las listas vacías → PASS', () => {
  assert.deepEqual(
    run({
      $schemaNote: 'x',
      designSystemGrandfathered: [],
      handlerHandleMaxLines150: [],
      backendControllerMaxLines: [],
      namingConventionsGrandfathered: [],
      frontendIndexChunkMaxKb: 650,
    }),
    [],
  );
});

test('archivo existente dentro del alcance → PASS', () => {
  assert.deepEqual(
    run({
      namingConventionsGrandfathered: [HANDLER],
      handlerHandleMaxLines150: [HANDLER],
      backendControllerMaxLines: [CONTROLLER],
      tsxMaxLines500: ['frontend/src/modules/sales/pages/SalesPage.tsx'],
      designSystemGrandfathered: [
        { file: 'frontend/src/modules/sales/pages/SalesPage.tsx', rules: ['F-04-btn'] },
        { file: 'frontend/src/modules/sales/sales.css', rules: ['F-04-color', 'F-04-token'] },
      ],
    }),
    [],
  );
});

test('archivo inexistente → FAIL identificando lista y ruta', () => {
  const found = run({ namingConventionsGrandfathered: [DEAD] });
  assert.equal(found.length, 1);
  assert.equal(found[0].rule, 'GF-dead-entry');
  assert.equal(found[0].file, 'tools/architecture/architecture-grandfather.json');
  assert.match(found[0].message, /Lista "namingConventionsGrandfathered"/);
  assert.ok(found[0].message.includes(`"${DEAD}"`));
});

test('varias listas → reporta solo la entrada inválida y en su lista', () => {
  const deadTsx = 'frontend/src/modules/legacy/OldPage.tsx';
  const found = run({
    namingConventionsGrandfathered: [HANDLER],
    backendControllerMaxLines: [CONTROLLER],
    designSystemGrandfathered: [
      { file: 'frontend/src/modules/sales/pages/SalesPage.tsx', rules: ['F-04-btn'] },
      { file: deadTsx, rules: ['F-04-btn'] },
    ],
  });
  assert.equal(found.length, 1);
  assert.match(found[0].message, /Lista "designSystemGrandfathered"/);
  assert.ok(found[0].message.includes(`"${deadTsx}"`));
});

test('ruta duplicada: existente → PASS; muerta → un solo hallazgo', () => {
  assert.deepEqual(run({ namingConventionsGrandfathered: [HANDLER, HANDLER] }), []);
  assert.equal(run({ namingConventionsGrandfathered: [DEAD, DEAD] }).length, 1);
});

test('forma actual del JSON: backslashes se normalizan como en isGrandfathered', () => {
  assert.deepEqual(run({ backendControllerMaxLines: [CONTROLLER.replace(/\//g, '\\')] }), []);
});

test('archivo existente fuera del alcance de la regla → FAIL', () => {
  const found = run({
    backendControllerMaxLines: [HANDLER],
    designSystemGrandfathered: [{ file: 'frontend/src/modules/sales/sales.css', rules: ['F-04-btn'] }],
  });
  assert.deepEqual(
    found.map((v) => v.rule),
    ['GF-out-of-scope', 'GF-out-of-scope'],
  );
  assert.match(found[0].message, /Lista "backendControllerMaxLines"/);
  assert.match(found[1].message, /regla "F-04-btn" no aplica/);
});

test('lista con entradas sin checker consumidor → FAIL; entrada malformada → FAIL', () => {
  assert.deepEqual(run({ someRemovedList: [] }), []);
  assert.equal(run({ someRemovedList: [HANDLER] })[0].rule, 'GF-unknown-list');
  assert.equal(run({ designSystemGrandfathered: [{ rules: ['F-04-btn'] }] })[0].rule, 'GF-invalid-entry');
  assert.equal(run({ namingConventionsGrandfathered: [42] })[0].rule, 'GF-invalid-entry');
});

test('fileExistsExactCase distingue mayúsculas y rechaza directorios', () => {
  assert.equal(fileExistsExactCase('tools/architecture/run-all.mjs'), true);
  assert.equal(fileExistsExactCase('tools/architecture/Run-All.mjs'), false);
  assert.equal(fileExistsExactCase('tools/architecture'), false);
  assert.equal(fileExistsExactCase('tools/architecture/no-existe.mjs'), false);
});

test('grandfather real del repo pasa y el check corre dentro de architecture:check', () => {
  assert.deepEqual(runCheckGrandfatherIntegrity().violations, []);
  assert.ok(CHECKS.some((c) => c.name === 'grandfather-integrity'));
});
