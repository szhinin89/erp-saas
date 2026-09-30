import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import {
  RULES,
  findHttpUsages,
  isHttpLayer,
  runCheckFrontendHttpAccess,
} from './check-frontend-http-access.mjs';
import { REPO_ROOT } from './shared/fs-utils.mjs';

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — el guard se prueba por su RESULTADO: fixtures mínimos en
 * memoria, el repositorio real (0 violaciones) y el estado previo al ticket (HEAD), donde debe
 * encontrar exactamente los accesos HTTP directos que se migraron.
 */
const CFG = {
  allowedDirs: ['frontend/src/lib/', 'frontend/src/modules/lib/'],
  infrastructureFiles: [{ file: 'frontend/src/hooks/useAuthenticatedImage.ts', reason: 'infra' }],
};
const HOOK = 'frontend/src/modules/sales/hooks/useSalesPage.ts';

function guard(content, rel = HOOK, extra = []) {
  return runCheckFrontendHttpAccess({
    ...CFG,
    files: [
      { rel, content },
      { rel: 'frontend/src/hooks/useAuthenticatedImage.ts', content: 'import { api } from "../modules/lib/api";\napi.get(url);' },
      ...extra,
    ],
  }).violations;
}

// ── PASS ─────────────────────────────────────────────────────────────────────

test('pasa: el service api/ del owner usa los primitivos HTTP', () => {
  assert.deepEqual(
    guard('import { apiGet } from "../../lib/apiEnvelope";\nexport const s = { list: () => apiGet("/api/v1/x") };', 'frontend/src/modules/sales/api/salesService.ts'),
    [],
  );
  assert.deepEqual(
    guard('import { apiGet } from "../../../lib/apiEnvelope";', 'frontend/src/modules/items/catalog/api/catalogService.ts'),
    [],
  );
});

test('pasa: la infraestructura HTTP oficial (src/lib, src/modules/lib)', () => {
  assert.deepEqual(guard('import axios from "axios";\nexport const api = axios.create({});', 'frontend/src/modules/lib/api.ts'), []);
  assert.deepEqual(guard('import axios from "axios";\naxios.post("/api/v1/auth/refresh");', 'frontend/src/lib/session/authRefreshManager.ts'), []);
});

test('pasa: hook/página que consume el service o la facade del owner', () => {
  assert.deepEqual(
    guard(
      [
        'import { paymentMethodService } from "../api/paymentMethodService";',
        'import { accountLookupFacade } from "../../accounting/facades/accountLookupFacade";',
        'paymentMethodService.list(true); accountLookupFacade.listAccounts();',
      ].join('\n'),
    ),
    [],
  );
});

test('pasa: clasificar errores con axios no es HTTP', () => {
  assert.deepEqual(guard('import axios from "axios";\nif (axios.isAxiosError(err) && err.response?.status === 409) {}'), []);
  assert.deepEqual(guard('import { isAxiosError, type AxiosError } from "axios";'), []);
});

test('pasa: variable local "api", refetch(), método .fetch(), comentarios y tipos no son HTTP', () => {
  assert.deepEqual(
    guard(
      [
        'import type { ApiResponse } from "../../../types/api";',
        'import { readEnvelopePayload } from "../../lib/apiEnvelope";',
        'const api = useThing(); api.get("x");',
        'state.refetch(); loader.fetch();',
        '// apiGet("/api/v1/x"); fetch("/x")',
        '/* axios.get("/x") */',
      ].join('\n'),
    ),
    [],
  );
});

test('pasa: los tests quedan fuera de alcance', () => {
  assert.deepEqual(guard('import { apiGet } from "../../lib/apiEnvelope";', 'frontend/src/modules/sales/hooks/useSalesPage.test.ts'), []);
});

// ── FAIL ─────────────────────────────────────────────────────────────────────

test('falla: hook/página/componente importa los primitivos de apiEnvelope', () => {
  const v = guard('import { apiGet, readEnvelopePayload } from "../../lib/apiEnvelope";\napiGet("/api/v1/accounting/accounts");');
  assert.equal(v.length, 1);
  assert.equal(v[0].rule, RULES.outsideApi);
  assert.equal(v[0].line, 1);
  assert.match(v[0].message, /apiGet/);
});

test('falla: página importa la instancia axios "api"', () => {
  const v = guard('import { api } from "../../lib/api";\napi.post("/api/v1/auth/forgot-password", {});', 'frontend/src/modules/auth/pages/ForgotPasswordPage.tsx');
  assert.deepEqual(v.map((x) => x.rule), [RULES.outsideApi]);
});

test('falla: componente compartido fuera de api/ (components/)', () => {
  const v = guard('import { apiGet } from "../../../modules/lib/apiEnvelope";', 'frontend/src/components/items/ItemEditorModal/useItemCreationCatalogs.ts');
  assert.deepEqual(v.map((x) => x.rule), [RULES.outsideApi]);
});

test('falla: axios enviando requests, fetch global y XMLHttpRequest', () => {
  assert.equal(guard('import axios from "axios";\naxios.get("/api/v1/x");').length, 1);
  assert.equal(guard('import axios from "axios";\naxios({ url: "/x" });').length, 1);
  assert.equal(guard('import http from "axios";\nimport { get } from "axios";').length, 1);
  assert.equal(guard('await fetch("/api/v1/x");').length, 1);
  assert.equal(guard('const x = new XMLHttpRequest();').length, 1);
});

test('falla: excepción de infraestructura obsoleta (no existe o ya no hace HTTP)', () => {
  const v = runCheckFrontendHttpAccess({
    ...CFG,
    files: [{ rel: 'frontend/src/hooks/useAuthenticatedImage.ts', content: 'export const x = 1;' }],
  }).violations;
  assert.deepEqual(v.map((x) => x.rule), [RULES.staleException]);
});

test('isHttpLayer: carpeta api/ de módulo o de área; nunca un nombre de archivo', () => {
  assert.equal(isHttpLayer('frontend/src/modules/sales/api/salesService.ts', CFG.allowedDirs), true);
  assert.equal(isHttpLayer('frontend/src/modules/items/catalog/api/catalogService.ts', CFG.allowedDirs), true);
  assert.equal(isHttpLayer('frontend/src/modules/sales/hooks/api.ts', CFG.allowedDirs), false);
  assert.equal(isHttpLayer('frontend/src/modules/lib/api.ts', CFG.allowedDirs), true);
});

test('findHttpUsages: reporta la línea del import o de la llamada', () => {
  assert.deepEqual(findHttpUsages('\n\nimport { apiPost } from "../lib/apiEnvelope";').map((u) => u.line), [3]);
  assert.deepEqual(findHttpUsages('const a = 1;\nfetch("/x");').map((u) => u.line), [2]);
});

// ── Repositorio real ─────────────────────────────────────────────────────────

test('repositorio actual: 0 violaciones', () => {
  assert.deepEqual(runCheckFrontendHttpAccess().violations, []);
});

/** Último commit antes de ZH-FRONTEND-HTTP-CLIENT-SSOT-01 (estado con los accesos HTTP directos). */
const PRE_TICKET_COMMIT = '3bdbc4b0';

test('estado previo al ticket: detecta exactamente los accesos HTTP directos migrados', (t) => {
  const git = (...args) =>
    execFileSync('git', args, { cwd: REPO_ROOT, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  let files;
  try {
    // Solo archivos con algún token HTTP pueden violar la regla: el resto no hace falta leerlo.
    const candidates = git('grep', '-l', '-E', 'apiEnvelope|lib/api|axios|fetch|XMLHttpRequest', PRE_TICKET_COMMIT, '--', 'frontend/src')
      .split(/\r?\n/)
      .filter((line) => /\.tsx?$/.test(line))
      .map((line) => line.slice(PRE_TICKET_COMMIT.length + 1));
    files = candidates.map((rel) => ({ rel, content: git('show', `${PRE_TICKET_COMMIT}:${rel}`) }));
  } catch {
    t.skip(`commit ${PRE_TICKET_COMMIT} no disponible (clon superficial)`);
    return;
  }
  const flagged = [...new Set(runCheckFrontendHttpAccess({ files }).violations.map((v) => v.file))].sort();
  // Los clientes duplicados que vivían dentro de api/ (masterData/useSri*Types, emissionPointsService,
  // salesService) no son "HTTP fuera de capa": se resolvieron por el inventario por endpoint.
  assert.deepEqual(flagged, [
    'frontend/src/components/items/ItemEditorModal/useItemCreationCatalogs.ts',
    'frontend/src/modules/auth/pages/ForgotPasswordPage.tsx',
    'frontend/src/modules/auth/pages/ResetPasswordPage.tsx',
    'frontend/src/modules/auth/pages/SetupPage.tsx',
    'frontend/src/modules/cashRegisters/hooks/useCashRegistersPage.ts',
    'frontend/src/modules/finance/pages/BankAccountsPage.tsx',
    'frontend/src/modules/items/components/ItemForm/ItemFormTabs.tsx',
    'frontend/src/modules/items/detail/components/VariantsSection.tsx',
  ]);
});
