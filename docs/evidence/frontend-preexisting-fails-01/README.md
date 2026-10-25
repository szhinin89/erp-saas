# Frontend — 4 FAIL preexistentes (Caja y Contabilidad)

Fecha: 2026-10-24. Clasificación: **pruebas desactualizadas** (sin bug de producción). Solo se
modificaron dos archivos de prueba; sin cambios en código de producción, reglas fiscales,
contables o de Caja, datos, migraciones, commit, push ni deploy. El trabajo pendiente de Compras
e Ítems no se tocó.

Los 4 FAIL se reprodujeron en un worktree limpio de `HEAD` (5622f63a1), por lo que eran previos
al trabajo sin commit del workspace.

## Causa raíz

| Prueba | FAIL | Causa | Corrección |
|---|---|---|---|
| `src/modules/caja/cajaI18n.test.ts` › has no untranslated JSX text… | 1 | `CajaPage.tsx` recibió en f0e25bce9 (depósito bancario) el icono Material Symbols `account_balance`. La prueba excluye ligaduras de iconos sin contenido lingüístico (`add`, `refresh`, `arrow_back`), pero no se actualizó al añadir esta. El texto visible del botón ya usa `t("caja.bankDeposit.button")`. | Añadir `account_balance` a la lista de ligaduras. La detección de texto sin traducir no se relaja para ningún otro texto. |
| `src/modules/accounting/pages/JournalEntryDetailPage.test.tsx` (3 pruebas) | 3 | El mock de `../api/accountingApi` solo exportaba `accountingApi`. Desde f42bec04f (cierre anual), la página y `ManualJournalActions` importan también `FISCAL_YEAR_CLOSING_FACT`. Vitest lanza `No "FISCAL_YEAR_CLOSING_FACT" export is defined on the mock` en el render, React desmonta el árbol y no aparece la tabla (`Unable to find role="table"`). | Mock parcial con `importOriginal` (idioma ya usado en `AdminCoreLoginPage.test.tsx`), que usa la constante real sin duplicarla. Las aserciones no cambian. |

## Resultados

| Verificación | Antes | Después |
|---|---|---|
| Dos archivos afectados | 4 FAIL / 5 PASS | 9/9 PASS, sin errores no controlados |
| Suite frontend completa | 3244/3248 (4 FAIL) | 3248/3248 PASS, 322 archivos ([resumen](frontend-tests-summary.txt)) |
| `npx tsc -b` | — | PASS |
| `npm run lint` | — | 0 errores (35 warnings `max-lines` previos) |
| `npm run build` | — | PASS |
| `npm run architecture:check` | — | 140/140 y gate PASS (0 violaciones nuevas) |

Observación: una corrida intermedia de la suite completa registró 2 timeouts por carga, en
`AccountingPostingsPage.test.tsx` (19 s) y `ResolvePendingProductsModal.flows.test.tsx` (38 s). En
aislamiento pasan 13/13, y pasaron en la corrida anterior y en la final. No forman parte de este
ticket y no se modificaron. Se registran como posible inestabilidad de pruebas pesadas bajo carga.
