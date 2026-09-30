import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  RULES,
  classifyImport,
  checkFacadeFileNaming,
  extractImportSpecifiers,
  runCheckFrontendSubscriberNaming,
} from './check-frontend-subscriber-naming.mjs';

/**
 * ZH-FRONTEND-SUBSCRIBER-NAMING-RESTORE-01 — el guard se prueba por su RESULTADO sobre fixtures
 * mínimos en memoria (owner/subscriber/shared) y sobre el repositorio real (0 violaciones).
 */
const SHARED = ['lib', 'config'];
const SALES_HOOK = 'frontend/src/modules/sales/hooks/useSalesPage.ts';

function guard(content, rel = SALES_HOOK) {
  return runCheckFrontendSubscriberNaming({ files: [{ rel, content }], sharedModules: SHARED }).violations;
}

const rules = (violations) => violations.map((v) => v.rule);

// ── PASS ─────────────────────────────────────────────────────────────────────

test('pasa: el owner consume su propio service/hook/componente', () => {
  assert.deepEqual(
    guard(
      [
        'import { salesService } from "../api/salesService";',
        'import { useSalesCustomerRepricing } from "./useSalesCustomerRepricing";',
        'import { CustomerPicker } from "../components/CustomerPicker";',
      ].join('\n'),
    ),
    [],
  );
});

test('pasa: subscriber consume la facade pública del owner (valor, tipo y re-export)', () => {
  assert.deepEqual(
    guard(
      [
        'import { warehouseLookupFacade } from "../../inventory/facades/warehouseLookupFacade";',
        'import type { WarehouseDto } from "../../inventory/facades/warehouseLookupFacade";',
        'import { type BankDto, bankLookupFacade } from "../../settings/banks/facades/bankLookupFacade";',
        'export { customerLookupFacade } from "../../masterData/facades/customerLookupFacade";',
      ].join('\n'),
    ),
    [],
  );
});

test('pasa: módulos shared, código fuera de modules/ y paquetes', () => {
  assert.deepEqual(
    guard(
      [
        'import { formatApiRequestError } from "../../lib/apiError";',
        'import { downloadBlob } from "../../../lib/download";',
        'import { ZHBtn } from "../../../components/zh/ZHForm";',
        'import { useForm } from "react-hook-form";',
      ].join('\n'),
      'frontend/src/modules/purchases/pages/PurchaseReturnListPage.tsx',
    ),
    [],
  );
});

test('pasa: CSS propio del módulo y CSS oficial del Design System (styles/, components/zh/)', () => {
  assert.deepEqual(
    guard(
      [
        'import "../styles/sales-return.css";',
        'import "./SalesPage.css";',
        'import "../../../styles/shared/items-catalog.css";',
        'import "../../../components/zh/electronicDocuments/electronic-documents.css";',
        'import logo from "../../auth/assets/logo.svg";',
      ].join('\n'),
      'frontend/src/modules/sales/pages/SalesReturnListPage.tsx',
    ),
    [],
  );
});

test('pasa: type-only desde facade y vi.mock() del arnés de test', () => {
  assert.deepEqual(
    guard(
      [
        'import type { PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";',
        'vi.mock("../../masterData/api/businessPartnerFacade", () => ({ businessPartnerFacade: {} }));',
        'vi.mock("../../payables/facades/payableLookupFacade", async (importOriginal) => ({',
        '  ...(await importOriginal<typeof import("../../payables/facades/payableLookupFacade")>()),',
        '}));',
      ].join('\n'),
      'frontend/src/modules/finance/components/ApplySupplierCreditModal.test.tsx',
    ),
    [],
  );
});

test('pasa: comentarios con rutas internas no cuentan como import', () => {
  assert.deepEqual(
    guard(
      [
        '/**',
        ' * Antes: import { stockService } from "../../inventory/stock/api/stockService";',
        ' */',
        '// import { authService } from "../../auth/api/authService";',
      ].join('\n'),
    ),
    [],
  );
});

// ── FAIL ─────────────────────────────────────────────────────────────────────

test('falla: subscriber importa un service interno del owner', () => {
  const v = guard('import { stockService } from "../../inventory/stock/api/stockService";');
  assert.deepEqual(rules(v), [RULES.internalImport]);
  assert.equal(v[0].line, 1);
  assert.match(v[0].message, /subscriber "sales" imports owner "inventory"/);
});

test('falla: subscriber importa page / hook / componente / store privados', () => {
  assert.deepEqual(
    rules(
      guard(
        [
          'import { LoginPage } from "../../auth/pages/LoginPage";',
          'import { useManualCashMovementFlow } from "../../caja/hooks/useManualCashMovementFlow";',
          'import { SupplierSearchSelect } from "../../masterData/components/SupplierSearchSelect";',
          'import { useItemUiStore } from "../../items/store/itemUiStore";',
        ].join('\n'),
      ),
    ),
    Array(4).fill(RULES.internalImport),
  );
});

test('falla: type-only, import() dinámico y typeof import() hacia internos del owner', () => {
  assert.deepEqual(
    rules(
      guard(
        [
          'import type { WarehouseDto } from "../../inventory/types";',
          'const m = await import("../../payables/api/payablesService");',
          'type T = typeof import("../../payables/api/payablesService");',
        ].join('\n'),
      ),
    ),
    Array(3).fill(RULES.internalImport),
  );
});

test('falla: "Facade" fuera de facades/, barrel de facades y subcarpeta dentro de facades/', () => {
  assert.deepEqual(
    rules(
      guard(
        [
          'import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";',
          'import { x } from "../../masterData/facades";',
          'import { y } from "../../masterData/facades/internal/helper";',
        ].join('\n'),
      ),
    ),
    Array(3).fill(RULES.internalImport),
  );
});

test('falla: import multilínea con specifier en otra línea reporta la línea del import', () => {
  const v = guard(['import { useState } from "react";', 'import {', '  branchService,', '} from "../../branches/api/branchService";'].join('\n'));
  assert.deepEqual(rules(v), [RULES.internalImport]);
  assert.equal(v[0].line, 2);
});

test('falla: naming de facade — sin sufijo, genérico o export que no coincide con el archivo', () => {
  const rel = (name) => `frontend/src/modules/items/facades/${name}`;
  const naming = (name, content = '') =>
    rules(runCheckFrontendSubscriberNaming({ files: [{ rel: rel(name), content }], sharedModules: SHARED }).violations);
  assert.deepEqual(naming('itemLookup.ts'), [RULES.facadeNaming]);
  assert.deepEqual(naming('ItemLookupFacade.ts'), [RULES.facadeNaming]);
  assert.deepEqual(naming('index.ts'), [RULES.facadeNaming]);
  assert.deepEqual(naming('lookupFacade.ts'), [RULES.facadeNaming]);
  assert.deepEqual(naming('publicFacade.ts'), [RULES.facadeNaming]);
  assert.deepEqual(naming('itemLookupFacade.ts', 'export const itemFacade = {};'), [RULES.facadeNaming]);
  assert.deepEqual(naming('itemLookupFacade.ts', 'export const itemLookupFacade = {};'), []);
  assert.deepEqual(naming('itemDetailNavigationFacade.ts', 'export function useOpenItemDetail() {}'), []);
  assert.deepEqual(naming('pendingPayablesFacade.test.ts'), []);
  assert.deepEqual(checkFacadeFileNaming(rel('supplierPickerFacade.ts'), 'export { SupplierSearchSelect } from "../components/SupplierSearchSelect";'), []);
});

test('falla: un módulo importa la hoja de estilos privada de otro módulo (css/scss)', () => {
  const v = guard(
    [
      'import "../../sales/styles/sales-return.css";',
      'import "../../auth/pages/LoginPage.css";',
      'import "../../electronicDocuments/monitor/components/electronic-documents-monitor.css";',
      'import styles from "../../caja/pages/CajaPage.module.scss";',
    ].join('\n'),
    'frontend/src/modules/purchases/pages/PurchaseReturnListPage.tsx',
  );
  assert.deepEqual(rules(v), Array(4).fill(RULES.cssImport));
  assert.match(v[0].message, /module "purchases" imports private stylesheet of module "sales"/);
});

// ── Rutas Windows / POSIX ────────────────────────────────────────────────────

test('normaliza rutas Windows y POSIX del archivo y del specifier', () => {
  const winFile = 'frontend\\src\\modules\\sales\\hooks\\useSalesPage.ts';
  for (const fromFile of [SALES_HOOK, winFile]) {
    assert.equal(
      classifyImport({ fromFile, source: '../../inventory/stock/api/stockService', sharedModules: SHARED }).kind,
      'internal',
    );
    assert.equal(
      classifyImport({ fromFile, source: '..\\..\\inventory\\facades\\stockLookupFacade', sharedModules: SHARED }).kind,
      'facade',
    );
    assert.equal(classifyImport({ fromFile, source: '../api/salesService', sharedModules: SHARED }).kind, 'own');
    assert.equal(classifyImport({ fromFile, source: '../../lib/apiError', sharedModules: SHARED }).kind, 'shared');
    assert.equal(
      classifyImport({ fromFile, source: '..\\..\\auth\\pages\\LoginPage.css', sharedModules: SHARED }).kind,
      'style',
    );
    assert.equal(classifyImport({ fromFile, source: '../styles/sales-return.css', sharedModules: SHARED }).kind, 'own');
    assert.equal(
      classifyImport({ fromFile, source: '../../../components/zh/electronicDocuments/electronic-documents.css', sharedModules: SHARED }).kind,
      'outside',
    );
  }
  assert.deepEqual(
    rules(runCheckFrontendSubscriberNaming({
      files: [{ rel: winFile, content: 'import { stockService } from "../../inventory/stock/api/stockService";' }],
      sharedModules: SHARED,
    }).violations),
    [RULES.internalImport],
  );
});

test('extrae specifiers estáticos, re-exports, type-only y dinámicos', () => {
  const found = extractImportSpecifiers(
    [
      'import a from "./a";',
      'import type { B } from "./b";',
      'export * from "./c";',
      'export { d } from "./d";',
      'import "./e.css";',
      'const f = () => import("./f");',
      'export const g = "./not-an-import";',
    ].join('\n'),
  ).map((s) => s.source);
  assert.deepEqual(found, ['./a', './b', './c', './d', './e.css', './f']);
});

// ── Repositorio real ─────────────────────────────────────────────────────────

test('el repositorio no tiene imports cross-módulo fuera de facades, CSS de otro módulo ni facades mal nombradas', () => {
  const { violations } = runCheckFrontendSubscriberNaming();
  assert.deepEqual(violations, []);
});
