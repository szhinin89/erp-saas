# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- IAM-V1-F3 (IMPLEMENTADO, commit local sin push; pendiente de CI propio y gate del propietario;
  autorizado 2026-11-11): perfiles iniciales Facturador y Contador en empresas nuevas,
  administrador inicial de empresa con credenciales temporales de una sola visualización,
  regeneración desde Admin Core y serialización del alta por tenant. No se cierra hasta su CI
  SUCCESS. Estado y evidencia en [STATUS](../../STATUS.md).
- IAM-V1-F2 (IMPLEMENTADO, pendiente gate del propietario; autorizado 2026-11-10; CI #1144
  SUCCESS valida su contenido): administración de Perfiles, Permisos y Usuarios, visibilidad
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
