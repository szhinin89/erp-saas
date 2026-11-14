# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SEC-SUSP-01 (PUBLICADO, pendiente validación CI; revisión del propietario aprobada 2026-11-13; autorizado con excepción
  limitada a ElectronicDocuments/SRI/Communications FROZEN, [ADR-048](../decisions/ADR-048-company-suspension-automatic-processes.md)):
  transmisiones SRI nuevas y entrega de correo pausadas para empresas suspendidas o deshabilitadas, sin detener
  consultas ni conciliación; observabilidad por empresa. Evidencia y archivos en [STATUS](../../STATUS.md).
- SEC-SUSP-02b (PUBLICADO, pendiente validación CI; aprobado por el propietario 2026-11-13): el refresh
  valida el estado de usuario/tenant/empresa/membresía antes de rotar, sin sucesor huérfano ni falsa alarma de
  reutilización; pruebas PostgreSQL 11/11 y gates OK. Evidencia en [STATUS](../../STATUS.md). SEC-SUSP-02 (sesión
  para empresas suspendidas) queda como KEEP.
- IAM-V1-F2 (IMPLEMENTADO, pendiente gate del propietario; autorizado 2026-11-10; CI #1144
  SUCCESS valida su contenido; IAM-V1-F3 ya CLOSED con CI #1146 SUCCESS y aceptación del propietario
  2026-11-11, evidencia en [STATUS](../../STATUS.md); sin siguiente fase IAM autorizada): administración de Perfiles, Permisos y Usuarios, visibilidad
  fail-closed y protección de rutas por permisos efectivos. Estado, evidencia y archivos en
  [STATUS](../../STATUS.md). La decisión de datos sobre el perfil `DataEntry` sigue siendo del
  propietario (sin cambios).
- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
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
- PURCHASES-MANUAL-SUPPLIER-ROLE-ACTIVE-01 (PENDING; Compras CLOSED, requiere reapertura): guardar
  una compra (alta y actualización del borrador, también manual) no exige rol Proveedor activo.
  Registro en [STATUS](../../STATUS.md); sin trabajo abierto.
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
- API-DOC-F35-L1 (PUBLICADO, pendiente resultado del CI; gate del propietario aprobado 2026-11-13; Gastos permanece
  CLOSED, sin reapertura general): BUG REAL F-35, `PUT` de categorías de gasto no debe revertir
  `isDeductible`/`requiresInvoice`. Alcance exacto y evidencia en [STATUS](../../STATUS.md). el resto de F-01 a F-52 sigue pendiente de autorización por bloque.
- API-DOC-F14-L2 (PUBLICADO, pendiente resultado del CI; revisión del propietario aprobada 2026-11-13; Contabilidad
  conserva sus restricciones de cierre): BUG REAL F-14, el reintento idempotente de `ManualJournal.Create`
  debe reflejar el estado, la versión y el número reales del asiento existente. Alcance y evidencia en
  [STATUS](../../STATUS.md). Bloque independiente de API-DOC-F35-L1.
- API-DOC-F09-L3C1 (PUBLICADO, pendiente resultado del CI; revisión del propietario aprobada 2026-11-13, autorizado con
  excepción específica a ADR-ERP-002): BUG REAL F-09, el RUC de `POST /api/integration/v1/companies` se
  valida (RUC ecuatoriano o provisional `TMP-EC-`) en lugar de rellenarse o truncarse; respuesta 422. Alcance
  y evidencia en [STATUS](../../STATUS.md). C2 (F-01) se lista abajo; C3 (F-02, `Activate` y `IsActive`) se lista abajo.
- API-DOC-F01-L3C2 (IMPLEMENTADO, pendiente revisión del propietario y CI; autorizado 2026-11-13 con excepción
  específica a ADR-ERP-002): BUG REAL F-01, `brandingJson` de `POST /api/integration/v1/companies` ya no llega a
  `Company.Website`; se conserva en v1 pero un valor no vacío responde 422 hasta definir su contrato. Alcance y
  evidencia en [STATUS](../../STATUS.md). Bloque independiente de API-DOC-F09-L3C1.
- API-DOC-F02-L3C3 (PUBLICADO, pendiente validación CI; revisión del propietario aprobada 2026-11-13; autorizado con
  excepción específica a ADR-ERP-002): BUG REAL F-02, `PUT /api/integration/v1/companies/{id}/activate` ya no fuerza
  `IsActive=true`; solo transiciona `OperationalStatus` y conserva la habilitación administrativa previa. Alcance y
  evidencia en [STATUS](../../STATUS.md). Bloque independiente de C1 y C2.
- API-DOC-B-MSG (PUBLICADO, pendiente resultado del CI; aprobado por el propietario 2026-11-13; Contabilidad e
  Initial Load siguen CLOSED, solo clasificación y mensajes): BUG REAL F-10/F-33, los códigos de validación de
  Contabilidad (asientos manuales, cierre anual) y Carga Inicial (cierre, apertura, preflight) pasan de 400 a 422 con
  mensaje catalogado, conservando los valores de `code`; `MANUAL_POST_INTERNAL` (excepción técnica) es error interno 500. Alcance, impacto HTTP/OpenAPI y residuo en
  [STATUS](../../STATUS.md). Bloque independiente de los demás hallazgos.
- API-DOC-B-SALES (PUBLICADO, pendiente validación CI; revisión del propietario aprobada 2026-11-13; Ventas sigue
  CLOSED): BUG REAL F-18, autorizar una factura anulada responde «anulada» en lugar de «ya fue autorizada»; BUG REAL
  F-17, `pageNumber`/`pageSize` fuera de rango en `SalesReceivables` y `SalesReturn` responden 422 en lugar de error
  de servidor (el `status` desconocido es KEEP documentado). Alcance y evidencia en [STATUS](../../STATUS.md).
- API-DOC-B-BP (PUBLICADO, pendiente validación CI de la nueva prueba; decisión del propietario 2026-11-13, opción 1): F-51
  queda KEEP por decisión explícita. Desactivar un tercero impide nuevas operaciones, conserva documentos, obligaciones
  pendientes y roles, y no introduce bloqueo nuevo; sin cambio funcional para V1 y sin registrarse como BUG REAL.
  Evidencia (prueba de caracterización en PostgreSQL real) en [STATUS](../../STATUS.md).
