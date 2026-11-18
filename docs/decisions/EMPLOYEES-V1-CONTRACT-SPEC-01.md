# Empleados V1 — Especificación contractual (diseño aprobado, NO implementado)

Documento de diseño, **no SSOT**. Decisiones y justificación en [ADR-050](ADR-050-employees-v1-architecture.md); orden de trabajo
en el [plan de implementación](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md). **Nada de lo descrito está implementado ni
autorizado.** Cuando exista código probado, la SSOT nacerá en `docs/architecture/employees.md`
([enforcement](../architecture/enforcement.md#ssot-por-módulo)).

Etiquetas: **APROBADO** (decisión del propietario) · **EXISTENTE** (comprobado en el código) · **PROPUESTO** (mecanismo técnico
pendiente de validación; no es decisión aprobada) · **NO VERIFICADO** (comprobación asignada a una fase posterior). Los
nombres de tablas, columnas, rutas, códigos de error y puertos de este documento son PROPUESTOS salvo indicación expresa; los
identificadores de decisión (DEC-E, EMP-T, E-D, P) y las reglas funcionales referidas a ellos son APROBADOS.

Fuera de alcance V1: nómina, salarios, contratos laborales, comisiones, reportes de productividad, distribución de costos
laborales, corrección de períodos, `IsPrimary` y catálogo de motivos de baja.

## 1. Entidades, propiedades y tipos (PROPUESTO; `snake_case` según [naming](../architecture/naming.md))

Comunes a toda entidad nueva: `id uuid PK`, `tenant_id uuid NOT NULL`, `created_at`/`created_by`, `updated_at`/`updated_by` y
token de concurrencia `xmin` (`Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`, patrón EXISTENTE).

### 1.1 `employees` — `Employee` (`ICompanyOperationalEntity`)

| Columna | Tipo | Restricción |
|---|---|---|
| company_id | uuid | NOT NULL; inmutable |
| business_partner_id | uuid | NOT NULL; FK a `master_business_partners` |
| identity_user_id | uuid | NULL; FK a `IdentityUser` (DEC-E2) |
| job_position_id | uuid | NULL; FK a `employee_job_positions` (P-01) |
| profession_id | uuid | NULL; FK a `employee_professions` (P-01) |
| is_active | boolean | NOT NULL; ver INV-06 |

Sin nombre, identificación ni contacto: se leen del BP (DEC-E3).

### 1.2 `employee_employment_periods` — `EmployeeEmploymentPeriod`

`employee_id` FK, `company_id`, `start_date date NOT NULL`, `end_date date NULL`, `termination_reason varchar(500) NULL` (P-07;
longitud máxima conforme al precedente EXISTENTE de motivos en texto libre, p. ej. `reverse_reason`; valor definitivo a
confirmar en EMP-E1).

### 1.3 `employee_branch_assignments` — `EmployeeBranchAssignment`

`employee_id` FK, `company_id`, `branch_id` FK a `Branch`, `start_date date NOT NULL`, `end_date date NULL`. Sin `is_primary`
(P-04).

### 1.4 Catálogos: `employee_job_positions` (`JobPosition`), `employee_professions` (`Profession`), `employee_specialties` (`Specialty`)

`company_id`, `name varchar(150) NOT NULL`, `normalized_name varchar(150) NOT NULL`, `is_active boolean NOT NULL`.

### 1.5 `employee_specialty_links` — `EmployeeSpecialty`

`employee_id` FK, `specialty_id` FK, `company_id`, `is_active boolean NOT NULL`.

### 1.6 Auditoría

`employee_audit` (`EmployeeAudit`) y `employee_catalog_audit` (`EmployeeCatalogAudit`, con tipo de catálogo), heredando de
`AuditRecordBase` con `ConfigureAuditBase` (patrón EXISTENTE: `PricingRuleAudit`).

## 2. Relaciones, claves, índices y unicidad (PROPUESTO)

| Objeto | Definición |
|---|---|
| UX-EMP-01 | `UNIQUE (company_id, business_partner_id)` en `employees` |
| UX-EMP-02 | `UNIQUE (company_id, identity_user_id) WHERE identity_user_id IS NOT NULL` |
| UX-PER-01 | `UNIQUE (employee_id) WHERE end_date IS NULL` (un único período abierto) |
| EX-PER-01 | `EXCLUDE USING gist (employee_id WITH =, daterange(start_date, end_date, '[]') WITH &&)` |
| CK-PER-01 | `CHECK (end_date IS NULL OR end_date >= start_date)` |
| EX-ASG-01 | `EXCLUDE USING gist (employee_id WITH =, branch_id WITH =, daterange(start_date, end_date, '[]') WITH &&)` |
| CK-ASG-01 | `CHECK (end_date IS NULL OR end_date >= start_date)` |
| UX-CAT-01 | `UNIQUE (company_id, normalized_name)` en cada catálogo |
| UX-SPL-01 | `UNIQUE (employee_id, specialty_id)` |
| IX-EMP-01 | `(tenant_id, business_partner_id) WHERE is_active`, para la guardia de BP |

Los `EXCLUDE` requieren `btree_gist` (EXISTENTE en la baseline) y se declaran con SQL crudo (EF no los modela; precedente
`accounting_periods`); deben registrarse en `RawSqlDatabaseObjectsSurviveMigrationSquashTests` (EXISTENTE). El rango `'[]'` es
cerrado (fechas inclusivas). Nombres definitivos, orden de columnas y longitudes: NO VERIFICADO, EMP-E1.

## 3. Invariantes

| ID | Invariante | Garantía |
|---|---|---|
| INV-01 | Una ficha por `(company_id, business_partner_id)` | UX-EMP-01 |
| INV-02 | Un usuario vinculado a una sola ficha por empresa | UX-EMP-02 |
| INV-03 | `company_id` inmutable | Dominio sin setter; prueba |
| INV-04 | A lo sumo un período abierto por ficha | UX-PER-01 |
| INV-05 | Períodos sin solapamiento, incluidos los cerrados | EX-PER-01 |
| INV-06 | `is_active` ⇔ existe un período abierto (`end_date IS NULL`) | Dominio, en la misma transacción |
| INV-07 | Asignaciones del mismo empleado y sucursal sin solapamiento | EX-ASG-01 |
| INV-08 | Sucursal de la misma empresa y asignación contenida en un período laboral | Dominio y aplicación bajo lock |
| INV-09 | La baja cierra las asignaciones vigentes | Dominio |
| INV-10 | Un BP con ficha activa no se desactiva ni pierde el rol Employee | Guardia (§12) |
| INV-11 | Asignar no crea ni modifica `CompanyUserBranch` ni permisos | Prueba negativa |
| INV-12 | La reincorporación no reabre asignaciones anteriores | Dominio |
| INV-13 | Un nuevo período inicia estrictamente después de la fecha de baja del anterior | EX-PER-01 y dominio |

## 4. Estados y transiciones

| Estado | Condición | Transiciones |
|---|---|---|
| Activo | Existe un período abierto | `terminate` → Baja |
| Baja | Sin período abierto | `rehire` → Activo |

El alta crea la ficha y su primer período (estado Activo). No existe eliminación; una ficha en Baja se conserva para histórico.
«Activo en la fecha D» se evalúa con los rangos de los períodos; `is_active` solo indica período abierto.

**Reincorporación el mismo día de la baja (APROBADO como consecuencia del modelo temporal).** Con fechas inclusivas, un período
cerrado con `end_date = D` y un período nuevo con `start_date = D` se solapan. **V1 no permite reincorporar el mismo día de la
baja:** `start_date` del nuevo período debe ser mayor que el `end_date` del anterior. Pruebas de borde en T-02.

## 5. Reglas de negocio

| ID | Origen | Regla | Permiso | Errores (PROPUESTO) |
|---|---|---|---|---|
| EMP-BR-01 | DEC-E1 | Una ficha por BP y empresa | `employees.create` | EMPLOYEE_ALREADY_EXISTS |
| EMP-BR-02 | DEC-E3, E-D1, EMP-T-04 | El alta exige BP existente del tenant, activo, con rol Employee activo e identificación admitida (cédula o pasaporte). Con RUC incompatible se bloquea y se remite a revisión de identidad en MasterData; no se duplica ni convierte | `employees.create` | EMPLOYEE_BP_INACTIVE, EMPLOYEE_BP_ROLE_MISSING, EMPLOYEE_IDENTIFICATION_NOT_ALLOWED |
| EMP-BR-03 | EMP-T-02, P-06 | Empleados no crea BP ni asigna el rol; esos pasos usan MasterData con sus permisos. Buscar el BP exige `masterdata.businesspartners.view` | MasterData | — |
| EMP-BR-04 | DEC-E1 | `company_id` inmutable | — | — |
| EMP-BR-05 | P-01 | Cargo y profesión opcionales al crear y editables después; las operaciones que los requieran validan su presencia en su propio módulo; ningún consumidor asume que existen | `employees.update` | EMPLOYEE_CATALOG_INACTIVE si el valor está inactivo |
| EMP-BR-06 | DEC-E5 | Varias especialidades por empleado; retirar y reasignar alterna `is_active` en la misma fila, con trazabilidad en auditoría | `employees.update` | EMPLOYEE_CATALOG_INACTIVE |
| EMP-BR-07 | EMP-T-06 | El alta abre el primer período con `start_date` | `employees.create` | EMPLOYEE_PERIOD_OVERLAP |
| EMP-BR-08 | EMP-T-06, P-03, P-07 | Baja: cierra el período con `end_date >= start_date`, cierra las asignaciones vigentes, motivo opcional. No revoca el acceso ERP; si la membresía sigue activa devuelve una advertencia | `employees.disable` | EMPLOYEE_ALREADY_TERMINATED, EMPLOYEE_INVALID_DATE |
| EMP-BR-09 | E-D4, P-03 | Reincorporación: ficha en Baja, BP y rol activos, período nuevo con `start_date` posterior al `end_date` anterior (no el mismo día), sin sucursal; no reabre asignaciones | `employees.disable` | EMPLOYEE_ALREADY_ACTIVE, EMPLOYEE_PERIOD_OVERLAP, EMPLOYEE_BP_INACTIVE |
| EMP-BR-10 | P-02 | Prohibido editar períodos registrados; no existe pantalla ni endpoint de corrección en V1 | — | — |
| EMP-BR-11 | DEC-E6, P-04 | Asignación: empleado activo, sucursal activa de la misma empresa, fechas dentro de un período, sin solapamiento de la misma sucursal; varias sucursales simultáneas; sin sucursal principal | `employees.assignments.manage` | EMPLOYEE_BRANCH_INVALID, EMPLOYEE_ASSIGNMENT_OVERLAP, EMPLOYEE_ASSIGNMENT_OUTSIDE_PERIOD |
| EMP-BR-12 | E-D4 | Elegible por sucursal = activo en la fecha y con asignación vigente a esa sucursal en esa fecha | — | — |
| EMP-BR-13 | DEC-E2 | Vincular usuario: membresía activa en la misma empresa, usuario no vinculado a otra ficha de la empresa; no crea membresías, perfiles ni permisos | `employees.link-user` | EMPLOYEE_USER_NO_MEMBERSHIP, EMPLOYEE_USER_ALREADY_LINKED |
| EMP-BR-14 | EMP-T-08 | Desvincular: `identity_user_id = null`; no elimina la cuenta ni toca membresía ni permisos; el rastro queda en auditoría | `employees.link-user` | EMPLOYEE_USER_NOT_LINKED |
| EMP-BR-15 | DEC-E5 | Catálogos por empresa; activación e inactivación sin borrado físico; valores históricos conservados | `employees.catalogs.manage` | EMPLOYEE_CATALOG_DUPLICATE |
| EMP-BR-16 | EMP-T-09 | Una única regla de normalización para los tres catálogos (§16) | `employees.catalogs.manage` | EMPLOYEE_CATALOG_DUPLICATE |
| EMP-BR-17 | EMP-T-03, E-D3 | Guardia de BP (§12): bloquear la desactivación y la revocación del rol Employee con ficha activa en cualquier empresa del tenant | permisos de MasterData | BP_HAS_ACTIVE_EMPLOYMENT |
| EMP-BR-18 | EMP-T-05 | Administración por empresa operativa; sin dependencia de `CompanyUserBranch` | todos | EMPLOYEE_NOT_FOUND para fichas de otra empresa |
| EMP-BR-19 | P-05 | `employees.view` devuelve los datos mínimos de identificación del BP por proyección de solo lectura; Employees no edita BP | `employees.view` | — |
| EMP-BR-20 | EMP-T-07 | Toda operación genera auditoría oficial | — | — |
| EMP-BR-21 | DEC-E4 | Contrato `IEmployeeDirectory` (§19) | — | — |
| EMP-BR-22 | DEC-E1 | Sin nómina, salarios, contratos laborales, comisiones ni reportes de productividad | — | — |
| EMP-BR-23 | PROPUESTO | **Baja con asignaciones posteriores a su fecha:** se bloquea preventivamente; no se eliminan ni modifican esas asignaciones | `employees.disable` | EMPLOYEE_TERMINATION_BLOCKED_BY_FUTURE_ASSIGNMENTS |

**Pendiente de definición del propietario (antes de implementar el comportamiento afectado: la baja con asignaciones posteriores,
EMP-E5):** el procedimiento de regularización o cancelación de asignaciones futuras. Este documento no define un flujo adicional ni
lo clasifica como aprobado; mientras no exista, el bloqueo preventivo de EMP-BR-23 (PROPUESTO) es el único comportamiento descrito.

**Habilitación de baja y reincorporación (consistencia con INV-08, INV-09 e INV-12):** la baja y la reincorporación se implementan y
exponen juntas en EMP-E5, junto con las asignaciones, porque la baja cierra el período y las asignaciones vigentes en una sola
operación atómica y la reincorporación no reabre las anteriores. En EMP-E4 solo existen el alta y la edición de la ficha.

## 6. Validaciones

- Fechas de negocio `YYYY-MM-DD` (`DateOnly`, [ADR-034](ADR-034-temporal-contract-single-source.md)); sin horas.
- Nombres de catálogo: obligatorios, recortados, longitud máxima 150.
- Motivo de baja: opcional, longitud máxima según §1.2.
- `businessPartnerId`, `branchId`, `identityUserId` y catálogos pertenecen al mismo tenant y a la empresa operativa (el BP es del
  tenant).
- Cuatro capas de validación según [enforcement](../architecture/enforcement.md): Zod, FluentValidation, dominio y base de datos.

## 7. Permisos por operación

| Operación | Permiso |
|---|---|
| Listar y ver empleados, períodos y asignaciones | `employees.view` |
| Crear ficha | `employees.create` (más `masterdata.businesspartners.view` para buscar el BP) |
| Editar cargo, profesión y especialidades | `employees.update` |
| Baja y reincorporación | `employees.disable` |
| Crear y cerrar asignaciones | `employees.assignments.manage` (la consulta usa `employees.view`) |
| Vincular y desvincular usuario | `employees.link-user` |
| Consultar catálogos | `employees.catalogs.view` |
| Administrar catálogos | `employees.catalogs.manage` |
| Crear o editar BP, asignar o reactivar el rol Employee | Permisos oficiales de MasterData (`masterdata.businesspartners.create`, `.update` y el permiso del endpoint de roles) |

APROBADO: catálogo de ocho permisos, separación respecto de MasterData, perfiles existentes sin los permisos nuevos, Admin con
su comportamiento oficial. PROPUESTO: declaración en el Kernel y el esquema `employees.<acción>` (EXISTENTE: el Kernel admite
nombres de dos y tres segmentos, y guiones, p. ej. `masterdata.businesspartners.configure-company`).

## 8. API conceptual (PROPUESTO, salvo `unlink-user`, APROBADO; base `api/v1/employees`, envelope oficial)

| Método y ruta | Descripción |
|---|---|
| `GET /employees` | Lista paginada; filtros: estado, texto (nombre o identificación), cargo, profesión, especialidad, sucursal |
| `GET /employees/{id}` | Ficha con proyección del BP, períodos, asignaciones, especialidades y usuario |
| `POST /employees` | Crea ficha y primer período |
| `PATCH /employees/{id}` | Cargo y profesión |
| `PUT /employees/{id}/specialties` | Conjunto de especialidades activas |
| `POST /employees/{id}/terminate` | Baja |
| `POST /employees/{id}/rehire` | Reincorporación |
| `GET /employees/{id}/branch-assignments` | Asignaciones |
| `POST /employees/{id}/branch-assignments` | Crea asignación |
| `POST /employees/{id}/branch-assignments/{assignmentId}/close` | Cierra asignación |
| `POST /employees/{id}/link-user` | Vincula usuario |
| `POST /employees/{id}/unlink-user` | Desvincula usuario (APROBADO, EMP-T-08) |
| `GET/POST/PATCH /employees/job-positions`, `/professions`, `/specialties`; `POST …/{id}/deactivate` y `/activate` | Catálogos |

No existe `DELETE`. Rutas definitivas y patrón de controllers: NO VERIFICADO, EMP-E1.

## 9. Contratos de entrada y salida (PROPUESTO)

- `POST /employees`: `{ businessPartnerId, startDate, jobPositionId?, professionId?, specialtyIds[]? }` → `EmployeeDto`.
- `terminate`: `{ endDate, reason? }` → `EmployeeDto` con `warnings[]` (por ejemplo `ACTIVE_MEMBERSHIP`).
- `rehire`: `{ startDate }` → `EmployeeDto`.
- `branch-assignments` (POST): `{ branchId, startDate, endDate? }`; `close`: `{ endDate }`.
- `link-user`: `{ identityUserId }`; `unlink-user`: sin cuerpo.
- `EmployeeDto`: `id`, `companyId`, `businessPartner { id, displayName, identificationType, identificationNumber }`, `isActive`,
  `jobPosition { id, name, isActive }?`, `profession { … }?`, `specialties[]`, `identityUser { id, username, membershipActive }?`,
  `periods[]`, `assignments[]`, `rowVersion`.
- Las actualizaciones reciben `rowVersion` y rechazan una versión obsoleta.

## 10. Errores (PROPUESTO)

Cada código se declara en `ApiResponseCodes.Employees` con entrada en `MessageCatalog` (severidad, mensaje de usuario y de
desarrollador y `ApiErrorCategory`; EXISTENTE: el catálogo es la fuente única y `ResponseFactory` deriva el HTTP de la
categoría). Códigos: `EMPLOYEE_NOT_FOUND`, `EMPLOYEE_ALREADY_EXISTS`, `EMPLOYEE_BP_INACTIVE`, `EMPLOYEE_BP_ROLE_MISSING`,
`EMPLOYEE_IDENTIFICATION_NOT_ALLOWED`, `EMPLOYEE_PERIOD_OVERLAP`, `EMPLOYEE_ALREADY_TERMINATED`, `EMPLOYEE_ALREADY_ACTIVE`,
`EMPLOYEE_INVALID_DATE`, `EMPLOYEE_TERMINATION_BLOCKED_BY_FUTURE_ASSIGNMENTS`, `EMPLOYEE_BRANCH_INVALID`,
`EMPLOYEE_ASSIGNMENT_OVERLAP`, `EMPLOYEE_ASSIGNMENT_OUTSIDE_PERIOD`, `EMPLOYEE_USER_NO_MEMBERSHIP`, `EMPLOYEE_USER_ALREADY_LINKED`,
`EMPLOYEE_USER_NOT_LINKED`, `EMPLOYEE_CATALOG_DUPLICATE`, `EMPLOYEE_CATALOG_INACTIVE`, `EMPLOYEE_CONCURRENT_MODIFICATION`; en
MasterData: `BP_HAS_ACTIVE_EMPLOYMENT`. Las categorías disponibles (solo se verificaron `BusinessRule` y `Duplicate`) y la
correspondencia HTTP: NO VERIFICADO, EMP-E1.

## 11. Transacciones y concurrencia

| Aspecto | Patrón EXISTENTE | Mecanismo PROPUESTO (verificar en EMP-E1) |
|---|---|---|
| Unicidad y exclusión temporal | Índices únicos parciales y `EXCLUDE USING gist` (`accounting_periods`) | Objetos de §2 |
| Token de concurrencia | `xmin` con `IsRowVersion` | Mismo patrón en `employees` y demás agregados |
| Lock transaccional | `pg_advisory_xact_lock` y `_shared` con clave `(int4,int4)` (`AccountingOperationalPolicyRepository`) | Namespace `"Employees"` por ficha y `"BpEmployment"` por `(tenant_id, business_partner_id)`; generación de claves tomada del precedente |
| Lectura entre empresas | `AsPlatformQuery()` interno con tenant explícito (`AccessRepository`) | Implementación de la guardia en Infrastructure |

1. Cada comando mutante usa una transacción y toma el lock `("Employees", employee_id)` y verifica `rowVersion`.
2. Alta y reincorporación toman además el lock `("BpEmployment", tenant_id, business_partner_id)` en modo exclusivo y, dentro de él,
   verifican BP activo y rol activo.
3. Doble alta: lo resuelve UX-EMP-01; el perdedor recibe `EMPLOYEE_ALREADY_EXISTS`.
4. Períodos o asignaciones simultáneos: lo resuelven UX-PER-01, EX-PER-01 y EX-ASG-01; la violación se traduce al código
   correspondiente.
5. Alta (ficha y período), baja (cierre de período y asignaciones) y reincorporación son operaciones atómicas.

## 12. Protección de BusinessPartner entre empresas

- PROPUESTO: puerto `IBusinessPartnerEmploymentGuard`, propiedad de MasterData: `HasActiveEmploymentAsync(tenantId, businessPartnerId, ct) → bool`.
- PROPUESTO: implementación en `ERP.Infrastructure` (módulo Employees): consulta `employees` con `AsPlatformQuery()` y filtro
  explícito `tenant_id = @tenantId AND is_active`, sin filtrar por empresa. Devuelve únicamente un booleano; no revela empresa ni
  ficha.
- EXISTENTE: `PlatformQueryAccessor` es `internal`, no aplica `TenantId` y su uso se restringe a archivos de la allowlist de
  `IgnoreQueryFiltersAuditTests`. El archivo de la guardia debe agregarse a esa allowlist con **autorización específica**.
- PROPUESTO: `DeactivateBusinessPartnerHandler` y `RevokeBusinessPartnerRoleHandler` (solo si el rol es `Employee`) toman el lock
  `("BpEmployment", tenant_id, business_partner_id)`, consultan la guardia y devuelven `BP_HAS_ACTIVE_EMPLOYMENT` si es verdadero.
- El `tenantId` proviene del contexto autenticado; no hay acceso entre tenants.
- APROBADO: esta extensión se implementa y verifica antes de cualquier alta real (E-D3) y requiere autorización expresa.

## 13. Historial de períodos

Los períodos nunca se borran ni se editan (P-02). La baja fija `end_date`; la reincorporación inserta un período nuevo posterior
(§4). El historial es consultable en la ficha y en `IEmployeeDirectory`. La corrección de períodos es una funcionalidad futura
que requiere autorización y auditoría específicas.

## 14. Sucursales laborales

Las asignaciones tienen fechas e historial. Cerrar una asignación fija su `end_date`. La baja cierra automáticamente las
vigentes y se bloquea si existen asignaciones posteriores a su fecha (EMP-BR-23, PROPUESTO). La reincorporación no reabre
asignaciones anteriores. Una asignación no crea, modifica ni requiere `CompanyUserBranch` y no concede permisos. No hay sucursal
principal.

## 15. Vinculación y desvinculación de usuarios

La vinculación exige membresía activa en la empresa y unicidad por empresa (INV-02). Se respetan los controles de privacidad de
Identity/Access: la búsqueda de usuarios usa los endpoints y permisos oficiales del módulo de acceso (NO VERIFICADO cuáles,
EMP-E1; si hiciera falta una extensión de Access requiere autorización propia). Ninguna operación de Empleados crea
`IdentityUser`, membresías, perfiles ni permisos. Al desvincular se conserva el rastro en auditoría.

## 16. Catálogos y normalización

PROPUESTO: una sola implementación de normalización, compartida por los tres catálogos: recortar, colapsar espacios internos y
comparar sin distinguir mayúsculas ni tildes. EXISTENTE: no hay un normalizador de nombres reutilizable (solo
`PurchaseLineNormalizer`, local de Compras); se crea uno dentro de Employees tras verificarlo en EMP-E1. Desactivar un valor no
altera las fichas que ya lo usan; no se puede asignar un valor inactivo a una ficha nueva o editada.

## 17. Auditoría

Eventos de dominio levantados desde el agregado y procesados por `*AuditHandler` hacia `EmployeeAudit` y `EmployeeCatalogAudit`.
Se auditan: alta, cambio de cargo o profesión, cambio de especialidades, baja (con motivo), reincorporación, creación y cierre de
asignaciones, vinculación, desvinculación y cambios de catálogo. No se modifica la infraestructura de auditoría; el actor se
obtiene de `AuditActor` ([audit-infrastructure](../architecture/audit-infrastructure.md)).

## 18. Frontend, navegación y responsive

APROBADO (EMP-T-10, E-D2): módulo visual propio en el launcher, al nivel de Clientes y Proveedores; sin «Datos Maestros». PROPUESTO
el resto:

- Menú: `Empleados [Module("employees")]` → «Gestión de empleados» → «Empleados» (`/employees`); «Catálogos laborales»
  (`/employees/catalogs`) con una pestaña por catálogo (patrón Lista→Editor de configuración).
- Ficha `/employees/:id` con `ZHTabBar` y tres secciones: **General** (identidad en solo lectura, cargo, profesión,
  especialidades, estado, historial de períodos), **Sucursales** (asignaciones abiertas y cerradas) y **Acceso ERP** (usuario,
  estado de membresía, vincular y desvincular).
- Alta: buscar y seleccionar el BP por identificación (exige `masterdata.businesspartners.view`); si no existe o no tiene rol
  Employee, remitir a los flujos de MasterData; luego crear la ficha.
- Reutilizar `ErpPageTemplate`, `ZHTabBar`, `ZHDataTable`, formularios RHF + Zod, componentes ZH, mensajes y guard de cambios sin
  guardar; sin estilos en línea; sin formularios de identificación duplicados. Declarar la auditoría de reutilización de UI antes
  de implementar.
- Responsive según [frontend](../architecture/frontend.md) (verificar en EMP-E7). i18n bajo `app.nav.item.employees.*`; icono en
  `MODULE_ICON_BY_ID`.
- NO VERIFICADO: si el wizard de BP admite el rol Employee; si no, la extensión mínima requiere autorización propia.
- `MENU_MAP.md` solo se actualiza cuando el módulo exista en código.

## 19. Contrato mínimo `IEmployeeDirectory` (APROBADO como necesidad de V1; firma PROPUESTA)

Interfaz de Application, consumible en proceso, parte de Empleados V1:

- `GetByIdAsync(employeeId)` → `EmployeeSummary?`
- `ListAsync(companyId, includeInactive, filtros)`
- `IsActiveOnAsync(employeeId, date)` → bool
- `GetBranchAssignmentsAsync(employeeId, branchId?, date?)`
- `IsEligibleForBranchAsync(employeeId, branchId, date)` → bool (EMP-BR-12)
- `GetLinkedUserAsync(employeeId)` → `IdentityUserId?`

`EmployeeSummary`: `employeeId`, `companyId`, `businessPartnerId`, `displayName`, `identityUserId?`, `isActive`, `jobPositionId?`,
`professionId?`, `specialtyIds[]`, `periods[]`, `assignments[]`.

Garantías: solo lectura; ids estables y nunca reutilizados; inactivos incluidos con `includeInactive` para atenciones históricas;
cargo y profesión pueden ser nulos (P-01). No incluye permisos clínicos, agenda, horarios ni disponibilidad (de Spa). Spa registra
por separado el empleado responsable y el usuario que registra, y no se implementa funcionalidad de Spa dentro de Empleados.

## 20. Matriz de pruebas y criterios de aceptación

| Prueba | Cubre | Criterio de aceptación |
|---|---|---|
| T-01 | INV-01, INV-02 | Segunda ficha o segundo vínculo del mismo usuario en la empresa falla (PostgreSQL real) |
| T-02 | INV-04, INV-05, INV-13 | Segundo período abierto y períodos solapados fallan. **Bordes:** baja con `end_date = D` y reingreso con `start_date = D` se rechaza; reingreso con `start_date = D+1` se acepta; período de un solo día `start = end` válido; `end_date < start_date` se rechaza |
| T-03 | INV-07, INV-08 | Asignación solapada, de otra empresa o fuera de período falla |
| T-04 | Concurrencia | Dos altas, dos reincorporaciones y dos asignaciones simultáneas dejan un único resultado |
| T-05 | EMP-BR-17 | La carrera crear o reincorporar contra desactivar o revocar nunca deja ficha activa con BP inactivo o sin rol |
| T-06 | Aislamiento | Ficha de otra empresa → `EMPLOYEE_NOT_FOUND`; la guardia ve todas las empresas del tenant y ninguna de otro tenant |
| T-07 | EMP-BR-02 | RUC bloqueado; cédula y pasaporte aceptados; BP o rol inactivos rechazados |
| T-08 | INV-11 | Asignar no crea `CompanyUserBranch` ni permisos; vincular y desvincular no alteran membresías |
| T-09 | INV-09, INV-12, EMP-BR-12, EMP-BR-23 | La baja cierra asignaciones; reincorporar no las reabre; sin sucursal no es elegible; la baja con asignaciones posteriores se bloquea sin modificar datos |
| T-10 | Permisos | Cada operación sin su permiso falla; los perfiles existentes no reciben los permisos; `employees.create` no habilita acciones de BP |
| T-11 | Auditoría | Cada operación deja un registro oficial |
| T-12 | P-01, P-05, P-07 | Alta sin cargo ni profesión; la proyección del BP no expone datos de más; baja sin motivo es válida |
| T-13 | Arquitectura | Allowlist de `IgnoreQueryFilters`, SQL crudo registrado, manifiesto de pruebas de Infrastructure, controllers delgados, módulo en Kernel |
| T-14 | Frontend | Menú por permiso, pestañas, guard de cambios sin guardar, formularios, responsive |
| T-15 | EMP-BR-21 | `IEmployeeDirectory`: activo en fecha, elegibilidad por sucursal, inactivos históricos, nulos en cargo y profesión |

## 21. Trazabilidad decisión → requisito → fase → prueba

| Decisión | Requisitos | Fase | Pruebas |
|---|---|---|---|
| DEC-E1 | EMP-BR-01, 04, 22 | E2 | T-01 |
| DEC-E2 | EMP-BR-13, INV-11 | E5, E6 | T-08 |
| DEC-E3 | EMP-BR-02, 03, 19 | E4 | T-07, T-12 |
| DEC-E4 | EMP-BR-21, §19 | E6b | T-15 |
| DEC-E5 | EMP-BR-05, 06, 15 | E2, E4 | T-12 |
| DEC-E6 | EMP-BR-11 | E5 | T-03, T-08 |
| EMP-T-01 | §1–§2 | E2 | T-13 |
| EMP-T-02 | EMP-BR-03 | E4 | T-10 |
| EMP-T-03 | EMP-BR-17 | E3 | T-05, T-06 |
| EMP-T-04 | EMP-BR-02 | E4 | T-07 |
| EMP-T-05 | EMP-BR-18 | E2–E7 | T-06, T-10 |
| EMP-T-06 | EMP-BR-07, 08, 09 | E2, E4 (alta), E5 (baja y reincorporación) | T-02, T-04 |
| EMP-T-07 | EMP-BR-20, §17 | E2 | T-11 |
| EMP-T-08 | EMP-BR-14 | E6 | T-08 |
| EMP-T-09 | EMP-BR-16 | E2 | T-01 (catálogos) |
| EMP-T-10, E-D2 | §18 | E7 | T-14 |
| E-D1 | EMP-BR-02 | E4 | T-07 |
| E-D3 | EMP-BR-17 | E3 | T-05 |
| E-D4 | EMP-BR-09, 12 | E5 | T-09 |
| P-01 | EMP-BR-05 | E2, E4 | T-12 |
| P-02 | EMP-BR-10 | E4 | T-02 |
| P-03 | EMP-BR-08, 09 | E5 | T-10 |
| P-04 | EMP-BR-11 | E5 | T-03 |
| P-05 | EMP-BR-19 | E4 | T-12 |
| P-06 | EMP-BR-03 | E4, E7 | T-10 |
| P-07 | EMP-BR-08 | E5 | T-12 |

Fase de las pruebas ligadas a baja y reincorporación: T-02 (bordes de reincorporación a nivel de aplicación), T-09, T-10 (permiso
`employees.disable`) y T-12 (motivo de baja) se ejecutan en EMP-E5; T-04 cubre altas en E4 y reincorporaciones y asignaciones en E5.
Las restricciones de datos de T-02 se prueban desde E2. Las pruebas T-01 a T-15 se conservan.
