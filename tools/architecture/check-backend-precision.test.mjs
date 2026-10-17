import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadConfig } from './shared/fs-utils.mjs';
import {
  RULES,
  SSOT,
  evaluateHits,
  findBackendPrecisionHits,
  isInScope,
  runCheckBackendPrecision,
} from './check-backend-precision.mjs';

/**
 * B-PREC — el guard se prueba por su resultado: alcance y excepciones reales
 * (config/backend-precision.json) aplicados a fixtures mínimos, y el repositorio real contra el
 * baseline auditado. SSOT: docs/architecture/data-standards.md.
 */

const cfg = loadConfig('backend-precision.json');
const allowances = { exceptions: cfg.exceptions };
const NEW_FILE = 'backend/src/ERP.Application/Modules/Sales/Services/NewPricing.cs';

function guard(source, file = NEW_FILE) {
  if (!isInScope(cfg, file)) return [];
  return evaluateHits(findBackendPrecisionHits(file, source), allowances).violations;
}

test('falla en código nuevo: escala literal, helper propio y default FiscalPrecision configurable; cita la SSOT', () => {
  const violations = guard(`
    var a = Math.Round(total / n, 2, MidpointRounding.AwayFromZero);
    var b = decimal.Round(x, 4);
    private static decimal Round2(decimal v) => v;
    public static Line Create(decimal qty, int quantityDecimals = FiscalPrecision.Quantity) => null;
  `);
  assert.deepEqual(
    violations.map((v) => v.rule).sort(),
    [RULES.fiscalDefault, RULES.literalRound, RULES.literalRound, RULES.roundHelper].sort(),
  );
  assert.ok(violations.every((v) => v.message.includes(SSOT)));
});

test('no marca precisión de la política, constantes fijas nombradas ni comentarios; el repo cumple el baseline', () => {
  assert.deepEqual(
    guard(`
      var a = Math.Round(x, policy.AccountingDecimals, MidpointRounding.AwayFromZero);
      var b = Math.Round(x, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
      private const int Scale = FiscalPrecision.UnitCost;
      // var c = Math.Round(x, 2);
    `),
    [],
  );
  assert.deepEqual(guard('var r = Math.Round(hits / (double)total, 4);', 'backend/src/ERP.Infrastructure/Caching/X.cs'), []);
  assert.equal(runCheckBackendPrecision().violations.length, 0);
});
