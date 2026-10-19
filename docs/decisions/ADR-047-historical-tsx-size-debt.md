# ADR-047 — Excepción temporal para deuda histórica de tamaño TSX (PR-7)

## Status

Accepted — aprobado por el propietario para implementación controlada. Fecha: 2026-10-19.
Clasificación: STRUCTURAL DEBT. Decisión limitada al guardrail PR-7; no autoriza
refactors de componentes ni reabrir módulos CLOSED/FROZEN.

## Context

La regla [PR-7 / R-01](../architecture/pr-rules-catalog.md#r-01--tamaño-máximo-de-componentepágina)
bloquea un PR cuando un `.tsx` supera 500 líneas. La comprueba la sección PR-7 de
`tools/architecture/check-architecture-guardrails.ps1`.

El HEAD de referencia es `e38a0a682d6339ec7f230c5b890c4278f3464d10`. Tras ADR-046
y LOCAL-ERP-READINESS-01B, el guardrail completo falla exclusivamente por 36
infracciones PR-7 históricas. No se ha demostrado un defecto funcional asociado y
varios archivos pertenecen a módulos CLOSED/FROZEN: un refactor incidental no está
justificado.

En el HEAD base, `tsxMaxLines500` estaba vacío y solo admitía rutas: incorporarlas
directamente concedería una dispensa sin techo, como ocurría con
`handlerHandleMaxLines150` antes de ADR-046.

**Métrica vigente (sin cambios).** El verificador cuenta líneas **no vacías**
(`(Get-Content | Measure-Object -Line).Lines`). El catálogo describía `wc -l`
(líneas físicas); esa descripción era inexacta y se corrige en el catálogo. Pasar
a líneas físicas añadiría nueve infracciones nuevas y es otro proyecto técnico.

## Decision

### Alcance de PR-7 (pruebas y configuración)

Sin cambio respecto al comportamiento real del verificador:

| Tipo | ¿Sujeto a PR-7? |
|---|---|
| Componentes, páginas y hooks `.tsx` bajo `frontend/src/**` | Sí |
| Pruebas `*.test.tsx` bajo `frontend/src/**` | Sí |
| Configuración/rutas `.tsx` (p. ej. `frontend/src/routes/*.tsx`) | Sí |
| Archivos `.ts` (hooks, api, `*.test.ts`, `vite/vitest/playwright.config.ts`, e2e) | No |
| `.tsx` fuera de `frontend/src` | No |

El inventario contiene 26 componentes/páginas/hooks, 9 pruebas y 1 archivo de
configuración de rutas.

### Inventario cerrado y techos individuales

Rutas relativas al repositorio, sin comodines. Techo = conteo en el HEAD base, sin
margen. La columna Área es solo la ubicación; el estado CLOSED/FROZEN de cada
módulo lo definen `STATUS.md`/`FEATURES.md` y este ADR no lo reclasifica.

| Archivo | Conteo en HEAD base / techo | Tipo | Área |
|---|---:|---|---|
| `frontend/src/components/ReportPageTemplate.tsx` | 546 | Componente | components |
| `frontend/src/components/items/ItemEditorModal/ItemEditorForm.tsx` | 652 | Componente | components/items |
| `frontend/src/components/items/ItemEditorModal/ItemEditorModal.test.tsx` | 682 | Prueba | components/items |
| `frontend/src/components/zh/inputs/ZhDecimalInput.test.tsx` | 787 | Prueba | components/zh |
| `frontend/src/modules/access/users/pages/UserConfigPage.test.tsx` | 723 | Prueba | access |
| `frontend/src/modules/accounting/pages/AccountingPostingsPage.tsx` | 507 | Página | accounting |
| `frontend/src/modules/accounting/pages/ChartOfAccountsPage.tsx` | 588 | Página | accounting |
| `frontend/src/modules/branches/components/BranchGeneralSection.tsx` | 550 | Componente | branches |
| `frontend/src/modules/caja/hooks/useCajaPage.test.tsx` | 1390 | Prueba | caja |
| `frontend/src/modules/caja/pages/CajaPage.tsx` | 886 | Página | caja |
| `frontend/src/modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx` | 568 | Página | configuracion |
| `frontend/src/modules/configuracion/empresa/sections/BrandingSettingsSection.tsx` | 585 | Componente | configuracion |
| `frontend/src/modules/expenses/components/ExpenseRetentionSection.tsx` | 503 | Componente | expenses |
| `frontend/src/modules/expenses/pages/ExpenseDocumentFormPage.retention.test.tsx` | 514 | Prueba | expenses |
| `frontend/src/modules/expenses/pages/ExpenseDocumentFormPage.tsx` | 692 | Página | expenses |
| `frontend/src/modules/initialLoad/components/OpeningReconciliationCard.test.tsx` | 533 | Prueba | initialLoad |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.tsx` | 518 | Página | inventory |
| `frontend/src/modules/items/catalog/wizard/TreeEditor.tsx` | 506 | Componente | items |
| `frontend/src/modules/items/detail/components/CollectionSection.tsx` | 923 | Componente | items |
| `frontend/src/modules/masterData/components/MasterDataPartnerWizard.test.tsx` | 809 | Prueba | masterData |
| `frontend/src/modules/masterData/components/MasterDataPartnerWizard.tsx` | 647 | Componente | masterData |
| `frontend/src/modules/masterData/pages/MasterDataBusinessPartnerDetailPage.tsx` | 1221 | Página | masterData |
| `frontend/src/modules/masterData/pages/MasterDataSuppliersPage.tsx` | 811 | Página | masterData |
| `frontend/src/modules/pricing/pages/PriceListExceptionsTab.tsx` | 568 | Página | pricing |
| `frontend/src/modules/pricing/pages/PriceListsPage.tsx` | 501 | Página | pricing |
| `frontend/src/modules/purchases/components/PurchaseReceptionXmlViewModal.tsx` | 558 | Componente | purchases |
| `frontend/src/modules/purchases/components/ResolvePendingProductsModal.tsx` | 737 | Componente | purchases |
| `frontend/src/modules/purchases/pages/PurchaseCreditNoteDetailPage.tsx` | 667 | Página | purchases |
| `frontend/src/modules/purchases/pages/PurchaseCreditNoteFormPage.tsx` | 599 | Página | purchases |
| `frontend/src/modules/purchases/pages/PurchasesPage.tsx` | 3616 | Página | purchases |
| `frontend/src/modules/sales/components/PaymentMethodsSection.tsx` | 504 | Componente | sales |
| `frontend/src/modules/sales/components/SalesInvoiceLineGridRow.tsx` | 582 | Componente | sales |
| `frontend/src/modules/sales/pages/SalesPage.moneyValues.test.tsx` | 597 | Prueba | sales |
| `frontend/src/modules/sales/pages/SalesPage.ride.test.tsx` | 533 | Prueba | sales |
| `frontend/src/modules/sales/pages/SalesPage.tsx` | 773 | Página | sales |
| `frontend/src/routes/catalogRoutes.tsx` | 602 | Configuración/rutas | routes |

Solo se exceptúa el tamaño de cada archivo. No se exceptúan otras reglas
(PR-1…PR-18, Design System, seguridad, aislamiento) ni se autoriza intervenir
módulos CLOSED/FROZEN.

### Representación y fuente ejecutable única

Los elementos de la lista existente `tsxMaxLines500` pasan de rutas a objetos. No
se crea otra lista ni se aceptan cadenas que concedan una dispensa ilimitada:

```json
{
  "file": "frontend/src/modules/purchases/pages/PurchasesPage.tsx",
  "maxLines": 3616,
  "baselineCommit": "e38a0a682d6339ec7f230c5b890c4278f3464d10",
  "adr": "docs/decisions/ADR-047-historical-tsx-size-debt.md"
}
```

El JSON es la única configuración ejecutable; la tabla registra la decisión y su
evidencia. Toda ampliación del inventario o aumento de techo requiere otra decisión
explícita; un fallo no autoriza recalibrar la línea base.

### Algoritmo aprobado

1. Validar toda la configuración antes de contar, también en modo diff: ruta exacta
   y única, relativa, bajo `frontend/src/`, extensión `.tsx`, sin `..`, comodines ni
   diferencias de caso; archivo existente; `maxLines` entero mayor que 500; ADR
   existente y `baselineCommit` de 40 hex que resuelve a un commit. Rechazar cadenas
   legacy, metadatos incompletos o desconocidos y duplicados.
2. Archivo no exceptuado: más de 500 líneas falla (sin cambios).
3. Archivo exceptuado: por encima de `maxLines` falla con ruta, conteo, techo y ADR;
   entre 501 y `maxLines` emite advertencia visible; con 500 o menos falla por
   excepción obsoleta hasta retirar la entrada en el mismo cambio.
4. Se conservan los modos actuales: en PR se evalúan los archivos cambiados; en
   push a `main`, todo el árbol. Un archivo reducido figura entre los cambiados; uno
   borrado o renombrado falla en la validación del punto 1.
5. Un error de lectura, configuración o conteo falla; nunca omite el control.
6. No hay compensación entre archivos. Cada remediación parcial debe reducir
   también su `maxLines` en el mismo cambio para cerrar el margen recuperado.

### Remediación y retirada

Sin fecha artificial. Retirar una entrada requiere: archivo con 500 líneas o menos,
regresiones del módulo PASS (`npm run lint`, `npx tsc -b`, `npm run build`, vitest
focal), eliminación de su objeto del JSON, guardrail e integridad PASS y diff review.
En módulos CLOSED/FROZEN se requiere además autorización expresa del propietario;
esta excepción no la concede. Reducir solo líneas vacías no cambia el conteo; una
reducción solo de formato no demuestra mejora estructural. Una ruta nueva no hereda
la excepción.

### Pruebas de aceptación del mecanismo

Fixtures aisladas (repositorio temporal); no se modifican los 36 archivos.

- General: archivo nuevo de 500 líneas PASS, 501 FAIL; líneas vacías no cuentan;
  CRLF y LF equivalentes.
- Excepción: techo exacto PASS con advertencia; techo + 1 FAIL aunque otros archivos
  disminuyan; 501 PASS; 500 FAIL por obsoleta; retirada la entrada, 500 PASS y 501 FAIL.
- Configuración: cadena legacy, objeto incompleto o con campos extra, techo ausente,
  no entero o ≤ 500, ADR inexistente, commit inválido o inexistente, ruta duplicada,
  con caso incorrecto, con `..`, fuera de `frontend/src`, `.ts` o inexistente: FAIL.
- Modo diff: exceptuado cambiado que crece FAIL; la configuración se valida siempre.
- Integridad Node acepta objetos válidos y rechaza malformados; las demás listas
  conservan su contrato.
- Activación inicial (una vez): 36 entradas idénticas a esta tabla, 36 advertencias
  y cero violaciones PR-7 en el árbol real.

### Archivos de implementación

- `tools/architecture/check-architecture-guardrails.ps1`: sección PR-7 y su carga. Además
  se corrige un fail-open preexistente: sin comando nativo previo, `$LASTEXITCODE` nulo tras
  el handler-size hacía salir 0 sin evaluar las reglas frontend.
- `tools/architecture/architecture-grandfather.json`: 36 objetos aprobados.
- `tools/architecture/check-grandfather-integrity.mjs` y su `.test.mjs`: forma con techo.
- `tools/architecture/check-architecture-guardrails.test.mjs`: fixtures PR-7, ya
  incluidas en `npm run architecture:check`.
- `tools/architecture/check-architecture-guardrails.pr7.initial.mjs`: comprobación
  inicial, ejecutada explícitamente con
  `node --test tools/architecture/check-architecture-guardrails.pr7.initial.mjs`;
  no forma parte del runner permanente.
- `docs/architecture/pr-rules-catalog.md` (R-01/PR-7), `docs/architecture/enforcement.md`,
  `tools/architecture/README.md` y el índice de ADRs. Ningún archivo funcional del ERP cambia.

## Consequences

PR-7 se desbloquea sin refactorizar componentes, con deuda visible y prohibición de
crecimiento; el límite general de 500 se mantiene para archivos nuevos y no
exceptuados. No acredita calidad funcional ni aptitud para piloto/despliegue.

Fuera de alcance: el warning > 400 líneas descrito en R-01 (no implementado), los
archivos que superarían 500 con líneas físicas y la regla ESLint `max-lines`.
Compatibilidad requerida: Windows PowerShell 5.1 y PowerShell 7 (CI Linux). La
aprobación del ADR no acredita un CI correcto; solo un CI verificado permite
declarar el gate satisfecho.

## Alternatives Considered

- Refactor inmediato de los 36: alto riesgo; incluye módulos CLOSED/FROZEN sin autorización.
- Lista de rutas sin techo: permite crecimiento; descartada.
- Aumentar el límite general u omitir PR-7: permite infracciones nuevas; descartado.
- Cambiar la métrica a líneas físicas: añade nueve infracciones y mezcla otro cambio.
- Baseline separado: duplica configuración; se evoluciona la lista existente.
