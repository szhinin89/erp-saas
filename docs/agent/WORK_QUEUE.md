# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SPA-V1.1-DOC (**PLANIFICACIÓN DOCUMENTAL COMPLETA / ARQUITECTURA APROBADA / CI #1227 SUCCESS**): 13 áreas funcionales y 19 pantallas especificadas; [ADR-051](../decisions/ADR-051-spa-v1.1-architecture.md) ACCEPTED / APROBADO, SPA-SEL-01..12 y DP-01..03 aprobadas por el propietario el 2026-11-19, publicadas en `8e04e3a6d` y verificadas con [CI #1227 SUCCESS](https://github.com/szhinin89/erp-saas/actions/runs/44629184698). [Contrato central](../decisions/SPA-V1.1-CONTRACT-SPEC-01.md), [registro de aprobación §25](../decisions/SPA-V1.1-APPROVED-DECISIONS-01.md#25-aprobación-arquitectónica-de-adr-051-spa-sel-0112-y-dp-0103) y [plan F0–F8 documentado](../decisions/SPA-V1.1-IMPLEMENTATION-PLAN-01.md). Pendientes reales: Empleados / `IEmployeeDirectory` (EMP-E6b), con implementación/pruebas y autorización separadas; COND-01..13 según corresponda (parte arquitectónica de COND-13 aprobada, autorizaciones por fase pendientes); autorizaciones especiales de componentes protegidos y validaciones técnicas, jurídicas y fiscales aplicables. O-14 y O-24/V-01 siguen abiertos en su alcance; O-13 conserva aspectos parciales. La planificación general y la aprobación de ADR-051 están completadas; no se reabren decisiones aprobadas. **SPA no implementado; F0–F8 sin autorización de implementación**, tampoco preparación de solo lectura ni desarrollo de Empleados. El CI no acredita API real ni cierre funcional. Estado y conciliación CI #1202–#1227 SUCCESS, con #1201 FAILURE histórico e incidencia abierta, en [STATUS](../../STATUS.md).
- EMPLOYEES-V1-E1 (**VERIFICACIÓN TÉCNICA COMPLETADA / CIERRE APROBADO POR EL PROPIETARIO**, 2026-11-24; ADR-050 **ACCEPTED / APROBADO**, Empleados no
  implementado): verificación de solo lectura registrada en el [plan](EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md), con las diez decisiones del
  propietario (baseline única sin migraciones incrementales, `CONCURRENCY_CONFLICT`, fechas inválidas como 422, paginación `skip/take`, clave
  `BpEmployment`, vinculación con permisos de Empleados y de membresías sin cuentas globales, filtro `specialtyId`, allowlist sin cambios si
  `AsPlatformQuery()` cumple los controles, bloqueo preventivo de baja). EMP-E2 está **COMPLETADO / CLOSED** (2026-11-24; commit `2974c7cbd`, base técnica no operativa, gate técnico aprobado, CI satisfactorio confirmado por el propietario; detalle, evidencia y observaciones A–C en [STATUS](../../STATUS.md)); EMP-E3 está **IMPLEMENTADO / GATE TÉCNICO APROBADO / PENDIENTE CI** (2026-11-24; protección de BusinessPartner con lock `BpEmployment` y validación READ COMMITTED; T-05 parcial: la acreditación completa corresponde a E4/E5; evidencia y resolución de M1, L1–L3 en [STATUS](../../STATUS.md)); EMP-E4 a EMP-E7 y EMP-E6b permanecen **PENDING / NO AUTORIZADAS**:
  cada una requiere autorización expresa y lista exacta de rutas. Requisitos de aceptación heredados de EMP-E2: E4 evalúa la protección EF de `CompanyId`; E4 y E5 acreditan la validación y las pruebas negativas de referencias cruzadas entre empresas y tenants antes de habilitar operaciones reales. Siguen pendientes la definición del propietario sobre asignaciones futuras al
  dar de baja y las autorizaciones separadas de esquema/baseline, MasterData (solo las dos guardias y su transacción), Kernel, frontend
  compartido (wizard de BP) y Access solo si fuera necesario. `IEmployeeDirectory` es el contrato oficial especificado, pendiente de
  implementación y verificación (EMP-E6b); se conserva la integración definida con SPA. Estado en [STATUS](../../STATUS.md); diseño en
  [ADR-050](../decisions/ADR-050-employees-v1-architecture.md), [especificación](../decisions/EMPLOYEES-V1-CONTRACT-SPEC-01.md) y
  [plan](EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md).
- PEDIDOS-VENTA-V1-F0 (**PENDING / NO AUTORIZADA**; ADR-049 Accepted el 17/11/2026, diseño aprobado y no implementado):
  verificaciones técnicas VER-01 a VER-14 de Órdenes de Pedido V1 (solo lectura; no escribe código). Requiere
  autorización expresa del propietario para iniciarse; ninguna fase de implementación (F1–F9) está autorizada, y F5/F6
  son NO AUTORIZABLES. Conserva la baseline única (DEC-06) y las restricciones FROZEN/CLOSED (DEC-01: extensiones
  mínimas, fase por fase). Estado en [STATUS](../../STATUS.md); diseño en
  [ADR-049](../decisions/ADR-049-sales-order-v1-architecture.md) y [plan](PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md).
- PURCHASES-NEW-TAB-REDESIGN-01 (publicado en `04619c611`, CI #1209 SUCCESS; reapertura visual de Compras V1, que sigue CLOSED en
  lo funcional; sustituye a PURCHASES-NEW-TAB-PHASE-A-01, publicada en `130d23267`, CI #1208 SUCCESS): rediseño de «Nueva compra» aprobado por el propietario,
  extracción de componentes de `PurchasesPage.tsx` y barra de acciones fija. Pendiente: validación con API real,
  sesión autenticada y una compra desde XML real con productos pendientes (lo verificado fue con API simulada). Limitación conocida de i18n: al cambiar de idioma, los mensajes de
  validación ya visibles conservan el idioma anterior hasta revalidar (requiere cambio en `usePurchasesPage.ts`; sin
  decisión del propietario). Alcance, pruebas y evidencia en [STATUS](../../STATUS.md).
- SRI-ID-TYPE-SHORT-NAME-01 (PUBLICADO en `f4f04c272`; CI #1206 SUCCESS): `short_name`
  en `global.sri_id_type` vía baseline inicial. Pendiente: verificar
  `shortName` en la API real con sesión autorizada. Estado y evidencia en [STATUS](../../STATUS.md).
- IAM-V1-F2 (IMPLEMENTADO, pendiente gate del propietario; autorizado 2026-11-10; CI #1144
  SUCCESS valida su contenido; IAM-V1-F3 ya CLOSED con CI #1146 SUCCESS y aceptación del propietario
  2026-11-11, evidencia en [STATUS](../../STATUS.md); sin siguiente fase IAM autorizada): administración de Perfiles, Permisos y Usuarios, visibilidad
  fail-closed y protección de rutas por permisos efectivos. Estado, evidencia y archivos en
  [STATUS](../../STATUS.md). La decisión de datos sobre el perfil `DataEntry` sigue siendo del
  propietario (sin cambios).
- SEC-A01 (requisitos de despliegue; implementación CLOSED; SEC-A01-INFRA-01 deja el código de
  `X-Forwarded-Proto` y las variables Compose preparados y probados, sin desplegar): antes de
  producción, verificar IP real tras el proxy y declarar proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Desarrollo terminado; son
  comprobaciones de configuración del servidor productivo previas al despliegue. Estado y gates en
  [STATUS](../../STATUS.md).
- Aceptación ampliada de ProductPicker (PENDING; separado de PURCHASES-PRODUCT-PICKER-RACE-01,
  CLOSED con CI #1126 SUCCESS): ejecución ampliada de 63 archivos por el propietario sin la
  intermitencia previa; aún no acreditada. Cierre y evidencia en [STATUS](../../STATUS.md).
- ITEMS-LOOKUP-PICKER-FOCUS-RACE-01 (PENDING; Items FROZEN, no reabierto): `ItemLookupPicker`
  reinicia `focusIdx` con un efecto pasivo sobre `results`, mismo patrón de carrera que
  PURCHASES-PRODUCT-PICKER-RACE-01; requiere reapertura expresa de Items, reproducción determinista
  y corrección en el componente. Registro en [STATUS](../../STATUS.md).
- BP-CONSOLIDATION-01-E2E (PENDING; independiente, **no bloquea el cierre**): validación E2E de Clientes y
  Proveedores con API real y sesión autorizada; hasta ahora solo mocks y navegador con API simulada.
  BP-CONSOLIDATION-01 está COMPLETADA / CLOSED (commit `65c207e42`, CI #1205 SUCCESS; #1201 es CI-1201,
  ajeno). Evidencia en [STATUS](../../STATUS.md).
- BP-CONSOLIDATION-01-DEBT (deuda técnica independiente; **no bloquea el cierre**; sin autorización de
  implementación): flechas/*roving tabindex* en `ZHTabBar`; auditoría de contraste y axe-core con herramienta
  aprobada; accesibilidad de `ZHPromptModal`; rama `section="review"` de `MasterDataBpFormFields` y rama de
  edición no embebida del wizard; y las limitaciones de BP-NAV-GUARD-01 (overlay de bloqueo, shell AdminCore,
  confirmación abierta al terminar la sesión).
- CI-1201 (PENDING; causa raíz por determinar): run #1201 sobre `e0f1d338c` falló en `backend / Backend (api)`
  por una prueba de integración de Ventas,
  `SalesCancelCollectionConcurrencyTests.Cancel_y_devolucion_con_credito_a_CxC_simultaneas_no_se_bloquean_y_quedan_consistentes`
  (HTTP 422 en lugar de 201; 1724/1725 correctas). Commit sin cambios de backend; #1200 y #1202 pasaron; no
  se atribuye a Business Partners. Sin investigar ni tocar Ventas aún. Detalle en [STATUS](../../STATUS.md).
- BP-NAV-GUARD-01 (publicado en `9761470e8`, CI #1202 SUCCESS; gate del propietario pendiente): router de
  datos en `App.tsx`; protección de cambios sin guardar de la ficha con `useBlocker` y consulta previa en el
  cambio de empresa, el cierre de sesión y «Volver al Admin Core»; los eventos automáticos de seguridad no
  se bloquean. Casos no protegidos y evidencia en [STATUS](../../STATUS.md).
- Revisión de perfiles de acceso antes de desplegar (PENDING; riesgo residual de
  PURCHASES-RECEPTION-SECURITY-01, CLOSED con CI #1128 SUCCESS): perfiles con `purchases.view` sin
  `purchases.create`/`purchases.update` pierden acciones de recepción y conciliación. Consulta de
  solo lectura; detalle en [STATUS](../../STATUS.md).
- ACCOUNTING-CROSS-MODULE-ACCOUNT-LOCK-01 (PENDING; limitación de ACCOUNTING-CHART-INTEGRITY-01,
  CLOSED con CI #1127 SUCCESS, no reabierto): las configuraciones de otros módulos no toman el lock
  del Plan de cuentas; una asignación de cuenta concurrente con su desactivación o con quitarle
  movimiento puede confirmarse (`PostingAccountGuard` solo rechaza al contabilizar). Incluye la
  reasignación con propietario inactivo en las seis familias. Revisar antes de producción; detalle
  en [STATUS](../../STATUS.md).
- PURCHASES-RECEPTION-XML-ATTEMPT-01 (PENDING; requiere backend y migración): historial persistente
  de intentos y resultados de consulta XML. Antes de intervenir el handler congelado, revisar
  ADR-028/ADR-046, migraciones, privacidad y aislamiento; esta cola no autoriza alterar contratos
  fiscales protegidos.
- DASHBOARD-REGEN-DH-001 (PENDING; Dashboard FROZEN v1.0): regeneración documental separada con el
  procedimiento oficial (`tools/dashboard/run-dashboard-final.ps1`) sobre árbol limpio, revisando el
  diff antes de publicar; resuelve DH-001 de
  [DASHBOARD_HEALTH](../ProgressDashboard/DASHBOARD_HEALTH.md). No se mezcla con commits funcionales.
- CI-SELECTIVE-ARCHITECTURE-01 (PENDING): retirar una excepción de
  `tools/architecture/architecture-grandfather.json` clasifica hoy como perfil completo (ruta fuera
  de las listas de `ci_changes.mjs`; p. ej. CI #1121 sobre `bd816be40`). Evitar el FULL innecesario
  conservando fail-closed y probando los perfiles frontend, backend, mixed, full y documentation.
  Relacionado con la evidencia pendiente de CI-SELECTIVE-01 en [STATUS](../../STATUS.md).
- API-DOC-F01-L3C2 (PUBLICADO `cba7e44d4`, CI #1181 SUCCESS; pendiente gate del propietario; autorizado 2026-11-13 con excepción
  específica a ADR-ERP-002): BUG REAL F-01, `brandingJson` de `POST /api/integration/v1/companies` ya no llega a
  `Company.Website`; se conserva en v1 pero un valor no vacío responde 422 hasta definir su contrato. Alcance y
  evidencia en [STATUS](../../STATUS.md). Bloque independiente de API-DOC-F09-L3C1.

- CI-1169 (PUBLICADO `7be7d3a5d`, CI #1170 SUCCESS): gate del propietario no acreditado; no CLOSED. Evidencia en [STATUS](../../STATUS.md).
- Incidencia Redis #1171: intento 1 FAILURE, intento 2 SUCCESS sobre el mismo SHA, confirmados por API; causa pendiente de diagnóstico independiente. Detalle en [STATUS](../../STATUS.md).
- EXPENSE-SUPPLIER-SUBCATEGORIES-01 (fases 1–3 publicadas: `72a9afc3d`, `f99332051`, `f4a0bc483`; cierre documental en gate del propietario): CI #1197 SUCCESS. Sin trabajo técnico pendiente (D4 completada). Evidencia en [STATUS](../../STATUS.md); diseño en [el diseño](../decisions/EXPENSE-SUPPLIER-SUBCATEGORIES-DESIGN-01.md).
- EXPENSE-UI-02 (cierre técnico `00bdef910`, CI #1193 SUCCESS): confirmar aceptación visual del propietario antes de declarar cierre funcional; no autoriza cambios. Evidencia en [STATUS](../../STATUS.md).
