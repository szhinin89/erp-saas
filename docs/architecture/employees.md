# Empleados — ficha laboral, ciclo laboral, sucursales, usuarios y directorio

**SSOT** del módulo Empleados: comportamiento implementado y comprobado. Otros documentos solo enlazan aquí. La decisión y su
justificación están en [ADR-050](../decisions/ADR-050-employees-v1-architecture.md) y el diseño aprobado en la
[especificación](../decisions/EMPLOYEES-V1-CONTRACT-SPEC-01.md); el estado de entregas, en [STATUS](../../STATUS.md).

---

## Responsabilidades y límites

- Registra la relación laboral de un BusinessPartner con una empresa: ficha, períodos, cargo, profesión, especialidades,
  sucursales laborales y vínculo opcional con un usuario ERP.
- No duplica identidad ni contactos (se leen del BusinessPartner), no crea BP ni asigna roles, no crea cuentas, membresías,
  perfiles ni permisos y no modifica `CompanyUserBranch`. **El estado laboral y el acceso al ERP son independientes.**
- Sin nómina, salarios, contratos laborales, comisiones, agenda, horarios ni disponibilidad.
- Es solo backend: no hay pantallas, menú, launcher ni módulo de Kernel; los permisos `employees.*` no son asignables a perfiles,
  de modo que hoy solo los ejerce el Admin conforme a las reglas oficiales de autorización.

## Modelo

| Tabla | Contenido | Garantías físicas |
|---|---|---|
| `employees` | Ficha por `(company_id, business_partner_id)`; `identity_user_id`, cargo y profesión opcionales; `is_active` = hay período abierto | `uq_employees_company_business_partner`; `uq_employees_company_identity_user` (parcial) |
| `employee_employment_periods` | Períodos con `start_date` / `end_date` (inclusivas) y motivo de baja | `uq_employee_employment_periods_open`; `ex_employee_employment_periods_no_overlap`; CHECK de fechas |
| `employee_branch_assignments` | Sucursal laboral con fechas | `ex_employee_branch_assignments_no_overlap` (empleado y sucursal); CHECK de fechas |
| `employee_job_positions`, `employee_professions`, `employee_specialties` | Catálogos por empresa | Nombre normalizado único por empresa |
| `employee_specialty_links` | Especialidades de la ficha; retirar y reasignar alterna `is_active` | Una fila por empleado y especialidad |
| `employee_audit`, `employee_catalog_audit` | Auditoría | Solo inserción |

Todas son de empresa (`ICompanyOperationalEntity`) y usan `xmin` como token de concurrencia (`rowVersion`). `TenantId` y
`CompanyId` son inmutables: sin setter en el dominio y con `SetAfterSaveBehavior(Throw)` en EF. No hay borrado físico.

## BusinessPartner

- El alta exige BP del tenant, activo, con rol Employee activo e identificación admitida para el uso `Employee`.
- La ficha proyecta del BP solo identificador, nombre visible, tipo y número de identificación.
- Un BP con ficha activa en cualquier empresa del tenant no se desactiva ni pierde el rol Employee
  (`IBusinessPartnerEmploymentGuard`, `BP_HAS_ACTIVE_EMPLOYMENT`).

## Catálogos

Consulta, alta, renombre, activación e inactivación por empresa. El nombre se recorta, colapsa espacios y se compara sin
distinguir mayúsculas ni tildes. Renombrar, activar e inactivar exigen `rowVersion`. Un valor inactivo se conserva donde ya está
asignado y no admite asignaciones nuevas.

## Ficha y ciclo laboral

- **Alta:** crea la ficha y su primer período (sin cotas de fecha). Cargo, profesión y especialidades son opcionales.
- **Edición:** solo cargo y profesión (`PATCH` distingue campo omitido de `null`) y conjunto de especialidades.
- **Baja:** cierra el período abierto con fecha fin ≥ inicio y motivo opcional (≤ 500), acorta las asignaciones vigentes a esa fecha
  y deja la ficha inactiva, todo en una transacción. Las asignaciones ya terminadas no cambian. Una asignación que empiece después
  de la fecha de baja bloquea la operación. No revoca el acceso ERP; si el usuario vinculado (no global) tiene membresía activa en
  la empresa, la respuesta incluye la advertencia `ACTIVE_MEMBERSHIP` (no se persiste ni aparece en consultas).
- **Reincorporación:** exige ficha en baja, BP y rol activos y un inicio estrictamente posterior al último fin. No reabre períodos
  ni asignaciones, no asigna sucursales y conserva cargo, profesión y especialidades.
- Los períodos registrados no se editan ni se eliminan.

## Asignaciones a sucursales

Crear exige ficha activa, sucursal activa de la misma empresa, rango contenido en un período laboral (sin fecha fin solo si el
período está abierto) y sin solapamiento en la misma sucursal. Se admiten varias sucursales simultáneas y no hay sucursal
principal. Cerrar exige asignación abierta, `rowVersion` de la asignación y fecha fin dentro del período. Desactivar después una
sucursal conserva las asignaciones.

## Vinculación de usuarios

- Una ficha admite un usuario y un usuario una ficha por empresa. Una ficha vinculada no se reemplaza: se desvincula primero.
- Vincular exige membresía **activa** en la empresa y cuenta no global (regla única `IamIdentityScope`); un usuario desactivado con
  membresía activa se admite. Los casos fuera de alcance responden igual (`EMPLOYEE_USER_NO_MEMBERSHIP`).
- Se permite en fichas activas o en baja. Desvincular no toca la cuenta, la membresía ni los permisos.
- `identityUser` (`id`, `username`, `membershipActive`) aparece en la ficha y en las respuestas de sus escrituras, no en la lista.
  Una cuenta que pasa a global se muestra como `null` (IAM-V1-F1) y puede desvincularse.

## API y permisos

Base `api/v1/employees`; envelope oficial; paginación `skip/take`; sin `DELETE`.

| Operación | Permiso |
|---|---|
| `GET /`, `GET /{id}`, `GET /{id}/branch-assignments` (filtro de lista `branchId` + `asOf`, por defecto hoy de la empresa) | `employees.view` |
| `POST /` | `employees.create` |
| `PATCH /{id}`, `PUT /{id}/specialties` | `employees.update` |
| `POST /{id}/terminate`, `POST /{id}/rehire` | `employees.disable` |
| `POST /{id}/branch-assignments`, `POST …/{assignmentId}/close` | `employees.assignments.manage` |
| `POST /{id}/link-user` | `employees.link-user` **y** `access.company_user_memberships.view` |
| `POST /{id}/unlink-user` | `employees.link-user` |
| `GET /job-positions`, `/professions`, `/specialties` y `/{id}` | `employees.catalogs.view` |
| `POST`, `PATCH /{id}`, `POST /{id}/activate` y `/deactivate` en esos catálogos | `employees.catalogs.manage` |

Los códigos de respuesta son las constantes `ApiResponseCodes.Employees` con su entrada en `MessageCatalog`.

## Seguridad y aislamiento

Tenant y empresa salen del contexto autenticado, nunca del cuerpo. Cada consulta del módulo los aplica como predicado propio, además
del filtro global. Un registro ajeno se comporta como inexistente. La lectura entre empresas se limita a la guardia de BP y a las
membresías, con tenant explícito y sin `IgnoreQueryFilters`.

## Concurrencia

- Las escrituras de la ficha usan una transacción propia o la ambiente, un lock consultivo (que exige READ COMMITTED) y
  lecturas posteriores al lock. Las de catálogos no toman locks: las protege `rowVersion` y la unicidad del nombre.
- Locks consultivos: `Employees` por ficha (edición, especialidades, baja, asignaciones, vínculo) y `BpEmployment` por tenant y BP
  (alta; desactivación del BP y revocación del rol). La reincorporación toma ambos, en ese orden; nunca el inverso.
- Referencias bloqueadas `FOR SHARE` durante la escritura: catálogos asignados, sucursal y membresía.
- Una versión obsoleta responde `CONCURRENCY_CONFLICT` sin cambios. En baja, reincorporación, cierre de asignación, vínculo y
  catálogos se comprueba tras la existencia y pertenencia y antes de las reglas; en la edición de la ficha y de especialidades se
  comprueba al persistir, después de validar las referencias.

## Auditoría

Eventos de dominio procesados por `EmployeeAuditHandler` y `EmployeeCatalogAuditHandler` en la misma transacción: ficha (`Created`,
`Updated` por campo, `Terminated`, `Rehired`, `BranchAssigned`, `BranchAssignmentClosed`, `UserLinked`, `UserUnlinked`) y catálogos
(`Created`, `Renamed`, `Deactivated`, `Activated`). Solo identificadores y fechas, sin datos personales. Un rechazo o conflicto no
audita. No existe consulta de esta auditoría por API.

## `IEmployeeDirectory`

Contrato oficial de lectura para otros módulos (`ERP.Application/Modules/Employees/Interfaces`), implementado en Infrastructure y
registrado `Scoped`.

| Método | Resultado |
|---|---|
| `GetByIdAsync` | Resumen de la ficha (también en baja) o nulo |
| `ListAsync(companyId, includeInactive, filtro)` | Sin paginación; `companyId` distinto del operativo → vacío; filtro solo `specialtyId` activo |
| `IsActiveOnAsync(id, fecha)` | Existe un período que cubre la fecha |
| `GetBranchAssignmentsAsync(id, sucursal?, fecha?)` | Historial o asignaciones que cubren la fecha |
| `IsEligibleForBranchAsync(id, sucursal, fecha)` | Período **y** asignación a la sucursal que cubren la fecha |
| `GetLinkedUserAsync(id)` | Identificador del usuario vinculado visible según IAM, aunque la membresía o la cuenta estén inactivas; nulo en otro caso |

- Fechas inclusivas; se evalúan con los registros, no con `is_active`. La elegibilidad no mira sucursal, usuario, membresías,
  permisos, acceso ERP ni estado del BP: habilitar la operación en la sucursal es del consumidor.
- Tenant y empresa salen de `IOperationalContext`; sin ellos el directorio falla (invariante interna).
- No comprueba permisos: cada consumidor aplica los suyos y no entrega nombres ni identificadores sin autorizarlo.
- Solo lectura, sin transacciones ni locks; la lista usa un número fijo de consultas. No garantiza una instantánea entre sentencias.

**Consumo transversal:** otros módulos usan únicamente este contrato y sus modelos. No acceden a entidades, repositorios, eventos ni
tablas de Empleados (`EmployeesBoundaryTests`). Hoy no tiene consumidores.

## Limitaciones conocidas

- No hay procedimiento para regularizar asignaciones futuras al dar de baja: solo el bloqueo preventivo.
- La protección de `TenantId`/`CompanyId` es de EF, no una restricción de PostgreSQL frente a SQL directo.
- El paso concurrente de una cuenta a global no se puede bloquear sin modificar Identity (único origen identificado: first-run).
- Una membresía revocada después de vincular conserva el vínculo.

## Pruebas

`ERP.Domain.Tests/Employees`, `ERP.Application.Tests/Employees`, `ERP.Infrastructure.Tests/Modules/Employees` (PostgreSQL real),
`ERP.API.Tests/Employees`, `EmployeesPermissionContractTests` y `ERP.Architecture.Tests/EmployeesBoundaryTests`.
