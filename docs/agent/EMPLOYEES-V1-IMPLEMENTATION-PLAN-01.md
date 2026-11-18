# Empleados V1 — Plan de implementación (PROPUESTO; ninguna fase autorizada)

Decisiones: [ADR-050](../decisions/ADR-050-employees-v1-architecture.md). Comportamiento exacto:
[especificación contractual](../decisions/EMPLOYEES-V1-CONTRACT-SPEC-01.md). Estado de entrega: [STATUS](../../STATUS.md); cola:
[WORK_QUEUE](WORK_QUEUE.md).

**Este plan no autoriza ninguna fase.** Funcionalidad no implementada; EMP-E1 está PENDING / NO AUTORIZADA. Cada fase requiere
autorización expresa del propietario con lista exacta de rutas, gate del propietario (PASS o FAIL) y staging y commit realizados
por el propietario ([flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase)). Sin suite completa de
pruebas; PostgreSQL real para integridad y concurrencia. Etiquetas: APROBADO, EXISTENTE, PROPUESTO y NO VERIFICADO según el ADR.

## Reglas transversales

- Baseline inicial única: criterio de planificación **sujeto a las reglas y al estado real de migraciones del proyecto**; este plan
  no autoriza generar ni modificar migraciones. Su aplicabilidad se verifica en EMP-E1.
- Prohibido en todas las fases: nómina, salarios, contratos laborales, comisiones, reportes de productividad, corrección de
  períodos, `IsPrimary`, catálogo de motivos de baja, módulo o tile «Datos Maestros», funcionalidad clínica o lógica de Spa.
- Prohibido sin autorización específica separada: MasterData, Identity/Access, infraestructura de auditoría, allowlist de
  `IgnoreQueryFiltersAuditTests`, frontend compartido y Kernel.
- `docs/architecture/employees.md`, `MENU_MAP.md`, `FEATURES.md` y `docs/architecture/README.md` solo se actualizan cuando el
  código correspondiente exista y esté comprobado.
- **Ninguna fase habilita un comportamiento que viole INV-08, INV-09 o INV-12.** La baja y la reincorporación solo se exponen y
  habilitan cuando existe, en la misma operación atómica, el cierre del período y de las asignaciones vigentes (INV-09) y la
  no reapertura de asignaciones anteriores (INV-12); esa capacidad se completa en EMP-E5. Una baja que deba cerrar asignaciones y
  todavía no disponga de esa capacidad no se considera completa.
- **No se habilitan altas reales antes de que EMP-E3 esté verificada y cerrada.**
- **Spa no se integra antes de entregar y verificar el contrato `IEmployeeDirectory` (EMP-E6b).**

## EMP-E0 — Documentación

**Objetivo:** ADR-050, especificación y plan; actualización de índices y estado. **Cierre:** guardrail documental, enlaces y
`git diff --check` limpios; cero código.

## EMP-E1 — Verificaciones técnicas (solo lectura)

**Objetivo:** resolver los puntos NO VERIFICADOS antes de programar. **Verifica:**
1. Estado real de migraciones y aplicabilidad de la baseline única; registro de SQL crudo.
2. `naming.md`: nombres definitivos de tablas, columnas, índices y restricciones; longitudes (motivo de baja, nombres de catálogo).
3. `ApiErrorCategory` disponibles y correspondencia HTTP; formato de `ApiResponseCodes`.
4. Patrón de controllers, rutas y paginación.
5. Cálculo del hash y namespaces de advisory locks del precedente; esquema de los locks nuevos de Employees (`"Employees"`,
   `"BpEmployment"`).
6. Reglas de módulos y capas: si Employees puede leer repositorios de MasterData o necesita un puerto de lectura de BP; puerto de
   la guardia propiedad de MasterData.
7. Búsqueda de usuarios en Access: endpoint, permiso y privacidad.
8. Soporte de Employee en el wizard de BP y en las pantallas de MasterData.
9. Conteo de módulos en `KernelRegistryTests` y registro de módulos en Kernel.
10. Normalizador de nombres existente reutilizable (EXISTENTE: ninguno compartido).
11. Estado de las pruebas de arquitectura aplicables a los módulos nuevos.

**Prohibido:** escribir código o documentos. **Cierre:** informe con cada punto resuelto o con su decisión pendiente.

## EMP-E2 — Base: entidades, períodos, catálogos, auditoría y permisos

**Dependencias:** E1. **Objetivo:** modelo de datos y reglas de dominio de la spec §1–§4, §16–§17, sin endpoints de alta.
**Alcance:** `Employee`, `EmployeeEmploymentPeriod`, `EmployeeBranchAssignment` (modelo), catálogos, `EmployeeSpecialty`,
`EmployeeAudit`, `EmployeeCatalogAudit`, `EmployeesPermissions`, normalizador, `EXCLUDE` y unicidades.
**Rutas potenciales:** `ERP.Domain/Employees/**`, `ERP.Application/Employees/**`, `ERP.Infrastructure/Employees/**`,
`ERP.Domain/Kernel/Permissions/EmployeesPermissions.cs`, configuración EF, esquema/migración según el resultado de E1, registro de
SQL crudo, manifiesto de pruebas de Infrastructure.
**Prohibido:** tocar MasterData, Access, auditoría o frontend; exponer endpoints de alta.
**Pruebas:** T-01, T-02 (datos), T-03 (datos), T-11, T-13 parcial. **Cierre:** pruebas en PostgreSQL real y gate del propietario.

## EMP-E3 — Protección de BusinessPartner (autorización separada: MasterData y allowlist)

**Dependencias:** E2. **Objetivo:** cumplir EMP-T-03 y E-D3. **Alcance:** puerto `IBusinessPartnerEmploymentGuard`, implementación en
Infrastructure, guardia en `DeactivateBusinessPartnerHandler` y en `RevokeBusinessPartnerRoleHandler` (solo rol Employee), código
`BP_HAS_ACTIVE_EMPLOYMENT`, lock `("BpEmployment", tenant, bp)`.
**Rutas potenciales:** `ERP.Domain/MasterData/Interfaces`, los dos handlers, `ERP.Infrastructure/Employees/**`,
`IgnoreQueryFiltersAuditTests` (solo la entrada de la guardia), `MessageCatalog`.
**Prohibido:** otros cambios en MasterData; alterar el alcance del BP (ADR-017).
**Pruebas:** T-05, T-06, T-13. **Cierre:** protección demostrada con PostgreSQL real y gate PASS. Hasta este cierre no se habilitan
altas reales.

## EMP-E4 — Alta y edición de la ficha (primera parte del ciclo laboral)

**Dependencias:** E3 cerrada. **Capacidades de ciclo laboral de esta fase:** alta (apertura del primer período) y edición de
cargo, profesión y especialidades. **Fuera de esta fase:** baja y reincorporación, que no se implementan ni se exponen en E4 porque
su operación completa exige el cierre atómico de asignaciones (se entregan en E5).
**Alcance:** alta, edición de cargo, profesión y especialidades, proyección del BP, endpoints y validaciones de EMP-BR-01 a
EMP-BR-07, EMP-BR-10 y EMP-BR-19.
**Pruebas:** T-04 (altas simultáneas), T-07, T-10 (permisos de alta y edición), T-12 (P-01 y P-05). **Cierre:** gate PASS; el alta
real queda habilitada solo después. Hasta E5 no existe baja ni reincorporación.

## EMP-E5 — Asignaciones laborales y ciclo laboral completo (baja y reincorporación)

**Dependencias:** E4. **Objetivo:** completar el ciclo laboral integrando períodos y asignaciones en operaciones atómicas.
**Alcance:** EMP-BR-11, EMP-BR-12; **baja** (EMP-BR-08) con cierre del período y de las asignaciones vigentes en una sola
transacción (INV-09); **reincorporación** (EMP-BR-09; no el mismo día de la baja) sin reabrir asignaciones anteriores (INV-12);
asignaciones contenidas en un período (INV-08); EMP-BR-23.
**Condición previa:** el propietario define el procedimiento de regularización o cancelación de asignaciones futuras antes de
implementar el comportamiento afectado (baja con asignaciones posteriores a su fecha); sin esa definición solo se implementa el
bloqueo preventivo PROPUESTO de EMP-BR-23 y no se inventa otro flujo.
**Pruebas:** T-02 (bordes de reincorporación a nivel de aplicación), T-03, T-04 (reincorporaciones y asignaciones simultáneas),
T-08, T-09, T-10 (permiso `employees.disable`), T-12 (P-07). **Cierre:** gate PASS con las operaciones de baja y reincorporación
verificadas como atómicas en PostgreSQL real. **Prohibido:** tocar `CompanyUserBranch` o permisos.

## EMP-E6 — Vinculación y desvinculación de usuarios

**Dependencias:** E4 y E1. **Alcance:** EMP-BR-13 y EMP-BR-14, `POST /employees/{id}/unlink-user`. Acceso a Identity/Access solo
mediante puertos de lectura; si hiciera falta tocar Access, autorización separada. **Pruebas:** T-08, T-10.

## EMP-E6b — Contrato `IEmployeeDirectory` para Spa

**Dependencias:** E5 y E6. **Alcance:** interfaz y servicio de lectura (spec §19) con referencias históricas estables.
**Pruebas:** T-15. **Condición:** debe cerrarse antes de cualquier integración de Spa. No incluye lógica clínica ni de Spa.

## EMP-E7 — Frontend, Kernel y launcher

**Dependencias:** E4–E6b. **Alcance:** `EmployeesModule.cs`, claves i18n, `frontend/src/modules/employees/**`,
`MODULE_ICON_BY_ID`, pantallas, ficha de tres secciones, catálogos laborales, guard de cambios sin guardar. Si el wizard de BP no
admite Employee: extensión mínima aparte con autorización propia. **Auditoría de reutilización de UI** declarada antes de escribir.
**Pruebas:** T-13, T-14; lint, `npx tsc -b` y build. **Cierre:** aceptación del propietario; entonces `MENU_MAP.md` y, con código
comprobado, `employees.md`, `FEATURES.md` y `docs/architecture/README.md`.

## Condiciones de cierre del módulo

Todas las fases cerradas (con la baja y la reincorporación habilitadas solo desde E5); T-01 a T-15 verdes; documentación sincronizada con el código; ningún componente FROZEN modificado fuera
de las autorizaciones separadas; sin duplicación de datos del BP.
