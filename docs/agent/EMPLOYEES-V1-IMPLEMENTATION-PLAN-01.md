# Empleados V1 — Plan de implementación (EMP-E1 a EMP-E6b cerradas; EMP-E7 pendiente de CI)

Decisiones: [ADR-050](../decisions/ADR-050-employees-v1-architecture.md). Comportamiento exacto:
[especificación contractual](../decisions/EMPLOYEES-V1-CONTRACT-SPEC-01.md). Estado de entrega: [STATUS](../../STATUS.md); cola:
[WORK_QUEUE](WORK_QUEUE.md).

**Este plan no autoriza por sí mismo ninguna fase.** Estado (2026-11-25): EMP-E1 **VERIFICACIÓN TÉCNICA COMPLETADA / CIERRE APROBADO
POR EL PROPIETARIO**; EMP-E2 a EMP-E6b **COMPLETADO / CLOSED**; **EMP-E7 IMPLEMENTADO / GATE APROBADO / ACEPTACIÓN DEL PROPIETARIO / PENDIENTE CI**. Cada fase requirió
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

**Prohibido:** escribir código y crear documentos nuevos. **Cierre:** informe con cada punto resuelto o con su decisión pendiente.

### Resultado de la ejecución de EMP-E1 (2026-11-24; solo lectura de código; verificación técnica, no implementación)

Autorizada por el propietario como verificación de solo lectura. No se modificó código, BD, migraciones, Kernel, Access ni MasterData, y
no se ejecutaron suites ni compilaciones: «Verificado» significa comprobado leyendo el código o las SSOT citados. **Cierre propuesto:
verificación técnica completada; cierre aprobado por el propietario, condicionado a las validaciones documentales.** No cambia ninguna decisión aprobada ni autoriza E2–E7 ni E6b.

| # | Punto | Mecanismo EXISTENTE y evidencia | Reutilizable | Brecha real | Autorización / decisión |
|---|---|---|---|---|---|
| 1 | Migraciones y baseline | Una sola migración `20261101012214_InitialProductionBaseline` (+ snapshot) como única cadena, sin upgrade-path ([DATABASE](../DATABASE.md#migraciones)). Los objetos raw SQL viven al final de `Up` (con su `Down`), con `btree_gist` modelada (`HasPostgresExtension`). Dos commits posteriores la editaron en sitio (`72a9afc3d`, `f4f04c272`). `dotnet ef migrations add` omite el SQL crudo. El registro es doble: diccionario `RequiredRawSqlObjects` y prueba «todo objeto raw está registrado» (`RawSqlDatabaseObjectsSurviveMigrationSquashTests`) y definición física en `RawSqlDatabaseObjectsBaselineIntegrationTests` (ya cubre un `EXCLUDE … daterange(…,'[]')`). `xmin` con `IsRowVersion` confirmado (`AccountingOperationalPolicyConfiguration`). | `EXCLUDE` de `accounting_periods` como plantilla literal; patrón `xmin`; índices parciales (`HasFilter` en uso) | Las tablas de Employees exigen editar a mano la baseline, su `Designer` y el snapshot (no regenerar) y añadir los tres `EXCLUDE`/`CHECK` al registro. Una BD ya migrada no se actualiza: se recrea (o se acepta una migración incremental, contraria a la política vigente). | Autorización del propietario para tocar la baseline/esquema (E2). **Decisión pendiente:** baseline editada en sitio (precedente) frente a migración incremental. |
| 2 | Nombres e identificadores | [naming](../architecture/naming.md): `snake_case`, índices `ix_/ux_/uq_`, FK `fk_*` con `_tenant_`. Precedentes: `ex_accounting_periods_company_no_overlap`, `ck_items_nature`, `uq_mbp_identification`; tablas `master_business_partners` e `identity_users` (destinos de FK). Longitudes: motivo en texto libre `HasMaxLength(500)` (`Payment.reverse_reason`, `JournalEntry.reverse_reason`). | Nombres de la spec §1–§2 compatibles; 500 confirmado para `termination_reason` | `naming.md` no fija longitudes ni prefijo de tabla por módulo; `EmployeeAudit` usa `ConfigureAuditBase("…_audit")` (patrón confirmado). 150 para catálogos no tiene precedente directo. | Ninguna. Confirmar 150 al implementar E2. |
| 3 | Errores y HTTP | `ApiErrorCategory` (10): Validation 422, BusinessRule 400, Duplicate 409, NotFound 404, Authentication 401, Authorization 403, Infrastructure 503, Integration 502, RateLimit 429, InternalError 500 (`ApiErrorStatus`). Los códigos son constantes `SCREAMING_SNAKE_CASE` en clases anidadas de `ApiResponseCodes`, con entrada obligatoria en `MessageCatalog` (prueba «una entrada por código»). Comunes ya existentes: `CONFLICT`, `UNIQUE_VIOLATION`, `CONCURRENCY_CONFLICT` (Duplicate) y `DOMAIN_RULE_VIOLATION` (**Validation, 422**, no 400). | Correspondencia propuesta: `EMPLOYEE_NOT_FOUND` → NotFound; `*_ALREADY_EXISTS`, `*_OVERLAP`, `USER_ALREADY_LINKED`, `CATALOG_DUPLICATE`, `CONCURRENT_MODIFICATION` → Duplicate; el resto → BusinessRule | `EMPLOYEE_CONCURRENT_MODIFICATION` duplica `CONCURRENCY_CONFLICT`; `EMPLOYEE_INVALID_DATE` admite Validation o BusinessRule | **Decisión pendiente** (propietario, en E2): reutilizar el código común o uno propio; categoría de `INVALID_DATE`. |
| 4 | Controllers, rutas y paginación | `[ApiController] [Route("api/v1/…")] [Authorize]` + política por acción `perm:{Const}`; MediatR; `ToOkOrBadRequest`; `PagedResult<T>`. Paginación con **dos convenciones**: `skip/take` (`BusinessPartnersController`) y `page/pageSize` (`ActivityController`). Los controllers deben ser delgados (`ApiControllerGuardrailTests`). `[AppFeature]` en controllers de menú. | Patrón completo para `api/v1/employees` | Elegir convención de paginación. Rutas `employees/job-positions` y `employees/{id}` no colisionan con `{id:guid}`. | Decisión menor en E4: `skip/take` o `page/pageSize`. |
| 5 | Locks | `pg_advisory_xact_lock(int4,int4)` vía `ExecuteSqlInterpolatedAsync`; clave = (hash del namespace UTF-8, hash de `Guid.ToByteArray()`); hash estable `h=17; h=h*31+b` copiado en cada repositorio (`AccountingOperationalPolicy`, `AccountRepository`, `JournalEntry`, `DocumentSequence`); exige transacción activa (lanza si no) y omite en no relacional. Variantes: `hashtext()` (`CompanyRepository`) y `hashtextextended` bigint (`InventoryCostLedger`). | `"Employees"` por ficha encaja directo (namespace + id) | (a) `"BpEmployment"` necesita **(tenant, BP)** = dos Guid en una sola ranura `int4`: combinar los bytes en un hash o usar la forma `bigint`; una colisión solo serializa de más. (b) No existe helper compartido de hash. (c) `DeactivateBusinessPartnerHandler` y `RevokeBusinessPartnerRoleHandler` **no usan transacción** (solo `SaveChanges`); tomar el lock exige `IUnitOfWork.BeginTransactionAsync` (patrón `PaymentUseCases`). (d) Orden: Employees → BpEmployment (alta/reincorporación) frente a solo BpEmployment (desactivar/revocar): sin ciclo. | Decisión técnica en E2/E3: forma de la clave. El cambio de transacción en los dos handlers pertenece a la autorización de MasterData de E3. |
| 6 | Capas y puerto de BP | `LayerDependencyTests`: Domain sin EF/ASP.NET/Infrastructure; Application sin Infrastructure/EF; Infrastructure sin API. No hay regla que impida a Employees usar interfaces de dominio de MasterData. `IBusinessPartnerRepository` (filtro de tenant) ofrece `GetByIdAsync` y `GetDisplayInfoByIdsAsync` → `BusinessPartnerDisplayInfo(Id, TradeName, LegalName, IdentificationNumber, IdentificationType)`, que coincide con la proyección mínima de P-05. `PlatformQueryAccessor.AsPlatformQuery()` es `internal` de `ERP.Infrastructure`. | Proyección P-05 sin puerto nuevo; la guardia se implementa en Infrastructure con tenant explícito | **Hallazgo:** `IgnoreQueryFiltersAuditTests` solo busca el texto `.IgnoreQueryFilters(`; ~20 archivos usan `AsPlatformQuery()` sin entrada en la allowlist. Una guardia que use `AsPlatformQuery()` **no necesitaría** entrada en la allowlist. No se halló otro gate sobre `AsPlatformQuery` (solo una fixture en `tools/architecture`). La caracterización `BusinessPartnerDeactivationPostgreSqlTests` fija hoy que desactivar con roles activos tiene éxito; deberá ajustarse en E3 junto con la guardia. | Autorización de MasterData (E3). **Decisión pendiente:** si se mantiene la autorización específica de la allowlist (ADR-050 §12) o se registra como innecesaria. |
| 7 | Búsqueda de usuarios (Access) | `GET api/v1/admin/iam/memberships/lookup?username=` (exacto, ≤50; clase con `access.company_user_memberships.view`): devuelve `UsernameLookupDto(IdentityUserExists, FullName, Membership)`; una cuenta global o de otro tenant responde como inexistente. `GET …/memberships` lista las de la empresa con `IdentityUserId`, `Username`, `FullName`, `IsActive` (sin búsqueda ni paginación). Puerto de dominio `IAccessRepository`: `GetCompanyUserMembershipAsync(companyId, userId)`, `GetUserByIdAsync`, `GetActiveGlobalUserIdsAsync`. | Validar membresía activa de EMP-BR-13 y rechazar cuentas globales por el puerto, sin tocar Access | La UI de vinculación necesita `access.company_user_memberships.view` además de `employees.link-user`; no hay búsqueda textual de usuarios. La spec no define si se pueden vincular cuentas globales (el puerto permite detectarlas). | Sin extensión de Access. **Decisión pendiente:** exigir ambos permisos (análogo a P-06) y excluir cuentas globales. |
| 8 | Wizard de BP | `MasterDataPartnerWizard` declara `type Role = "customer" \| "supplier"` (también `BP_CONTEXT_ROLE`, `businessPartnerSchema(role)`); solo lo usan las páginas de Clientes y Proveedores. Backend ya admite Employee: `RoleType.Employee = 3`, `MapRoleToUsage` en `AssignBusinessPartnerRoleHandler`/`UpdateBusinessPartnerHandler`, `CatalogController` acepta `usage=Employee`, `GET /business-partners?roles=` filtra por rol; asignar el rol exige `masterdata.businesspartners.update`. Existe `businessPartnerRegistrationFacade` (altas desde otro módulo). | Todo el backend; la fachada de registro como contrato público | **El wizard no soporta Employee** (rol, textos, `usage`, esquema): requiere extensión mínima de frontend compartido. | Autorización propia de frontend compartido en E7 (ya prevista). |
| 9 | Kernel | Los módulos se descubren por reflexión (`[Module]` en `Kernel/Modules/*Module.cs`, 12 hoy); `KernelRegistryTests` no afirma el total (solo módulos concretos); `Controller_perm_policies_exist_in_KernelRegistry_Permissions` exige que toda policy exista; los permisos se toman de las constantes de `Kernel/Permissions`. | Agregar `EmployeesPermissions` y `EmployeesModule` no rompe una cuenta fija | Un permiso solo es **asignable** si aparece en un `NavItem` o en las acciones relacionadas (`AssignablePermissionKeys`): sin `EmployeesModule` (E7) ningún perfil no administrador puede recibir `employees.*`. `INV_E2` exige alcance explícito de empresa en los requests. | Autorización de Kernel (E2 para permisos, E7 para módulo). El orden E2–E4 antes de E7 deja los permisos solo para Admin. |
| 10 | Normalizador | Solo existe `PurchaseLineNormalizer` (local de Compras); no hay normalizador compartido de nombres (confirmado). | — | Crear uno dentro de Employees (EMP-T-09). | Ninguna. |
| 11 | Pruebas de arquitectura | `RawSqlDatabaseObjectsSurviveMigrationSquashTests`, `IgnoreQueryFiltersAuditTests`, `KernelControllerPolicyTests`, `ApiControllerGuardrailTests`, `TenantIsolationInvariantTests` (INV-A1..E2: `CompanyId` sin setter público, no anulable, hereda tenant). Las clases nuevas de `ERP.Infrastructure.Tests` deben asignarse en `tools/ci/backend-test-groups.json`. | Aplican sin cambios a Employees | Ninguna propia; vigilar el manifiesto de CI. | Ninguna. |

**Compatibilidad de `IEmployeeDirectory` con SPA (punto 8 del encargo).** Las seis firmas y `EmployeeSummary` (spec §19) cubren todo lo que
el contrato SPA §6 consume: profesional responsable distinto del usuario registrador (`GetLinkedUserAsync`), elegibilidad por fecha y
sucursal, inactivos históricos con `includeInactive`, y cargo/profesión nulos (P-01). `EmployeeSummary` expone `displayName`, igual que
`BusinessPartnerDisplayInfo.DisplayName`. Dos puntos: «elegibles por especialidad» (SPA-OPS-SRV-06) exige un
filtro por `specialtyId` en `ListAsync` (decisión 8: incluido), y las citas futuras de un profesional que deja de estar
disponible (SPA-OPS-AGE-14) dependen de la política pendiente de asignaciones futuras. Las firmas siguen PROPUESTAS hasta E6b; ninguna
comprobación aquí las convierte en implementadas.

**Bloqueos técnicos reales (ninguno impide cerrar E1; condicionan fases posteriores).** (1) Edición manual de la baseline y registro de SQL
crudo, con BD existentes que se recrean (E2; sin migraciones incrementales, decisión 1). (2) Los handlers de BP no tienen transacción: el lock de E3 les cambia el flujo. (3) El
wizard de BP no admite Employee (E7). (4) `employees.*` no es asignable a perfiles hasta existir el módulo de Kernel (E7).

**Autorizaciones especiales necesarias (ninguna otorgada):** esquema/baseline (E2); Kernel: permisos (E2) y módulo (E7); MasterData, solo
las dos guardias y su transacción (E3); allowlist de `IgnoreQueryFiltersAuditTests` (**no se modificará** si `AsPlatformQuery()` cumple los controles, decisión 9);
frontend compartido para el wizard (E7); Access: ninguna.

**Decisiones aprobadas por el propietario (2026-11-24) sobre los puntos que E1 planteó** (no reabren ADR-050 ni amplían el alcance):
1. Se mantiene la baseline inicial única vigente, **sin migraciones incrementales** (punto 1).
2. Se reutiliza `CONCURRENCY_CONFLICT`; no se crea `EMPLOYEE_CONCURRENT_MODIFICATION` (punto 3).
3. Las fechas inválidas de entrada (`EMPLOYEE_INVALID_DATE`) se tratan como `Validation` (HTTP 422) (punto 3).
4. Paginación `skip/take` (punto 4).
5. `BpEmployment`: clave de bloqueo determinista que combina `TenantId` y `BusinessPartnerId`, conforme al patrón de hash existente (punto 5).
6. Vincular usuarios exige los permisos de Empleados (`employees.link-user`) y el de consulta de membresías (`access.company_user_memberships.view`) (punto 7).
7. No se pueden vincular cuentas globales (punto 7).
8. `IEmployeeDirectory.ListAsync` incluye el filtro `specialtyId`.
9. No se modifica `IgnoreQueryFiltersAuditTests` ni su allowlist si el uso de `AsPlatformQuery()` cumple los controles existentes (punto 6); la entrada prevista para E3 queda sin efecto salvo que la guardia deba llamar `IgnoreQueryFilters()` directamente.
10. Se mantiene el bloqueo preventivo de baja con asignaciones futuras (EMP-BR-23) hasta que el propietario defina el procedimiento de regularización.

Las decisiones 6, 7, 8 y 9 precisan el alcance de las fases afectadas; E2–E7 y E6b siguen **PENDING / NO AUTORIZADAS**. Las firmas de
`IEmployeeDirectory` siguen PROPUESTAS hasta E6b.

**No verificado:** comportamiento en ejecución (no se compilaron ni ejecutaron pruebas ni PostgreSQL); ubicación definitiva de carpetas
(`ERP.Domain/Modules/<X>` frente a `ERP.Domain/MasterData`; las rutas `ERP.Domain/Employees/**`, `ERP.Application/Employees/**` y `ERP.Infrastructure/Employees/**` de E2 son
provisionales: no existen y deben fijarse en la autorización de E2); el detalle de `PagedResult`; los atributos `[AppFeature]` frente al
menú de Kernel para Employees.

## EMP-E2 — Base: entidades, períodos, catálogos, auditoría y permisos

**Dependencias:** E1. **Objetivo:** modelo de datos y reglas de dominio de la spec §1–§4, §16–§17, sin endpoints de alta.
**Alcance:** `Employee`, `EmployeeEmploymentPeriod`, `EmployeeBranchAssignment` (modelo), catálogos, `EmployeeSpecialty`,
`EmployeeAudit`, `EmployeeCatalogAudit`, `EmployeesPermissions`, normalizador, `EXCLUDE` y unicidades.
**Rutas potenciales (provisionales; ubicación definitiva en la autorización de E2):** `ERP.Domain/Employees/**`, `ERP.Application/Employees/**`, `ERP.Infrastructure/Employees/**`,
`ERP.Domain/Kernel/Permissions/EmployeesPermissions.cs`, configuración EF, esquema en la baseline inicial única (sin migraciones incrementales), registro de
SQL crudo, manifiesto de pruebas de Infrastructure.
**Prohibido:** tocar MasterData, Access, auditoría o frontend; exponer endpoints de alta.
**Pruebas:** T-01, T-02 (datos), T-03 (datos), T-11, T-13 parcial. **Cierre:** pruebas en PostgreSQL real y gate del propietario.

## EMP-E3 — Protección de BusinessPartner (autorización separada: MasterData y allowlist)

**Dependencias:** E2. **Objetivo:** cumplir EMP-T-03 y E-D3. **Alcance:** puerto `IBusinessPartnerEmploymentGuard`, implementación en
Infrastructure, guardia en `DeactivateBusinessPartnerHandler` y en `RevokeBusinessPartnerRoleHandler` (solo rol Employee), código
`BP_HAS_ACTIVE_EMPLOYMENT`, lock `("BpEmployment", tenant, bp)`.
**Rutas potenciales:** `ERP.Domain/MasterData/Interfaces`, los dos handlers, `ERP.Infrastructure/Employees/**`,
`MessageCatalog`; `IgnoreQueryFiltersAuditTests` solo si la guardia no puede usar `AsPlatformQuery()` (decisión 9 de E1).
**Prohibido:** otros cambios en MasterData; alterar el alcance del BP (ADR-017).
**Pruebas:** T-05, T-06, T-13. **Cierre:** protección demostrada con PostgreSQL real y gate PASS. Hasta este cierre no se habilitan
altas reales.

## EMP-E4 — Alta y edición de la ficha (primera parte del ciclo laboral)

**Estado (2026-11-25): COMPLETADO / CLOSED (commit `acec540691`, CI #1239 SUCCESS).** Decisiones del propietario registradas para esta fase: D-1 `startDate` obligatoria y válida sin cotas; D-2 conservar el valor inactivo ya asignado y prohibir asignar uno inactivo; D-3 `PATCH` distingue omitido de `null`; D-4 filtro por sucursal diferido a E5; D-5 join de lectura del BP en Infrastructure; D-6 el alta exige solo `employees.create`; D-7 administración de catálogos incluida en E4; D-8 respuestas de referencias inválidas (BP → 404, catálogo → 422, ficha ajena → `EMPLOYEE_NOT_FOUND`, indistinguibles). Observación A resuelta con `SetAfterSaveBehavior(Throw)` sobre `TenantId` y `CompanyId` (protección de EF; sin cambio de esquema); observación B con predicados explícitos de tenant y empresa y pruebas negativas. Los catálogos exponen y exigen `rowVersion` (xmin) en renombre, activación e inactivación. T-05 completo sigue pendiente de E5. Detalle y evidencia en [STATUS](../../STATUS.md).

**Dependencias:** E3 cerrada. **Capacidades de ciclo laboral de esta fase:** alta (apertura del primer período) y edición de
cargo, profesión y especialidades. **Fuera de esta fase:** baja y reincorporación, que no se implementan ni se exponen en E4 porque
su operación completa exige el cierre atómico de asignaciones (se entregan en E5).
**Alcance:** alta, edición de cargo, profesión y especialidades, proyección del BP, endpoints y validaciones de EMP-BR-01 a
EMP-BR-07, EMP-BR-10 y EMP-BR-19.
**Pruebas:** T-04 (altas simultáneas), T-07, T-10 (permisos de alta y edición), T-12 (P-01 y P-05).
**Requisitos de aceptación heredados de la revisión de EMP-E2:** evaluar la protección adicional de la inmutabilidad de `CompanyId` a nivel EF (contrato y arquitectura vigentes); validación y pruebas negativas de referencias cruzadas entre empresas y tenants (cargo, profesión, especialidad y BP); sin acreditarlas no se habilita ninguna operación real. **Cierre:** gate PASS; el alta
real queda habilitada solo después. Hasta E5 no existe baja ni reincorporación.

## EMP-E5 — Asignaciones laborales y ciclo laboral completo (baja y reincorporación)

**Estado (2026-11-25): COMPLETADO / CLOSED (commit `6d87fe1087`, CI #1240 SUCCESS).** Decisiones registradas: D-E5-1 `rowVersion` en baja, reincorporación (ficha) y cierre de asignación (asignación), con existencia → versión → reglas; D-E5-2 `EMPLOYEE_BRANCH_INVALID` (422) indistinguible; D-E5-3 `warnings[]` vacío hasta E6 (`ACTIVE_MEMBERSHIP` obligatorio en E6); D-E5-4 filtro `branchId` + `asOf` (hoy de la empresa por defecto); D-E5-5 respuestas de cierre de asignación (404/409/422). Observación B de sucursales resuelta con predicados explícitos de tenant y empresa y `FOR SHARE` sin modificar Branches. Detalle y evidencia en [STATUS](../../STATUS.md).

**Dependencias:** E4. **Objetivo:** completar el ciclo laboral integrando períodos y asignaciones en operaciones atómicas.
**Alcance:** EMP-BR-11, EMP-BR-12; **baja** (EMP-BR-08) con cierre del período y de las asignaciones vigentes en una sola
transacción (INV-09); **reincorporación** (EMP-BR-09; no el mismo día de la baja) sin reabrir asignaciones anteriores (INV-12);
asignaciones contenidas en un período (INV-08); EMP-BR-23.
**Condición previa:** el propietario define el procedimiento de regularización o cancelación de asignaciones futuras antes de
implementar el comportamiento afectado (baja con asignaciones posteriores a su fecha); sin esa definición solo se implementa el
bloqueo preventivo PROPUESTO de EMP-BR-23 y no se inventa otro flujo.
**Pruebas:** T-02 (bordes de reincorporación a nivel de aplicación), T-03, T-04 (reincorporaciones y asignaciones simultáneas),
T-08, T-09, T-10 (permiso `employees.disable`), T-12 (P-07).
**Requisito de aceptación heredado de la revisión de EMP-E2:** validación y pruebas negativas de referencias cruzadas entre empresas y tenants (sucursal y empleado de otra empresa o tenant); sin acreditarlas no se habilita ninguna operación real. **Cierre:** gate PASS con las operaciones de baja y reincorporación
verificadas como atómicas en PostgreSQL real. **Prohibido:** tocar `CompanyUserBranch` o permisos.

## EMP-E6 — Vinculación y desvinculación de usuarios

**Estado (2026-11-25): COMPLETADO / CLOSED (commit `877d99d04f`, CI #1241 SUCCESS).** Decisiones registradas: D-E6-1 vincular y desvincular en fichas activas y en baja; D-E6-2 no se reemplaza un vínculo existente (409); D-E6-3 `rowVersion` en vincular y desvincular, **complemento contractual** que resuelve la contradicción con «`unlink-user`: sin cuerpo» de la especificación §9; D-E6-4 usuario inactivo con membresía activa admitido; D-E6-5 cuenta posteriormente global oculta, sin advertencia y desvinculable. Observación B de usuarios resuelta con la regla única de IAM y predicados de tenant y empresa; `ACTIVE_MEMBERSHIP` completado en la baja. Detalle y evidencia en [STATUS](../../STATUS.md).

**Dependencias:** E4 y E1. **Alcance:** EMP-BR-13 y EMP-BR-14, `POST /employees/{id}/unlink-user`. Acceso a Identity/Access solo
mediante puertos de lectura; si hiciera falta tocar Access, autorización separada. **Pruebas:** T-08, T-10.

## EMP-E6b — Contrato `IEmployeeDirectory` para Spa

**Estado (2026-11-25): COMPLETADO / CLOSED (commit `b78edc11a9350f3a148a03c649ccd69e621b4609`, [CI #1242 SUCCESS](https://github.com/szhinin89/erp-saas/actions/runs/45663558911), segundo intento).** Directorio PostgreSQL 13/13, resolución real por DI, T-15 acreditado, Architecture 158/158 y SSOT creada e indexada. El primer intento del CI falló por la incidencia histórica de Compras `Test8_D_polling_y_verificacion_en_linea_simultaneos_con_ANULADO_anulan_la_compra_una_sola_vez` (`OriginFinalized`: esperado 1, obtenido 0); la reejecución fue satisfactoria y la causa raíz permanece NO CONFIRMADA. Decisiones registradas: D-E6b-1 seis firmas definitivas, modelos mínimos, filtro solo `specialtyId` y lista sin paginación; D-E6b-2 EMP-BR-12 literal; D-E6b-3 contrato interno sin permisos propios y con aislamiento obligatorio. SSOT: [employees.md](../architecture/employees.md). Detalle y evidencia en [STATUS](../../STATUS.md).

**Dependencias:** E5 y E6. **Alcance:** interfaz y servicio de lectura (spec §19) con referencias históricas estables.
**Pruebas:** T-15. **Condición:** debe cerrarse antes de cualquier integración de Spa. No incluye lógica clínica ni de Spa.

## EMP-E7 — Frontend, Kernel y launcher

**Estado (2026-11-25): IMPLEMENTADO / GATE APROBADO / ACEPTACIÓN DEL PROPIETARIO / PENDIENTE CI.** Decisiones aprobadas D-1 (wizard de MasterData con rol Employee, incluidos sus dos componentes internos), D-2 (`AppFeature`) y D-3 (etiquetas de permisos); línea `/api/employees` autorizada en la guarda de plataforma. Aceptación visual y funcional del propietario aprobada, con las correcciones del wizard en contexto Employee. No se declara CLOSED hasta CI satisfactorio. Cobertura T-01 a T-15, resultados y limitaciones en [STATUS](../../STATUS.md); comportamiento en [employees.md](../architecture/employees.md).

**Dependencias:** E4–E6b. **Alcance:** `EmployeesModule.cs`, claves i18n, `frontend/src/modules/employees/**`,
`MODULE_ICON_BY_ID`, pantallas, ficha de tres secciones, catálogos laborales, guard de cambios sin guardar. Si el wizard de BP no
admite Employee: extensión mínima aparte con autorización propia. **Auditoría de reutilización de UI** declarada antes de escribir.
**Pruebas:** T-13, T-14; lint, `npx tsc -b` y build. **Cierre:** aceptación del propietario; entonces `MENU_MAP.md` y, con código
comprobado, `employees.md`, `FEATURES.md` y `docs/architecture/README.md`.

## Condiciones de cierre del módulo

Todas las fases cerradas (con la baja y la reincorporación habilitadas solo desde E5); T-01 a T-15 verdes; documentación sincronizada con el código; ningún componente FROZEN modificado fuera
de las autorizaciones separadas; sin duplicación de datos del BP.
