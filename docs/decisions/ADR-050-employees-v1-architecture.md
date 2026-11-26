# ADR-050 — Empleados V1 (módulo técnico independiente `Employees`)

Estado: **ACCEPTED / APROBADO** — fecha de aceptación formal: **2026-11-24**. El propietario aprueba expresamente
ADR-050 — Empleados V1 como arquitectura de referencia. Esta aceptación conserva las decisiones funcionales y técnicas
anteriores, incluida la aprobación funcional durante la planificación (decisiones de §3), sin alterarlas ni dar por
cumplidas las verificaciones técnicas pendientes.
**La aceptación aprobó el diseño; por sí sola no autorizaba código, endpoints, permisos, migraciones, cambios de base
de datos ni cambios en módulos FROZEN/CLOSED.** Cada fase requirió después autorización expresa del propietario.
**Estado vigente (2026-11-26): Empleados V1 implementado y CLOSED** (EMP-E1 a EMP-E7).

El comportamiento exacto está en la [especificación contractual](EMPLOYEES-V1-CONTRACT-SPEC-01.md) (diseño, no SSOT) y el
orden de trabajo en el [plan de implementación](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md). El comportamiento
implementado se define en la SSOT [employees.md](../architecture/employees.md)
([enforcement](../architecture/enforcement.md#ssot-por-módulo)). Estado de entrega: [STATUS](../../STATUS.md).

**Convención de etiquetas** (la aprobación de la documentación no convierte las propuestas técnicas en decisiones):

- **APROBADO:** decisión funcional o arquitectónica del propietario.
- **EXISTENTE:** comprobado en el código del repositorio.
- **PROPUESTO:** mecanismo técnico no implementado, pendiente de validación.
- **NO VERIFICADO:** comprobación asignada a una fase posterior (EMP-E1 salvo indicación).

## 1. Contexto

- EXISTENTE: `RoleType.Employee` (valor 3) existe en BusinessPartner y el catálogo SRI siembra `Employee` solo para cédula (05)
  y pasaporte (06); `IIdentificationUsageValidator` valida el uso de la identificación por rol.
- EXISTENTE: no hay entidad `Employee`, períodos laborales, sucursales laborales, catálogos de cargo, profesión o especialidad,
  permisos `employees.*`, módulo de Kernel `employees` ni pantallas.
- EXISTENTE: BusinessPartner es subscriber-scoped y FROZEN ([ADR-017](ADR-017-business-partner-scope.md)); no puede llevar
  `CompanyId` ni rediseñarse hacia `Person`.
- EXISTENTE: `DeactivateBusinessPartnerHandler` y `RevokeBusinessPartnerRoleHandler` no consultan dependencias; el segundo
  conserva un `TODO ADR-BP-14`.
- La relación laboral es por empresa y no puede vivir en BusinessPartner. Futuros módulos (primero Gali Spa) necesitan
  identificar al profesional que presta un servicio y atribuirle empresa, sucursal y fechas.

## 2. Objetivos y alcance V1

**Objetivos:** (1) módulo general e independiente, válido para cualquier empresa; (2) una ficha laboral por BusinessPartner y
empresa con historial de ingresos, bajas y reincorporaciones; (3) sucursales laborales independientes del acceso de usuario;
(4) contrato de lectura reutilizable por módulos consumidores; (5) ningún mecanismo paralelo de identidad, autorización,
auditoría o contabilidad.

**Incluye:** `Employee`, `EmployeeEmploymentPeriod`, `EmployeeBranchAssignment`, catálogos por empresa de cargo, profesión y
especialidad, vínculo opcional con usuario ERP, auditoría, protección de BusinessPartner, contrato mínimo `IEmployeeDirectory`
y módulo visual propio en el launcher.

**Excluye (V1):** nómina, salarios, contratos laborales, comisiones, distribución de costos laborales, reportes de
productividad, RRHH completo, corrección de períodos históricos y catálogo de motivos de baja. El diseño solo conserva los datos
(empleado, empresa, sucursal, fechas, usuario) para futuros consumidores (nómina por sucursal, productividad, ingresos
atribuibles); no implementa cálculo alguno.

## 3. Decisiones aprobadas (APROBADO)

| ID | Decisión |
|---|---|
| DEC-E1 | Módulo general e independiente; una ficha por BusinessPartner y empresa; reutilizable por Spa y cualquier negocio; sin RRHH completo ni nómina |
| DEC-E2 | Reutilizar `IdentityUser`, `CompanyUserMembership` y `CompanyUserBranch`; empleado sin usuario permitido; vincular no otorga permisos; solo el sistema de seguridad vigente |
| DEC-E3 | Buscar el BP por identificación y reutilizarlo; si no existe, registrarlo por el proceso oficial; usar `RoleType.Employee`; no duplicar identidad, contactos ni información personal |
| DEC-E4 | Profesionales de Spa que documentan atenciones requieren usuario ERP autorizado; la atención identifica al profesional responsable y al usuario que ejecutó el registro |
| DEC-E5 | Cargo, profesión y especialidad son catálogos distintos y por empresa; un cargo y una profesión por empleado; varias especialidades |
| DEC-E6 | Una ficha por empresa con varias sucursales laborales e historial; no confundir sucursales laborales con sucursales autorizadas al usuario |
| EMP-T-01 | Módulo Employees independiente, con dominio, aplicación, infraestructura y frontend propios |
| EMP-T-02 | Permisos separados; `employees.create` no autoriza operaciones de BP; el alta de BP y la asignación del rol Employee usan endpoints y permisos oficiales de MasterData |
| EMP-T-03 | Bloquear la desactivación del BP y la revocación del rol Employee si existe una ficha laboral activa en cualquier empresa del mismo tenant; extensión mínima y controlada de MasterData, autorizable por separado |
| EMP-T-04 | Solo cédula y pasaporte para Employee; no ampliar la matriz SRI |
| EMP-T-05 | Empleados se administra por empresa; no se restringe mediante `CompanyUserBranch` |
| EMP-T-06 | Historial estructurado: una ficha con varios `EmployeeEmploymentPeriod`, sin períodos superpuestos |
| EMP-T-07 | Auditoría común de los catálogos de cargo, profesión y especialidad con Entity Audit oficial |
| EMP-T-08 | Desvinculación mediante `POST /employees/{id}/unlink-user`, sin eliminar la cuenta ni modificar sus permisos |
| EMP-T-09 | Una única regla de normalización de nombres en catálogos |
| EMP-T-10 | Empleados tiene su propio módulo visual en el launcher, al nivel de Clientes y Proveedores; **no** se crea una sección ni tile «Datos Maestros» (corrección definitiva) |
| E-D1 | BP con RUC incompatible: bloquear el alta laboral y remitir a revisión de identidad en MasterData; sin duplicar BP ni cambiar su identificación automáticamente |
| E-D2 | Módulo visual propio Employees, con menú, permisos y pantallas independientes |
| E-D3 | La protección de EMP-T-03 se implementa y verifica antes de habilitar altas operativas reales; sus cambios sobre MasterData requieren autorización expresa |
| E-D4 | Reincorporación permitida sin sucursal laboral; el empleado no es elegible para operaciones que requieran sucursal hasta tener una asignación válida |
| P-01 | `JobPositionId` y `ProfessionId` son opcionales al crear la ficha; se completan después; las operaciones que los requieran validan su presencia; ningún consumidor asume que existen |
| P-02 | Los períodos históricos no se editan libremente; V1 solo crea, finaliza y reincorpora mediante operaciones controladas; la corrección queda para una funcionalidad futura con autorización y auditoría específicas; no hay pantalla de corrección en V1 |
| P-03 | `employees.disable` controla baja y reincorporación; no se crean permisos adicionales |
| P-04 | Sin `IsPrimary` en `EmployeeBranchAssignment`; varias sucursales simultáneas; la ausencia de sucursal principal no afecta la validez de la ficha |
| P-05 | `employees.view` permite consultar los datos mínimos de identificación del BP mediante una proyección de solo lectura; sin duplicarlos en Employee ni editar BP |
| P-06 | Buscar y seleccionar BP en el alta exige `masterdata.businesspartners.view`; crear BP o asignar el rol exige los permisos oficiales de MasterData; `employees.create` no los sustituye |
| P-07 | Motivo de baja opcional, texto libre de longitud acotada según las convenciones del repositorio, registrado con el período y la auditoría; sin catálogo de motivos |

Precisiones de planificación documentadas por el propietario en la ejecución documental:

- **Reincorporación (consecuencia del modelo temporal):** con períodos de fechas inclusivas, V1 no permite reincorporar el
  mismo día de la baja (ver §7).
- **Baja con asignaciones futuras:** se documenta un bloqueo preventivo como PROPUESTO; el procedimiento de regularización o
  cancelación de esas asignaciones queda **pendiente de definición** antes de implementar la baja (ver §8 y §17).

## 4. Arquitectura

- APROBADO: módulo técnico `Employees` con dominio, aplicación, infraestructura y frontend propios.
- PROPUESTO: entidades `Employee`, `EmployeeEmploymentPeriod`, `EmployeeBranchAssignment`, `JobPosition`, `Profession`,
  `Specialty`, `EmployeeSpecialty`, `EmployeeAudit`, `EmployeeCatalogAudit`.
- EXISTENTE (patrón): `ICompanyOperationalEntity` exige `CompanyId` y aplica un filtro fail-closed de tenant y empresa;
  `Employee` debe implementarla. Referencia al BP; no copia nombre, identificación ni contacto.
- APROBADO: el módulo no incorpora autenticación, autorización, auditoría ni contabilidad propias.
- Las fechas de negocio son `DateOnly` ([ADR-034](ADR-034-temporal-contract-single-source.md)).
- Integridad temporal y concurrencia: ver §12, que distingue los patrones EXISTENTES de los mecanismos PROPUESTOS.

## 5. Identidad y BusinessPartner

- APROBADO: BP es la identidad oficial; se mantiene su rol `Employee`.
- APROBADO: tipos admitidos, cédula y pasaporte. EXISTENTE: la validación reutiliza `IIdentificationUsageValidator` con
  `IdentificationUsageType.Employee`, y el catálogo sembrado coincide con la decisión.
- APROBADO (E-D1): BP con RUC incompatible bloquea el alta laboral y se remite a revisión de identidad en MasterData; no se
  duplica el BP ni se cambia su identificación.
- APROBADO: Empleados no crea ni modifica BP ni asigna roles; solo los lee mediante una proyección de solo lectura de campos
  mínimos (P-05).
- APROBADO (EMP-T-03, E-D3): protección del BP. PROPUESTO: puerto propiedad de MasterData, implementado en Infrastructure, que
  consulta fichas activas del mismo tenant en todas sus empresas y devuelve solo un booleano (ver §12).

## 6. Seguridad

- APROBADO: catálogo `employees.view`, `employees.create`, `employees.update`, `employees.disable`,
  `employees.assignments.manage`, `employees.link-user`, `employees.catalogs.view`, `employees.catalogs.manage`. PROPUESTO:
  declaración en `ERP.Domain/Kernel/Permissions/EmployeesPermissions.cs`.
- APROBADO: baja y reincorporación con `employees.disable` (P-03).
- APROBADO: los perfiles existentes no reciben estos permisos automáticamente; Admin conserva su comportamiento oficial; no
  se crea otro mecanismo de roles.
- APROBADO: administración por empresa operativa; no depende de `CompanyUserBranch`.
- APROBADO: las asignaciones laborales nunca conceden permisos ni modifican `CompanyUserBranch`.
- APROBADO: vincular o desvincular un usuario no cambia membresías, perfiles ni permisos.
- `company_id` nunca se acepta del body como autoridad.

## 7. Períodos laborales

APROBADO: una ficha conserva todos sus períodos; solo uno puede estar abierto; ninguno se solapa con otro, incluidos los
cerrados. V1 permite crear (alta), finalizar (baja) y reincorporar (período nuevo sobre la misma ficha); no hay edición ni
corrección de períodos registrados (P-02). La baja cierra las asignaciones vigentes. El motivo de baja es opcional (P-07).

**Consecuencia del modelo temporal (precisión del propietario):** los períodos usan fechas inclusivas. Una baja con fecha D y
un ingreso con fecha D se solaparían, por lo que **V1 no permite reincorporar el mismo día de la baja**: el nuevo período debe
iniciar en una fecha posterior a la fecha de baja del período anterior. Las pruebas de borde están en la especificación
(T-02).

## 8. Sucursales laborales

APROBADO: `EmployeeBranchAssignment` registra empleado, sucursal y fechas, con historial. Un empleado puede tener varias
sucursales simultáneas; no hay sucursal principal (P-04). No se superponen asignaciones del mismo empleado a la misma sucursal.
La sucursal pertenece a la misma empresa y la asignación es compatible con un período laboral. Una reincorporación puede
quedar sin sucursal; mientras tanto el empleado no es elegible para operaciones que exijan sucursal (E-D4). No duplica
`CompanyUserBranch`.

**Baja con asignaciones posteriores a su fecha:** PROPUESTO: la baja se bloquea de forma preventiva y no elimina ni modifica
silenciosamente esas asignaciones. El procedimiento de regularización o cancelación de asignaciones futuras **permanece
pendiente de definición por el propietario** antes de implementar la baja; este ADR no inventa un flujo adicional.

## 9. Catálogos

APROBADO: cargo, profesión y especialidad son catálogos independientes por empresa, con nombre y valor normalizado mediante
una única regla (EMP-T-09), unicidad por empresa y valor normalizado, y activación o inactivación sin eliminación física. Los
valores históricos se conservan. Un empleado tiene como máximo un cargo y una profesión (opcionales, P-01) y varias
especialidades.

## 10. Auditoría

APROBADO: Entity Audit oficial ([audit-infrastructure](../architecture/audit-infrastructure.md)). PROPUESTO: entidades
`EmployeeAudit` y `EmployeeCatalogAudit` (una tabla común para los tres catálogos, EMP-T-07), eventos de dominio levantados desde
el agregado, `*AuditHandler` en Application y configuración EF propia. Prohibido modificar la infraestructura de auditoría o
añadir columnas de actor. El motivo de baja se registra con el período y la auditoría.

## 11. Frontend y launcher

APROBADO: módulo `[Module("employees")]` en Kernel, al nivel de Clientes y Proveedores. No existe módulo ni tile «Datos
Maestros». Pantallas propias con ficha de tres secciones (General, Sucursales, Acceso ERP). `MENU_MAP.md` no se actualiza hasta
que el módulo exista en código (su fuente ejecutable es `Kernel/Modules/*.cs`). NO VERIFICADO: si el wizard de BP admite el rol
Employee; si no, la extensión mínima requiere autorización propia.

## 12. Integridad, concurrencia y protección de BusinessPartner

### 12.1 Mecanismos EXISTENTES (precedentes verificados)

| Mecanismo | Precedente |
|---|---|
| `EXCLUDE USING gist` con `btree_gist` | `accounting_periods` (baseline `InitialProductionBaseline`, SQL crudo) y `RawSqlDatabaseObjectsSurviveMigrationSquashTests` |
| Índices parciales (`HasFilter`) | `AccountingPostingConfiguration` |
| Token `xmin` como `IsRowVersion` | `AccountingOperationalPolicyConfiguration` |
| `pg_advisory_xact_lock` y `_shared` con clave `(int4,int4)` | `AccountingOperationalPolicyRepository` |
| `PlatformQueryAccessor.AsPlatformQuery()`: `internal` en `ERP.Infrastructure`, equivale a `IgnoreQueryFilters()` y no aplica `TenantId` | Allowlist de archivos en `IgnoreQueryFiltersAuditTests`; lectura multiempresa con tenant explícito en `AccessRepository` |
| Filtro fail-closed de tenant y empresa para `ICompanyOperationalEntity` | `EnterpriseQueryFilterConfigurator` |

### 12.2 Mecanismos PROPUESTOS (nuevos; a verificar en EMP-E1 antes de programar)

- Namespaces y claves de advisory locks de Employees (`"Employees"` por ficha y `"BpEmployment"` por `(tenantId, businessPartnerId)`)
  y la generación de sus claves `(int4,int4)` reutilizando el cálculo del precedente.
- Puerto `IBusinessPartnerEmploymentGuard` propiedad de MasterData, con implementación en Infrastructure.
- Entrada del archivo de la guardia en la allowlist de `IgnoreQueryFiltersAuditTests` (autorización específica).
- Serialización de la carrera «crear o reincorporar» contra «desactivar o revocar» con el mismo lock exclusivo, comprobada con
  PostgreSQL real.
- Estrategia de baseline inicial única: criterio de planificación **sujeto a las reglas y al estado real de migraciones del
  proyecto**; este ADR no autoriza generar ni modificar migraciones.

## 13. Integración con Spa

APROBADO: Spa reutiliza Employee y no crea otro registro laboral. Empleados entrega `IEmployeeDirectory` como contrato mínimo
de lectura dentro de V1 (ver especificación §19). Es el contrato oficial, especificado y **pendiente de implementación
y verificación**; la aceptación de este ADR no lo declara implementado. Spa conserva sus permisos clínicos, agenda, horarios, sesiones y
disponibilidad, y registra por separado el profesional responsable (`EmployeeId`) y el usuario que registró; el cobro no se
confunde con quien presta el servicio. Spa exige usuario ERP autorizado a quien documente atenciones; esa exigencia pertenece a
Spa. Empleados no implementa funcionalidad clínica ni lógica de Spa. La integración de Spa no comienza antes de entregar y
verificar el contrato.

## 14. Alternativas descartadas

1. Agregar `CompanyId` o campos laborales a BusinessPartner: contradice ADR-017 (FROZEN).
2. Un módulo o tile «Datos Maestros» para Empleados: descartado por EMP-T-10.
3. Duplicar nombre, identificación o contacto en Employee: viola DEC-E3.
4. Usar `CompanyUserBranch` como sucursal laboral: mezcla acceso con relación laboral (DEC-E6).
5. Otorgar permisos desde la asignación laboral: prohibido (DEC-E2).
6. Crear otra ficha al reincorporar: pierde historial; se reutiliza la ficha.
7. Catálogo de motivos de baja, `IsPrimary` o edición libre de períodos: descartados en V1 (P-07, P-04, P-02).
8. Ampliar la matriz SRI a RUC para Employee: descartado por EMP-T-04.
9. Auditoría propia o `UserActivity` como auditoría de negocio: prohibido.

## 15. Restricciones FROZEN/CLOSED

- No se modifica la infraestructura de Entity Audit, mensajes, secuencias ni tracking.
- No se altera el alcance de BP ni sus entidades; la extensión de MasterData se limita a las guardias de desactivación y
  revocación, con autorización separada.
- Identity y Access se usan sin cambios; solo lectura y vinculación del identificador de usuario.
- La allowlist de `IgnoreQueryFiltersAuditTests` solo se amplía con el archivo de la guardia, con autorización específica.
- Compras, Ventas, Items y demás módulos CLOSED no se reabren.

## 16. Riesgos

| # | Riesgo | Control |
|---|---|---|
| 1 | Hoy se puede desactivar un BP o revocar su rol con ficha activa | Guardia en E3 antes de altas reales |
| 2 | Carrera entre alta y desactivación | Lock común y prueba con PostgreSQL real |
| 3 | `PlatformQueryAccessor` no aplica filtro de tenant | `TenantId` explícito, resultado booleano, prueba de aislamiento entre tenants |
| 4 | `EXCLUDE` no modelado por EF | SQL crudo en la baseline y registro en `RawSqlDatabaseObjectsSurviveMigrationSquashTests` |
| 5 | Dependencia MasterData → Employees | Puerto propiedad de MasterData |
| 6 | Consumidores asumen cargo o profesión presentes | Contrato nullable (P-01) |
| 7 | Confusión entre sucursal laboral y acceso | Reglas y prueba negativa (INV-11) |
| 8 | Wizard de BP sin soporte verificado para Employee | Verificación en EMP-E1; extensión mínima con autorización propia |
| 9 | Asignaciones futuras al dar de baja: flujo de regularización sin definir | Bloqueo preventivo PROPUESTO; definición del propietario antes de implementar la baja |

## 17. Pendientes antes de implementar

- Verificaciones técnicas de EMP-E1 (lista en el [plan](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md)).
- Definición del propietario del procedimiento de regularización o cancelación de asignaciones futuras (§8), antes de implementar el
  comportamiento afectado (baja con asignaciones posteriores, EMP-E5). Hasta entonces solo existe el bloqueo preventivo PROPUESTO
  (EMP-BR-23); el mecanismo de regularización no está aprobado.
- Autorizaciones separadas: MasterData, allowlist de arquitectura, Kernel, frontend compartido, y Access si fuera necesario.

## 18. Condiciones de implementación

1. Autorización expresa del propietario y lista exacta de rutas por fase.
2. Baseline inicial única como criterio de planificación, sujeta a las reglas y al estado real de migraciones (§12.2).
3. Protección de BP implementada y comprobada con PostgreSQL real antes de habilitar altas reales (E-D3).
4. Flujo por fase de [enforcement](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase); gate, staging y commit los
   realiza el propietario.
5. Spa no se implementa dentro de ninguna fase de este ADR.
6. Ninguna fase habilita un comportamiento que viole INV-08, INV-09 o INV-12: la baja y la reincorporación se implementan y exponen
   solo cuando el cierre atómico del período y de las asignaciones laborales está disponible (EMP-E5); EMP-E4 solo entrega el alta
   y la edición de la ficha.

## 19. Consecuencias

- Una única fuente laboral reutilizable por Spa y por futuros módulos de RRHH, sin rehacer la identidad.
- Dependencia explícita de MasterData mediante una guardia entre empresas, con dos autorizaciones separadas.
- La nómina, la productividad y la distribución de costos se podrán apoyar en `Employee`, sus períodos y sus asignaciones.

## Referencias

[ADR-017](ADR-017-business-partner-scope.md) · [ADR-034](ADR-034-temporal-contract-single-source.md) ·
[ADR-049](ADR-049-sales-order-v1-architecture.md) · [Especificación](EMPLOYEES-V1-CONTRACT-SPEC-01.md) ·
[Plan](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md) · [Auditoría](../architecture/audit-infrastructure.md) ·
[Seguridad](../architecture/security.md) · [Enforcement](../architecture/enforcement.md)
