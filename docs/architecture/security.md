# Seguridad — auth, tokens y multi-tenant

Canónico para identidad y aislamiento. Detalle descriptivo: `docs/IDENTITY.md`, `docs/security/auth/refresh-rotation.md`. Reglas PR: [PR-RULES-CATALOG.md](./pr-rules-catalog.md).

---

## Multi-tenant (innegociable)

- `TenantId` desde **JWT/contexto**, nunca como autoridad desde body/query en operaciones tenant-scoped.
- Filtros globales EF por tenant en lecturas.
- Índices únicos con `TenantId` cuando la unicidad es por empresa.
- Cross-tenant solo en flujos plataforma / operador platform autorizados.
- Frontend: no enviar `tenantId` en formularios tenant-scoped.
- URLs: no UUID tenant en query — ver [SaaS — IDs sensibles fuera de la URL](#saas--ids-sensibles-fuera-de-la-url) más abajo.

### Aislamiento entre empresas del mismo tenant

Una empresa no consulta, modifica, usa ni afecta datos operativos de otra, aunque compartan tenant. Solo se comparten los datos TENANT (p. ej. BusinessPartner, `ITenantScopedRequest`) y GLOBAL/plataforma expresamente definidos.

- La empresa activa llega en `X-Company-Id` (`ICurrentCompany`) y **no es autoridad por sí sola**: la valida `CompanyScopeBehavior` vía `ICompanyAccessGuard` (empresa del tenant, operativa y con membership activa del usuario, o operador autorizado) para todo request `ICompanyScopedRequest` / `IRequiresCompanyContext` / `IBranchScopedRequest`. Rechazo: 403 `COMPANY_SCOPE_FORBIDDEN`. Un `ExplicitCompanyId` del cliente debe coincidir con la empresa activa.
- Los filtros globales EF por empresa (`ICompanyOperationalEntity`, `ICompanyScopedEntity`) usan esa misma empresa activa, fail-closed sin contexto; no reemplazan la validación de membership.
- La autorización `perm:*` (`RuntimePermissionAuthorizer`, vía `CompanyContextProvider`) evalúa la membership y el perfil del usuario **en la empresa operativa** (SEC-04):
  - Con `X-Company-Id` presente, solo vale una membership activa del usuario en esa empresa y del tenant del JWT; si no existe, el request se rechaza con 403 `COMPANY_SCOPE_FORBIDDEN` (mismo contrato que `CompanyScopeBehavior`), **sin fallback** a otra empresa del usuario. Sin header, la empresa operativa es la única membership activa del usuario en el tenant; con varias, no hay empresa operativa y se niega.
  - El bypass Admin (`*`) corresponde solo al rol `Admin` **de la membership de la empresa operativa**: ser Admin en A no concede privilegios en B. El rol del JWT (emitido para la empresa de apertura de sesión) nunca es autoridad de permisos. La única excepción transversal es el administrador global en modo operador, validado por `IOperatorCompanyAccessPolicy` (`operator_mode` + `GlobalUserRole` Admin activa).
  - `GetMyPermissions`, el contexto de sesión (roles/permisos), la navegación y la regla anti-escalamiento de `UpsertProfilePermissions` aplican la misma regla (`OperationalCompanyContext.HasAdminBypass`).
  - `perm:*` no reemplaza la validación de scope: **toda operación COMPANY expuesta por la API declara su marcador** o, como única alternativa existente, valida con el guard explícito en el handler (`RequireCurrentCompanyAsync` / `RequireMembershipAsync`, p. ej. perfil de empresa). Excepción conocida: `CreateCompanyUserPreferences`, consumidor interno sin endpoint propio (riesgo registrado en [STATUS](../../STATUS.md#riesgos-y-limitaciones-vigentes)).
  - Una escritura sobre una empresa pedida por id (`RequireMembershipAsync`), que puede no ser la del `X-Company-Id`, exige además el permiso de la operación en esa empresa (Admin de su membership, operador autorizado o perfil con el permiso): `perm:*` solo lo evaluó en la empresa operativa. Aplica a `UpdateCompany` (`PUT /companies/{id}`).
- **Perfiles de acceso por empresa (SEC-05).** `AccessProfile` es una entidad operativa de empresa (`TenantId + CompanyId`, `ICompanyOperationalEntity`, filtro EF fail-closed). Cada empresa tiene perfiles propios: dos empresas pueden usar el mismo nombre (único por `tenant_id + company_id + name`) sin compartir identidad ni permisos.
  - Consultar, crear, editar, desactivar y asignar permisos a un perfil exige empresa activa validada (`IRequiresCompanyContext`) y resuelve el perfil con tenant + empresa activa; un perfil de otra empresa no existe para la operación. Los cambios solo invalidan cachés de permisos y navegación de esa empresa.
  - Una membership solo recibe perfiles de su misma empresa: lo valida `UpsertCompanyUserMembership` (perfil existente, activo y de la empresa) y lo garantiza en base de datos la FK compuesta `company_user_memberships (company_id, profile_id) → access_profiles (company_id, id)`.
  - Un perfil desactivado no concede permisos efectivos: la resolución runtime (`IEffectivePermissionKeysProvider`) solo carga permisos de un perfil activo de la empresa de la membership. La revocación frente a la caché de permisos sigue el contrato SEC-06 (siguiente punto).
- **Revocación de permisos con caché (SEC-06).** Una revocación confirmada no sigue autorizando por una entrada obsoleta de la caché de permisos efectivos (`IEffectivePermissionKeysProvider` → `IPermissionsCacheService`, TTL 300 s); un fallo de infraestructura nunca se convierte en concesión.
  - Membership revocada o inactiva y administrador global en modo operador no dependen de la caché: `RuntimePermissionAuthorizer` resuelve membership y operador desde BD en cada request (SEC-04) antes de consultar permisos.
  - Una entrada solo es válida si coincide con el perfil vigente de la membership y con las versiones actuales de tenant, empresa y usuario, sin invalidación en curso. Se publica con las versiones observadas **antes** de cargar la BD (nunca con las vigentes al escribir) y solo si ese sello tiene menos de 60 s: una carga iniciada antes de una invalidación nace obsoleta.
  - Toda mutación que cambia permisos efectivos (permisos o estado de un perfil → alcance empresa; asignación, rol, perfil o revocación de membership → alcance usuario) persiste dentro del protocolo de `IPermissionsCacheInvalidator`, vía `PermissionsCacheInvalidation.RunAsync`: (1) marcar el alcance como "invalidación en curso" **antes** de persistir — si no se puede, no se persiste nada y se responde 503 `PERMISSIONS_INVALIDATION_UNAVAILABLE`; (2) persistir; (3) incrementar la versión y retirar la marca. Si (3) falla, la marca permanece: mientras exista, ninguna instancia usa ni publica entradas de ese alcance (resuelve desde BD), y se responde 503 `PERMISSIONS_INVALIDATION_INCOMPLETE` (cambio guardado, propagación en modo protegido). La marca expira a los 900 s, cuando ya no puede quedar ninguna entrada publicada con datos anteriores (60 s de sello + 300 s de TTL).
  - Versiones y marcas son atómicas: con Redis, scripts Lua sobre el `IConnectionMultiplexer` único del host (claves `{erp:permissions}:*`, sin expiración las versiones; una versión perdida se re-inicializa por encima de la anterior); sin Redis, contadores del proceso.
  - Fallo de lectura de caché → se resuelve desde BD sin publicar; fallo de escritura → se ignora (no publicar nunca concede); fallo de invalidación → `false` visible (log Error + métrica) con el efecto anterior.
  - **Despliegue:** con más de una instancia de API, Redis es obligatorio y el despliegue declara `Redis:Required=true` (el host no arranca sin conexión Redis); la caché en memoria solo se admite en un despliegue de instancia única. La cantidad real de instancias es configuración del entorno, fuera del repositorio.
  - **Contrato del Redis de autorización (opción A).** PostgreSQL es la autoridad; Redis nunca recupera un estado anterior ni expulsa marcas o versiones: `maxmemory-policy noeviction`, `save ""` (sin RDB), `appendonly no` (sin AOF), nodo primario sin réplicas conectadas (sin réplica asíncrona que pueda promoverse atrasada). Todo Redis configurado es el de autorización.
    - **Impide el arranque** (API y worker, `PermissionsCacheRedisStartupValidator` → `PermissionsCacheRedisGuarantee`, conexión efímera en modo admin con `CONFIG GET` + `INFO replication`): cualquier propiedad incompatible, Redis inaccesible, o `CONFIG`/`INFO` no disponibles (no verificable = no conforme). Un Redis gestionado que bloquee `CONFIG` no es compatible tal cual: requiere una verificación equivalente aprobada por el propietario.
    - **En ejecución**: Redis caído → lecturas desde BD, invalidación que no puede iniciarse → 503 sin persistir; memoria llena con `noeviction` → las escrituras fallan (mismo efecto fail-closed), nunca se expulsa una marca. Un reinicio sin persistencia vacía Redis: todo se lee de BD.
    - **Solo operativo** (configuración y supervisión, fuera del alcance del arranque): que nadie cambie la configuración después de arrancar (`CONFIG SET`), que no se restaure un respaldo de Redis ni se promueva una réplica, monitoreo de memoria (`used_memory` frente a `maxmemory`) y alertas de disponibilidad de Redis.
  - `UpsertCompanyUserMembership` valida todo (incluidas las sucursales) antes de persistir y persiste membership, sucursales y preferencias en una transacción (la ambiente si existe, p. ej. alta de usuario): un fallo de preferencias revierte el cambio de rol/perfil.
  - Anti-escalamiento en memberships (mismo bypass `OperationalCompanyContext.HasAdminBypass` y mismo criterio de alcance que `UpsertProfilePermissions`): solo un Admin de la empresa o el administrador global en modo operador asigna, modifica o retira el rol Admin (incluida la revocación de una membership Admin); un usuario no Admin con permisos delegados no modifica su propia membership ni asigna un perfil con permisos que él no tiene efectivos en esa empresa.
- **Identidades globales y alcance IAM empresarial (IAM-V1-F1).** Única regla en `IamIdentityScope` (Application/Access); identidad global = `GlobalUserRole` activo, nunca por username, email o perfil. Las identidades no guardan tenant: su pertenencia sale de sus memberships.
  - Desde cualquier sesión de empresa (incluido el modo operador) una cuenta global es invisible y no administrable: listado de usuarios, búsqueda por username, vinculación, cambio de rol/perfil, revocación, contraseña temporal, sucursales, preferencias, sesiones (listado, estadísticas, cierre) y la matriz/asignación de Seguridad administrativa. Su membership (p. ej. Admin de la primera empresa de First Run) y su autorización operativa no cambian; se administra solo desde Admin Core.
  - Administrable = membership en la empresa operativa y no global. Vinculable (agregar un usuario existente) = alguna membership en una empresa del mismo tenant y no global; una identidad de otro tenant nunca es visible ni vinculable.
  - Fuera de alcance responde exactamente igual que «no existe» en cada endpoint: lookup `identityUserExists=false`; vincular, revocar y contraseña temporal 404 «Usuario no encontrado.»; sucursales/preferencias y cierre de sesión su 404 habitual. Crear usuario con username o email ocupado (únicos en todo el sistema) responde 409 con un mensaje genérico único; ese conflicto es el único indicio residual de existencia.
  - Sesiones administrativas: siempre la empresa operativa; un `companyId` de la query no amplía el alcance.
  - Actividad por entidad (`GET /admin/activity/entity`, `IRequiresCompanyContext`): `UserActivity` solo guarda `TenantId`, así que la entidad consultada debe pertenecer a la empresa operativa, verificada con los repositorios de alcance de empresa existentes: `Branch`/`Warehouse` por `GetByIdForCompanyAsync`; `BusinessPartner` es dato TENANT compartido; `IdentityUser`/`CompanyUserMembership` deben ser administrables (nunca una cuenta global); cualquier otro tipo es fail-closed. Fuera de alcance responde lista vacía, igual que una entidad sin actividad. Los registros hechos por una cuenta global se muestran con actor neutro «Administración del sistema». El registro de auditoría se conserva íntegro en base.
  - `CompanyAdministratorInvariant`: el administrador global no cuenta como administrador propio. Una empresa administrada solo por el global puede recibir su primer Admin; desde entonces no se puede retirar al último Admin no global activo (además de la regla base de no quedar sin ningún Admin).
  - **Administrador inicial de empresa y credenciales temporales (IAM-V1-F3).** Al crear una empresa desde Admin Core (`POST /api/v1/companies`, política `CompanyProvisioning`) se puede registrar un administrador inicial (usuario, nombres, apellidos, correo; sin contraseña): empresa, bootstrap, `IdentityUser` con `RequirePasswordReset`, membresía `Admin` sin perfil y acceso a la sucursal principal se confirman en una sola transacción (`CompanyProvisioningService.CreateManagedCompanyAsync`). La contraseña temporal la genera el backend (`TemporaryPasswordGenerator`, CSPRNG); solo su hash se persiste y el texto plano existe únicamente en la respuesta de creación (`Cache-Control: no-store`), nunca en consultas posteriores, almacenamiento del navegador, logs ni auditoría. El primer ingreso reutiliza el flujo `RequiresPasswordReset` / `complete-password-reset`. Usuario y correo duplicados responden 409 genérico (comprobación previa, dentro de la transacción y violación de índice único ante concurrencia). Sin administrador inicial el contrato de integración/E2E no cambia. Las altas de empresa de un mismo tenant se serializan con el lock de aprovisionamiento por tenant (`pg_advisory_xact_lock`, el mismo de `EnsureDefaultCompanyAsync`) para que no se interbloqueen al sembrar datos de nivel tenant. Recuperación: `GET/POST /api/v1/admin-core/companies/{id}/administrators[...]/temporary-password` (política `PlatformAdmin`, solo administradores no globales con membresía `Admin` activa de esa empresa; cualquier otro caso es 404 uniforme): nueva contraseña, `RequirePasswordReset`, revocación de refresh tokens y sesiones (`IUserAccessRevocationService`) y auditoría (`UserActivity` `company.admin.password-regenerate`, sin contraseña ni hash) en una sola transacción; se muestra una sola vez.
  - Pruebas: `IamIdentityIsolationHttpTests` (PostgreSQL: dos tenants, varias empresas, global, Admin empresarial, operativos) y `AdminUserSessionIntegrationTests`.
- **Administración de usuarios y perfiles (IAM-V1-F2).**
  - «Administrador de empresa» es la presentación de `Role = Admin` (acceso total por su función, `HasAdminBypass`); «Usuario operativo» es `Role = User` (acceso según su perfil). Única presentación: `modules/access/roleLabels.ts`. No existe un perfil «Administrador de empresa» ni otra fuente de privilegios administrativos; un perfil con todos los permisos asignado a un `User` no equivale a la función (no puede asignar ni retirar el rol Admin).
  - Toda asignación operativa nueva exige perfil — alta, vinculación, reactivación o paso de Administrador de empresa a operativo (`UpsertCompanyUserMembershipHandler.OperationalProfileRequiredMessage`, 422). Un operativo activo existente sin perfil se conserva y puede editarse; la UI lo señala «sin acceso».
  - Contraseña temporal: `POST /admin/iam/users/{username}/assign-temporary-password` exige solo `access.identity_users.assign_temporary_password` (la clase `IdentityUsersController` exige únicamente sesión; `access.identity_users.create` es exclusivo de `POST /admin/iam/users`).
  - Asignación de permisos: marcar todo el catálogo pide confirmación y guardar un perfil con más de la mitad de los permisos muestra un aviso explícito, además de las diferencias respecto de lo guardado. El resumen «Este perfil concede» (pantallas y acciones por módulo) se deriva del mismo catálogo del Kernel.
  - Pruebas: `IamIdentityIsolationHttpTests` (contraseña temporal sin permiso de crear; alta y vinculación sin perfil), `UpsertCompanyUserMembershipHandlerTests`, `GetPermissionCatalogHandlerTests` y las pruebas frontend de `modules/access`.
- `ITenantScopedRequest` valida solo el tenant: no se usa para leer ni escribir datos de una empresa. `UpdateCustomerRoleConfigValidator` comprueba códigos en catálogos de la empresa del contexto sin revelar los de otra: el permiso del endpoint (SEC-04) rechaza un `X-Company-Id` sin membership activa antes del validator, y sin header la consulta no devuelve filas.
- **Catálogo de sucursales y bodegas (BRANCH-ADMIN-CATALOG-ACCESS-REVIEW-01).** Listar y editar sucursales (`settings.branches.*`) y listar, crear y editar bodegas (`inventory.warehouses.view/create/update`) es administración **company-wide**: lo autoriza el permiso en la empresa operativa (Admin de la membership, operador autorizado o perfil con el permiso), no la asignación de sucursales del usuario (`CompanyUserBranch`). La sucursal destino de una bodega debe ser de la empresa activa (422 si no). Habilitar y deshabilitar una bodega son branch-scoped: solo sobre bodegas de la sucursal activa (404 en otra). Una sucursal o bodega de otra empresa no existe para la operación. La configuración por sucursal (bodega por defecto) sigue exigiendo la sucursal de la ruta vía `IBranchAccessGuard`. Pruebas: `BranchWarehouseCatalogAccessHttpTests`.
- Guardrail: `INV_E2` (`TenantIsolationInvariantTests`) exige marcador explícito, de cualquier scope, en los namespaces COMPANY que enumera; no cubre todos los módulos ni distingue un marcador TENANT puesto por error.

### Prohibiciones backend (resumen)

- `IgnoreQueryFilters()` sin `PlatformQueryReason`
- `company_id` del body como autoridad de tenant
- Rutas con `tenantId` para operaciones del tenant autenticado

---

## Tokens

| Token | Almacenamiento | Rotación |
|-------|----------------|----------|
| Access JWT | Memoria SPA (+ espejo Zustand no persistido) | Corta (config JWT) |
| Refresh opaco | Cookie **httpOnly** `Path=/api` | Cada uso (family chain) |

### Prohibido

- Refresh token en `localStorage` / `sessionStorage`
- Revocar todos los tokens del usuario en replay de una sola familia

---

## Refresh rotation

- **FamilyId** por sesión/dispositivo
- Reuso benigno (< grace) → 401 sin revocar familia
- Reuso sospechoso → `RevokeFamilyAsync` (no logout global)
- Multi-tab: Web Locks `erp-refresh` + BroadcastChannel `erp.auth`
- Frontend: solo `authRefreshManager`
- El interceptor HTTP consulta `frontend/src/modules/lib/authRefreshPolicy.ts`: las rutas
  públicas, incluidas `global-login` y `complete-password-reset`, propagan su 401 sin
  refresh automático, reintento ni limpieza de sesión. Un 429 directo tampoco inicia
  refresh ni logout. Los 401 de solicitudes protegidas conservan el refresh y un único reintento.

---

## Login

- Login unificado tenant vía `/api/auth/login` (incluye operador platform cuando aplica).
- Logout: `fullLogout()` — stores, sessionStorage, BC `logout`, cookie vía API.

Detalle endpoints y claims: `docs/IDENTITY.md`.

### Límites de intentos de login (SEC-A01 — IMPLEMENTACIÓN V1 CLOSED / VERIFICACIONES DE DESPLIEGUE PENDIENTES)

`POST /api/v1/auth/login` y `POST /api/v1/auth/global-login` pasan por `ILoginAttemptThrottle`
(`LoginAttemptThrottle`) en el controller **antes** del handler; handlers, BCrypt, JWT, refresh y
sesiones no cambian. Ambas rutas comparten contadores (prefijo `erp:auth:login:`, claves con
SHA-256 del usuario normalizado y de la IP, nunca en claro) y configuración (`LoginThrottle`).

- **Contadores** (ventana fija alineada de `WindowMinutes`, el número de ventana forma parte de la
  clave): K1 cuenta + IP (`AccountIpLimit`, siempre activo), K2 IP (`IpLimit`, 0 = desactivado),
  K3 cuenta (`AccountLimit`, 0 = desactivado).
- **Admisión atómica**: `DistributedFixedWindowRateLimit.TryReserveAllAsync` comprueba todos los
  contadores y solo entonces reserva uno en cada uno (Lua con Redis; lock único en el respaldo en
  memoria). Un rechazo no modifica ni crea ningún contador.
- **Concurrencia**: `ConcurrencyLimiter` único por instancia (`MaxConcurrentVerifications`,
  `QueueLimit`, `MaxQueueWaitMilliseconds`), pedido después de la reserva; sin permiso se devuelve la
  reserva.
- **Liberación**: solo un login exitoso devuelve la reserva (`ReleaseAllAsync`, nunca negativo; una
  liberación tardía no toca la ventana siguiente). Credenciales inválidas, otros fallos, excepciones
  o cancelación durante el handler la conservan hasta que vence la ventana. No hay bloqueo
  permanente.
- **Respuestas**: límite excedido o sin capacidad → 429 `RATE_LIMITED`; Redis configurado pero
  inaccesible → 503 `LOGIN_THROTTLE_UNAVAILABLE` sin verificar la contraseña. Sin Redis
  configurado, el respaldo en memoria solo se usa en Development/Testing; en otro entorno → 503.
- **IP del cliente**: `Connection.RemoteIpAddress`. `X-Forwarded-For` solo se procesa desde
  `ForwardedHeaders:KnownProxies`/`KnownNetworks` (vacío por defecto = no se procesa; sin loopback
  implícito; `ForwardLimit = 1`).
- **Riesgo residual aceptado**: sin cookie de dispositivo, un atacante que conoce un usuario puede
  bloquearlo temporalmente desde cualquier origen al llenar K3, y desde la misma IP al llenar K1.

**Gates pendientes antes de producción**: valores definitivos de límites, concurrencia y cola
(`t_bcrypt` medido en el hardware real); IP real del cliente verificada detrás del proxy de
despliegue y `KnownProxies`/`KnownNetworks` reales antes de activar K2; Redis disponible y
`Redis:Required` acorde al número de instancias; HTTPS/`X-Forwarded-Proto` fuera de este alcance.

---

## Roles — fuente única (`SecurityRoles` / `isAdminRole`)

| Capa | Fuente única | Uso |
|------|---------------|-----|
| Backend | `ERP.Domain.Kernel.Security.SecurityRoles` (`Admin`, `User`) | Claim `ClaimTypes.Role` del JWT. Bypass de permisos (`*`) cuando `role == SecurityRoles.Admin` |
| Frontend | `frontend/src/access/permissionUi.ts` → `ADMIN_ROLES` / `isAdminRole(role)` | Único helper para gates de página por rol Admin |

**Prohibido**: comparaciones ad-hoc (`role === 'Admin'`, `role.toLowerCase() === 'admin'`, literal `"Admin"`/`"User"` fuera de `SecurityRoles`) en backend o frontend. Cualquier check de rol Admin pasa por `SecurityRoles.Admin` (backend) o `isAdminRole()` (frontend).

Excepción documentada: `Platform.Contracts/Integration/Dtos/CompanyProvisionRequest.cs` (`CreatorRole = "Admin"`) — DTO de integración cross-boundary sin referencia a `ERP.Domain`, fuera del alcance de `SecurityRoles` (ver `ERP_CORE_FREEZE.md`).

No confundir con perfiles de acceso asignables (ej. `"Facturador"` y `"Contador"`, perfiles iniciales de cada empresa NUEVA que el Admin de la empresa puede editar; ni el bootstrap ni la auto-sanación los restauran después), que pertenecen a cada empresa (SEC-05, ver [Aislamiento entre empresas del mismo tenant](#aislamiento-entre-empresas-del-mismo-tenant)) y no forman parte de `SecurityRoles`.

---

## Permisos vs. política de flujo documental (DOCUMENT-FLOW-POLICY-01)

Distinción conceptual obligatoria, nunca debe mezclarse:

- **Los permisos** (`ExpensePermissions`, `SettingsPermissions`, etc., `[Authorize(Policy = "perm:...")]`) definen **QUIÉN** puede ejecutar una acción. Se conceden por rol de la membership o por el perfil de acceso de cada empresa (SEC-05); el catálogo de permisos (`KernelRegistry`) es el mismo para todas las companies.
- **`DocumentFlowPolicy`** (tabla `document_flow_policy`, `IDocumentFlowPolicyService`) define **CÓMO** se comporta cada tipo de documento (`DocType`) por company: modo de creación/confirmación/autorización/anulación, y qué efectos dispara (CxP, asiento, inventario, notificaciones).
- **Los estados del documento** (`ExpenseStatus`, etc.) definen **EN QUÉ ETAPA** está un documento concreto.
- **Los eventos de dominio** (`ExpenseDocumentConfirmedEvent`, etc.) definen **QUÉ EFECTOS** genera un documento al avanzar de etapa.

**Regla dura**: `DocumentFlowPolicy` nunca se usa como permiso — no tiene (ni debe tener) campos `Can*`/`Allow*`. Un handler que consulta `IDocumentFlowPolicyService` para decidir el comportamiento del flujo **no reemplaza** la validación de permiso de la acción (`[Authorize(Policy = "perm:...")]` en el controller) — ambos gates son independientes y ambos deben pasar. Ver `CancelExpenseDocumentHandler`/`ConfirmExpenseDocumentHandler` (Expenses) como referencia de integración: el permiso se valida en el controller, la política se valida dentro del handler.

**`AuthorizationMode`** (`None`/`SingleStep`/`MultiStep`) se guarda por tipo de documento y la pantalla permite elegir cualquiera. Solo la confirmación de Gastos lo evalúa (`EnsureConfirmationFlowAsync`): un valor distinto de `None` rechaza la confirmación con `document_flow_policy.authorization_required`. No existen estados, comandos ni pantallas de aprobación o rechazo. Riesgo vigente: [STATUS](../../STATUS.md#riesgos-y-limitaciones-vigentes).

Pantalla de administración: Configuración → Documentos y flujos (`/settings/document-flows`, permisos `settings.documentFlows.view`/`.update`) — administra únicamente el acceso a esa pantalla de configuración, nunca los permisos de acción de cada módulo.

---

## Security & Access Contract V1 — LOCKED

Modelo de autorización UI vigente, congelado para esta pasada (Security & Access Governance V1). Cambios a la lista ❌ requieren discusión explícita + actualización de esta sección.

**Modelo actual:**
- **Menú/navegación**: 100% server-driven vía `GET /api/v1/me/menu` (`NavigationBuilder`, filtra por rol + `IEffectivePermissionKeysProvider`; Admin ve todo sin filtro).
- **Gates de página por rol**: único mecanismo permitido es `isAdminRole(user?.role)` (ej. `SecuritySettingsPage`, `ConfigContext` para lectura de config de tenant).
- **`usePermissionsUi().canShow` / `.has`**: visibilidad de UI según `SessionContext.authorization.permissions` (Admin y `*` = todo). **Fail-closed** (IAM-V1-F2): sin sesión cargada devuelve `false` y con la lista vacía solo concede lo que contenga. No es autorización: la API decide con `perm:*`.
- **`RouteAccessGuard`** (IAM-V1-F2): no monta ninguna página hasta resolver el menú de la sesión actual (ligado a usuario + tenant + empresa) y los permisos; si la sesión no pudo cargarse, un usuario no Admin no ve pantallas. Una URL se permite si es una pantalla del menú del usuario, su detalle/alta/edición (`/:param`, `/new`, `/:param/edit`), una hija declarada en `INHERITED_CHILD_ROUTES` (solo las que comparten exactamente el permiso del padre), un redirect registrado o `/dashboard`. Una hija con permiso propio (p. ej. `/suppliers/credits`) nunca hereda: necesita su ítem de menú. Los patrones salen de los mismos arreglos de rutas de `App.tsx` (`routes/protectedRoutePaths.ts`); una prueba de inventario exige que toda ruta registrada sea alcanzable con el menú completo.
- **Deny-by-default**: sin sesión / rol no-Admin en páginas con gate → `NoAccessPage` (`common.noAccess`). No hay acceso implícito.

✅ **Permitido** (no requiere revisión arquitectónica):
- Agregar nuevas páginas con gate `isAdminRole(user?.role)` siguiendo el patrón de `SecuritySettingsPage`.
- Agregar nuevos permission keys al catálogo backend (`ERP.Domain/Kernel/Permissions/*.cs`) y al menú server-driven (`UiNavItems.PermissionKey`/`PermissionKeysAnyJson`).
- Agregar tests de acceso (admin / no-admin / sin sesión) siguiendo `SecuritySettingsPage.test.tsx`.

❌ **Restringido** (requiere revisión arquitectónica explícita):
- Cambiar la semántica fail-closed de `canShow`/`has` o las reglas de herencia de `RouteAccessGuard` sin actualizar esta sección + tests de regresión.
- Crear una segunda fuente de verdad para "es Admin" (nuevo helper, nueva constante, nueva comparación de string).
- Bypasses temporales de autorización (flags, `if (true)`, roles hardcodeados fuera de `SecurityRoles`/`ADMIN_ROLES`).
- Mover el filtrado de menú del backend al frontend.

---

## Guardrails CI

`tools/architecture/check-identity-guardrails.ps1` — ver [ENFORCEMENT.md](./enforcement.md).

---

## SaaS — IDs sensibles fuera de la URL

Producto multi-tenant. Detalle billing/planes: [`docs/archive/SAAS-COMMERCIAL.md`](../archive/SAAS-COMMERCIAL.md) (histórico — ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)).

**Regla de producto:** no colocar UUID de tenant, company ni tokens en `location.search` ni en enlaces compartibles.

### Implementación de referencia

`frontend/src/lib/session/sessionStorageKeys.ts`:

- Prefijo de claves de contexto de navegación: **`erp.saas.`** (`SAAS_SESSION_STORAGE_PREFIX`).
- `fullLogout` limpia todas las claves `erp.saas.*` al cerrar sesión.

### Al extender el producto

- Nueva clave con prefijo **`erp.saas.`** o store existente.
- **No** `?tenantId=`, `?companyId=`, `?data=<uuid>`.
- Migrar URLs legacy: persistir en sessionStorage, quitar query con `replace`.

Cursor hint operativo: `.cursor/rules/saas-navigation-no-sensitive-url.mdc`.

Tarifas SRI: ver [backend.md#tarifas-sri](./backend.md).

### Items: alcance Tenant + Company (A1)

Item exige empresa del contexto autenticado y no tiene BranchId. Sin empresa válida, root y colecciones hijas no son visibles. Variantes, atributos, barcodes, imágenes, conversiones, sustitutos, packaging, códigos de proveedor, impuestos especiales y auditoría heredan aislamiento del Item. Sustitutos exigen ambos Items dentro del scope. Pricing y las operaciones resuelven el mismo Item de la empresa actual. BusinessPartner permanece tenant-wide. Constraints PostgreSQL respaldan las unicidades por empresa. Véase [ADR-040](../decisions/ADR-040-item-company-scope-inventory-nature.md).

## First Run

Instalación inicial (token de un solo uso, propietario exclusivo por base, alta inicial, launcher y reset local): SSOT en [first-run.md](./first-run.md).
