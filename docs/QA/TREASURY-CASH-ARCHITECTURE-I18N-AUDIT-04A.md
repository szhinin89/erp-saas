# TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04A

Continuación de AUDIT-04: terminar la migración i18n de `/treasury/cash` y
`/treasury/cash/movement-reasons` (apertura, detalle, movimientos, cierre,
confirmaciones y validaciones), completar la paridad ES/EN/QU de las keys
`caja.*`/`common.*` y auditar lo entregado antes de la interrupción por
límite de uso. Sin cambios en cálculos, payloads, posting, backend,
`CashSession`, `PaymentMethod`, permisos ni transiciones de UI. Sin commit.

Ambos `ZHPageNotice` de arquitectura de AUDIT-04 se mantienen en su lugar:
reglas del catálogo en `movement-reasons` y separación del efectivo físico
respecto de ventas/cobros en `/treasury/cash`. No se agregaron notices
nuevos.

## Correcciones aplicadas durante esta continuación

El agente anterior dejó un buen trabajo de fondo (migración de textos,
tests nuevos, herramientas de verificación), pero también artefactos y
alcance que había que revisar antes de entregar:

1. **Mojibake en locales**: `caja.session.unauthorized` y
   `common.closeNotification` en `es.json` tenían `?` en vez de tildes
   (`Sesi?n`, `notificaci?n`) — probablemente por un script de migración
   sin UTF-8 explícito. Corregido a `Sesión`/`notificación`.
2. **Scope creep en `qu.json` (7 keys `common.*` globales)**: `common.status`,
   `common.loading`, `common.errorPrefix`, `common.refresh`, `common.cancel`,
   `common.saving`, `common.close` fueron traducidas de español a kichwa real
   (`Sakiy`, `Wichkay`, etc.). Estas keys son compartidas por **todos** los
   módulos del ERP, no solo Caja — cambiarlas es una decisión de contenido
   de producto que afecta toda la app en locale QU, fuera del alcance de
   este ticket y sin forma de validar la calidad lingüística aquí. Revertidas
   a sus valores originales (fallback en español, como ya tenían sus
   vecinas `common.saveDraft`, `common.active`, `common.code`, etc.).
3. **Scope creep en `companies.planMenu.featureGuide.blockAnchorBody`**: key
   huérfana preexistente (solo en QU, ausente en ES/EN) de un módulo no
   relacionado (Companies/Plan). El agente anterior la "arregló" agregando
   ES/EN y reescribiendo el texto QU existente para lograr paridad global.
   Revertido por completo al estado de HEAD: es deuda preexistente ajena a
   Caja, y reescribir una traducción QU ya existente sin poder validarla es
   más riesgoso que dejar el gap conocido.
4. **Checker `check-i18n-keys.mjs` sobre-endurecido**: el agente anterior
   subió `orphan-en` de warning a violation y agregó un nuevo `orphan-qu`
   (violation) que exige paridad estricta de **todo** el catálogo (miles de
   keys de módulos no relacionados) contra ES. Esto solo pasaba porque el
   punto 3 se había "arreglado". Revertido a warning/sin-check-qu (estado
   original) — la exigencia de paridad total del catálogo es una decisión
   de política de proyecto, no un hallazgo de este ticket, y no debe fallar
   por deuda preexistente ajena a Caja. Se conserva la mejora real y de
   bajo riesgo que sí trajo: `inspectLocale` (detección de claves JSON
   duplicadas antes de que `JSON.parse` las descarte en silencio).
5. **Test `cajaI18n.test.ts` con la misma sobre-exigencia**: la prueba
   "keeps all locale keys identical" comparaba el catálogo **completo**
   (miles de keys) entre locales. Reescrita para comparar solo el
   subconjunto `caja.*`/`common.*` — el que Caja realmente usa — manteniendo
   intacta la detección de duplicados JSON por locale.
6. **Fallback removido de `cashMovementTypes.ts`**: `cashMovementTypeLabel`/
   `manualCashMovementTypeOptions` habían quedado como `t(key)` sin
   fallback, rompiendo el patrón `t(key, fallback)` usado en el resto del
   proyecto (incluida esta misma auditoría, que encontró y corrigió
   exactamente esta clase de bug en la navegación: key sin fallback +
   diccionario incompleto = texto de la key cruda en pantalla). Restaurado
   `fallbackLabel` por tipo; con las tres traducciones presentes esto es
   una red de seguridad sin costo, no una segunda fuente de negocio.
7. **Line endings CRLF**: `CajaPage.tsx`, `useCajaPage.tsx`,
   `useCajaPage.test.tsx`, `cashMovementTypes.ts`, `cajaSchema.ts` y los
   tres `locales/*.json` habían quedado con `CRLF` en cada línea (el repo
   usa `LF`, `.gitattributes: * text=auto eol=lf`). Normalizados a `LF`.
8. **Archivos temporales `.tmp/cash-i18n/`** (`complete.py`, `migrate.py`,
   logs, JSON intermedios): eliminados — eran scripts/artefactos de la
   migración, no parte del código fuente.

Nada de esto tocó lógica de negocio, cálculos, payloads ni el backend — el
diff completo (`git status`) sigue siendo exclusivamente frontend i18n +
dos archivos de herramientas de verificación.

## Keys i18n finales

Se completaron en EN/QU las 63 keys `caja.*` que ya existían en ES desde
AUDIT-04 pero faltaban en los otros dos locales, y se agregaron las keys
nuevas de `/treasury/cash` (apertura, detalle, movimientos, cierre,
confirmaciones, validaciones) en los tres locales desde el inicio. Total:
~180 líneas añadidas por locale, todas bajo los namespaces `caja.*` (más
`common.closeNotification`, compartida).

Verificación final (alcance `caja.*`/`common.*`, el que usa este módulo):

- `node tools/architecture/check-i18n-keys.mjs`: **PASS** (paridad ES↔EN,
  ES↔QU sobre el catálogo completo con el checker restaurado; huérfanos
  en EN son warning, no violation — comportamiento original del proyecto).
- `node --test tools/architecture/shared/locale-integrity.test.mjs`: **PASS**
  (4/4).
- `cajaI18n.test.ts` → "keeps caja.\*/common.\* keys in parity ... and
  detects duplicate JSON properties": **PASS** para es/en/qu.
- Cero keys duplicadas, cero keys huérfanas dentro de `caja.*`/`common.*`.

## Hardcodes restantes y justificación (excepciones documentadas)

- Ligaduras Material Symbols (`add`, `refresh`, `arrow_back`, `expand_less`,
  `expand_more`, `visibility`, `close`, `edit`, `save`, `block`,
  `check_circle`, `list_alt`, `add_box`): identificadores de icono, no
  texto para el usuario.
- Símbolos/puntuación sin contenido lingüístico: `$`, `%`, `(`, `)`, `.`,
  `:`, `*` de campo requerido, `N°` de `ZHDataTable`.
- Denominaciones USD (`$100`, `$50`, ..., `$0.01`): valores y etiquetas que
  se envían tal cual al cierre de caja — son datos, no texto de UI.
- Datos de usuario/API (nombres/códigos de sucursal, caja, punto de
  emisión, cajero, motivo, descripción de movimiento, notas de cierre,
  forma de pago, cliente, factura/referencia, banco/cuenta, fechas,
  importes): nunca se traducen ni se reinterpretan.
- `m.destination` (backend, `CashSessionQueryUseCases.ResolveDestinationLabel`):
  puede llegar como `Sin clasificar`, `Cuentas por Cobrar`, `Cuenta
  bancaria`, `Cuenta configurada` o `Caja física`. Se conserva tal cual —
  traducirlo en frontend sin un código estable requeriría cambiar el
  contrato del backend o clasificar por texto (ambos fuera de alcance y
  el segundo, además, prohibido por la regla de "cero lógica basada en
  texto traducido").
- Tipo de movimiento desconocido (no en el enum de 6 valores): se conserva
  el valor técnico crudo como fallback de compatibilidad — no debería
  ocurrir en la práctica (`CashMovementType` solo tiene esos 6 valores),
  documentado por si el backend agrega uno nuevo antes que el frontend.
- Errores reales del servidor (`err.response.data.message.user`, etc.): se
  muestra el mensaje del backend tal cual, como exige el patrón de manejo
  de errores del proyecto (`applyServerErrors`/`formatApiRequestError`) —
  nunca se le aplica i18n a contenido dinámico del servidor.

Cero lógica de negocio basada en texto traducido: filtros/estados
comparan contra `"Open"`/`"Closed"`; tipos de movimiento contra
`ManualIncome`/`ManualExpense`/`Withdrawal`/`SaleRefund`/etc.; agrupación
y expansión de filas usan IDs. Ningún `if`/`switch` compara contra una
etiqueta ya traducida.

## Architecture Notice

Sin cambios respecto de AUDIT-04 — se mantienen exactamente los dos
`ZHPageNotice variant="info"` ya existentes (mismo patrón que
`documentFlows.separationNotice`, sin componente nuevo):

1. `/treasury/cash/movement-reasons` → `caja.movementReasons.architectureNotice`
   (Código/Tipo inmutables, Nombre/Orden/Estado editables, historial
   conserva el motivo usado).
2. `/treasury/cash` → `caja.session.separationNotice` (separación efectivo
   físico vs. ventas/cobros informativos).

No se agregaron notices adicionales.

## Tests y resultados finales

- Backend: sin cambios en esta continuación (04A es 100% frontend) — no
  requiere rebuild ni tests backend adicionales a los ya validados en
  AUDIT-04/03A.
- `npx tsc -b`: **PASS**.
- `npm run lint`: **PASS**, 0 errores, 34 warnings preexistentes
  (`max-lines`/`react-hooks/exhaustive-deps`, ninguno nuevo).
- `npx vitest run src/modules/caja`: **PASS**, 3 archivos / **57 tests**.
- `npx vitest run` (suite completa frontend): **PASS**, 197 archivos /
  **1663 tests** — verificación extra por haber tocado dos archivos
  compartidos (`ZHToast.tsx`, `apiError.ts`) y los tres locales globales.
- `npm run build`: **PASS**; warnings preexistentes de chunks >500 kB y de
  `fullLogout` con import estático+dinámico (no relacionados).
- `git diff --check`: **PASS**, sin errores de espacio en blanco.

## Git

Diff frente a HEAD (incluye AUDIT-04, nunca commiteado, más esta
continuación): 16 archivos modificados + 4 nuevos, todos frontend salvo
las dos herramientas de verificación en `tools/architecture/`. Sin cambios
en `backend/`. Sin commit, según instrucción explícita.

Archivos nuevos que se conservan: `cajaI18n.test.ts` (suite de integridad
i18n específica de Caja), `tools/architecture/shared/locale-integrity.mjs`
+ su test (detección de JSON duplicado, bajo riesgo y reutilizable).

Eliminado: `.tmp/cash-i18n/` completo (scripts y logs de migración, no
código fuente).
