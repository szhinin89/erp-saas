# DS Massive Cleanup Plan

Línea base para la limpieza masiva de tipografía/apariencia local en todo el
frontend. Generado con `scripts/ds/ds-audit.ps1` (todos los módulos) el
2026-08-17. No aplica cambios — es el plan de ejecución para tareas
posteriores, una por módulo.

**Nota de mantenimiento**: al construir esta línea base se encontró y corrigió
un bug real en `scripts/ds/_ds-lib.ps1` (`Get-DsFindings`/`Get-DsTargetFiles`):
cuando un módulo tenía 0 o 1 archivo con hallazgos, PowerShell "desenrollaba"
la lista de retorno en el pipeline y el caller recibía `$null` en vez de una
lista vacía, rompiendo `ds-audit.ps1 -ModuleName settings|admin` con
`PropertyNotFoundStrict`. Se corrigió forzando el retorno con el operador
coma (`return ,$findings`). Ya validado contra `settings`/`admin` (0
hallazgos reales, ahora reportan limpio en vez de crashear) y re-confirmado
que `sales`/`purchases`/`inventory`/`dashboard`/`masterData`/
`electronicDocuments` siguen dando los mismos conteos que antes del fix.

## A. Estado del guard

```
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-guard.ps1 -Scope modules
ds-guard: OK - sin violaciones bloqueantes en scope 'modules'.
```
**Exit code 0. Violaciones bloqueantes (`NOT_OK_VISUAL_LOCAL`): 0** en todo
`frontend/src/modules`.

## B. Ranking por módulo

Todos los módulos con al menos un archivo `.tsx`/`.css`. Módulos con 0
hallazgos (`admin`, `branches`, `cashRegisters`, `config`, `establishments`,
`finance`, `pricing`, `reportes`, `ride`, `session`) se listan en la sección
E, no en la tabla (no requieren ninguna acción).

| Módulo | Total | OK_TOKEN | OK_ICON | OK_LAYOUT | OK_GLOBAL | NEEDS_DECISION | NOT_OK | Prioridad | Riesgo visual | Auto-fix / Manual |
|---|---|---|---|---|---|---|---|---|---|---|
| **sales** | 180 | 77 | 9 | 7 | 37 | 50 | 0 | — (ya cerrado, referencia) | — | — |
| **purchases** | 114 | 57 | 12 | 1 | 19 | 25 | 0 | — (ya cerrado, referencia) | — | — |
| **masterData** | 48 | 34 | 1 | 1 | 0 | 12 | 0 | **Alta** | Medio (pantallas de partner/sucursales, uso frecuente) | Manual |
| **auth** | 39 | 22 | 4 | 2 | 0 | 11 | 0 | Media | Bajo (pantallas de login, poco tráfico post-onboarding) | Manual |
| **inventory** | 27 | 18 | 2 | 0 | 0 | 7 | 0 | — (ya cerrado en ERP-DS-MODULE-SWEEP-01, pendiente de commit) | — | — |
| **dashboard** | 23 | 20 | 1 | 0 | 0 | 2 | 0 | Baja | Bajo (solo 2 hallazgos) | Manual (trivial) |
| **access** | 22 | 9 | 4 | 2 | 0 | 7 | 0 | Media | Medio (gestión de perfiles/permisos, uso administrativo) | Manual |
| **electronicDocuments** | 14 | 10 | 0 | 1 | 0 | 3 | 0 | Media-baja | Medio (monitor SRI, visible para soporte) | Manual |
| **items** | 7 | 4 | 2 | 0 | 0 | 1 | 0 | Baja | Bajo (1 hallazgo) | Manual (trivial) |
| **security** | 6 | 3 | 0 | 0 | 0 | 3 | 0 | Baja | Bajo | Manual (trivial) |
| **configuracion** | 9 | 8 | 0 | 1 | 0 | 0 | 0 | — (ya limpio, 0 NEEDS_DECISION) | — | — |
| **caja** | 4 | 1 | 0 | 0 | 0 | 3 | 0 | Media | **Alto** (pagos/caja — fuera de alcance de auto-fix, requiere cuidado extra) | Manual, con precaución |
| **company-management** | 1 | 0 | 0 | 0 | 0 | 1 | 0 | Baja | Bajo | Manual (trivial) |
| **logistica** | 1 | 0 | 0 | 0 | 0 | 1 | 0 | Baja | Bajo | Manual (trivial) |
| **emissionPoints** | 1 | 1 | 0 | 0 | 0 | 0 | 0 | — (ya limpio) | — | — |

Ninguna fila tiene `NOT_OK_VISUAL_LOCAL` — no hay bloqueos duros pendientes en
ningún módulo, solo `NEEDS_DECISION` (tipografía cruda a revisar).

## C. Orden recomendado de limpieza

Criterios combinados (deuda visual × riesgo funcional × importancia para
piloto × cercanía a patrones ya resueltos):

1. **masterData** (48 hallazgos, 12 `NEEDS_DECISION`) — mayor volumen real
   pendiente; pantallas de partner/sucursal muy visibles y usa patrones ya
   resueltos en Purchases (`ZHInfoRow`/`ZHDataValue`/mini-cards), así que el
   criterio de fix es directamente reutilizable, bajo riesgo de reinventar
   nada.
2. **auth** (39 hallazgos, 11 `NEEDS_DECISION`) — segundo mayor volumen,
   pantallas de login/reset con tráfico bajo y sin datos de negocio (cero
   riesgo funcional real), buen candidato para practicar el flujo antes de
   tocar módulos operativos.
3. **access** (22 hallazgos, 7 `NEEDS_DECISION`) — gestión de perfiles/roles,
   volumen moderado, mismo patrón de labels/tablas ya resuelto en Kardex.
4. **electronicDocuments** (14 hallazgos, 3 `NEEDS_DECISION`) — ya con
   `font-family-mono` resuelto (ERP-DS-MONO-REMAINING-01); solo faltan 3
   `NEEDS_DECISION` de tipografía, cierre rápido.
5. **dashboard** (23 hallazgos, solo 2 `NEEDS_DECISION`) — volumen bruto alto
   pero casi todo ya `OK_TOKEN`; cierre trivial.
6. **items**, **security**, **company-management**, **logistica** (1–7
   hallazgos cada uno) — barrido final de "cola larga", un solo commit
   agrupado si se revisan el mismo día (son distintos módulos pero cada uno
   trivial — de todas formas nunca mezclar el diff de archivos de más de un
   módulo en el mismo commit, ver sección E).
7. **caja** — deliberadamente al final y con precaución: es la única fila con
   riesgo "Alto" porque toca pantallas de pagos/caja. Ningún dato bloqueante
   (`NOT_OK`), pero cualquier ajuste ahí debe revisarse visualmente antes de
   commitear, igual que se hizo con las zonas de pago de Ventas (que
   quedaron fuera de los barridos anteriores a propósito).

`inventory` ya fue cerrado en `ERP-DS-MODULE-SWEEP-01` (cambios ya escritos en
el working tree, aún sin commit) — no repetir. `sales`/`purchases` son la
referencia, no se tocan salvo regresión.

## D. Separación de hallazgos

- **Fix automático seguro** (`ds-fix-known-patterns.ps1 -Apply`): **0 casos
  hoy** en todo `frontend/src/modules` (dry-run confirmado en el paso 6,
  sección de validaciones). Los 5 fixers narrow (italic prop/clase,
  `font-style: italic` de una línea, `font-family-mono` de una línea con
  selector de código, invariante `.zh-money-value`) no encontraron nada
  aplicable — ya se consumieron todos los casos triviales en tareas previas.
  Se mantiene como paso obligatorio antes de cada módulo nuevo, por si
  aparecen casos nuevos.
- **Fix manual por módulo**: los 126 `NEEDS_DECISION` restantes (tabla B) —
  requieren el mismo criterio ya aplicado en Sales/Purchases/Inventory:
  clasificar cada `font-size`/`font-weight`/`letter-spacing`/`text-transform`
  crudo como token exacto, ícono, layout, o excepción documentada.
- **Needs decision** (no se resuelven solo con grep): valores sin token
  exacto en la escala (9/10/11/13/17/21/27px, pesos 500 no estándar) —
  criterio ya usado: snap al token más cercano cuando el rol es label/dato
  normal, o dejar documentado si es una emphasis "hero" ya validada
  visualmente (mismo patrón que `.pf-total-mini-card__value`,
  `.sf-product__total-amount`).
- **No tocar**: `caja` en su forma de pago/efectivo (fuera de alcance
  explícito en toda esta serie de tareas), cualquier cosa en `backend/`,
  cálculos, XML/SRI, rutas, datos funcionales.

## E. Estrategia de ejecución

- **Un módulo por commit** — nunca mezclar el diff de dos módulos grandes
  (`masterData` y `auth` no van en el mismo commit, aunque se revisen el
  mismo día).
- Módulos triviales (1–7 hallazgos: `items`, `security`,
  `company-management`, `logistica`, `dashboard`) sí pueden agruparse en un
  solo commit **si** se revisan y validan juntos — igual criterio que "un
  commit por unidad de trabajo revisada", no por módulo estrictamente si el
  volumen es mínimo.
- Después de cada módulo: correr `ds-guard.ps1 -Scope modules` (debe seguir
  en exit 0 — global, no solo el módulo tocado, para detectar regresiones
  cruzadas vía CSS compartido, como pasó con `.kdx-mono`/Purchases).
- Correr `npx tsc -b`, `npm run build`, `npx eslint` acotado al módulo
  tocado + `src/components/zh` antes de cada commit.
- Pedir revisión visual del usuario antes de commitear cuando el módulo
  toque pantallas críticas para el piloto: `masterData` (partner/sucursal),
  `access` (permisos), `caja` (pagos) — mismo criterio ya aplicado durante
  todo este barrido en Sales/Purchases/Inventory.
- Módulos con 0 hallazgos (`admin`, `branches`, `cashRegisters`, `config`,
  `establishments`, `finance`, `pricing`, `reportes`, `ride`, `session`,
  `emissionPoints`, `configuracion`) no requieren trabajo — no incluir en
  ningún plan de commits.

## Archivos generados en esta tarea

- `docs/design-system/DS_AUDIT_REPORT.md` — audit global (`-Scope modules`).
- `docs/design-system/DS_AUDIT_electronicDocuments.md`
- `docs/design-system/DS_AUDIT_masterData.md`
- `docs/design-system/DS_AUDIT_dashboard.md`
- `docs/design-system/DS_AUDIT_inventory.md`
- `docs/design-system/DS_AUDIT_settings.md` (0 hallazgos)
- `docs/design-system/DS_AUDIT_admin.md` (0 hallazgos)
- `docs/design-system/DS_MASSIVE_CLEANUP_PLAN.md` (este archivo)
