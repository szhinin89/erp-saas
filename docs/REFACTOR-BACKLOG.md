# Backlog de refactor (calidad / arquitectura)

Checklist para ir cerrando en PRs. Marca con `[x]` lo completado. Última revisión de lista: generada desde el estado del repo (commands sin validador, transacciones, CQRS, tenant, auditoría).

---

## P0 — Multi-tenant y seguridad (revisión) — **cerrado**

- [x] Inventario de usos de `IgnoreQueryFilters()` y comprobación explícita de `TenantId` donde aplique  
  - [x] `ERP.Infrastructure/Persistence/Repositories/UserRepository.cs` — `GetById`/`GetByEmail`/`Exists` con IQF + `TenantId` en predicado (registro/login sin tenant ambiente); IQF documentado en el resto de métodos.  
  - [x] `ERP.Infrastructure/Persistence/Repositories/AccessRepository.cs` — IQF solo donde el predicado fija `TenantId` o `IdentityUserId` (bootstrap / cupos); comentarios de clase.  
  - [x] `ERP.Infrastructure/Services/ConfigService.cs` — IQF + `TenantId` en todas las consultas; comentario de clase.  
  - [x] `ERP.Infrastructure/Persistence/SaasPlansAdminService.cs` — IQF en borrado global por `FeatureId` (todos los tenants); comentario inline.  
  - [x] `ERP.Infrastructure/Persistence/GrowthAnalyticsReader.cs` — IQF en memberships para agregados de plataforma; comentario.  
  - [x] `ERP.Infrastructure/BackgroundServices/KardexReporteProcessor.cs` — IQF por job sin HTTP; tenant aplicado vía `ManualCurrentTenant` tras cargar fila; comentario.  
  - [x] Otros hallazgos por `rg IgnoreQueryFilters` — sin usos adicionales fuera de la lista anterior.
- [x] Auditar controllers: `[Authorize]` vs política `perm:...` — **criterio unificado (aplicado / documentado)**  
  - **DefaultPolicy = `Session`** (`Program.cs`): JWT de sesión con `tenant_id` real; cualquier `[Authorize]` sin `Policy` hereda esto (no usar `FallbackPolicy` para no romper endpoints públicos).  
  - **`perm:recurso.acción`**: pantallas de negocio ERP; `PermissionHandler` valida plan + perfil; **Admin** y **SuperAdmin** (en tenant) pasan sin comprobar filas de perfil; SuperAdmin con `tenant_id = Guid.Empty` en claim pasa cualquier `perm:` (operador de plataforma).  
  - **`GlobalSuperAdmin`**: panel SaaS / planes / config global (`SuperAdminController`, `Saas*Admin`, `SuperAdminConfig`).  
  - **`Bootstrap`**: solo `switch-tenant` tras login IAM (token corto).  
  - **`[Authorize(Roles = "...")]`**: IAM donde aún no hay claves `perm:` (membresías globales, perfiles en `AccessController`, parte de `TenantsController`, `SecurityController`). No es inconsistencia: es capa identidad vs. capa menú/ERP.  
  - **Solo `Session` sin `perm:`** (explícito `Policy = "Session"` o `[Authorize]` en clase): catálogos de bajo riesgo o “datos propios” — `GeographyController`, `ActivityController`, `GET access/me/menu`, `GET access/me/permissions`.  
  - **Públicos**: `[AllowAnonymous]` en `AuthController`, `SetupController`, planes/deployment públicos, `TenantsController` `public-settings`, etc.  
  - **Regla para código nuevo**: si el endpoint expone datos de negocio del tenant y no es “solo menú / solo yo”, añadir `perm:...` alineado al menú y a `PermissionHandler`; si es operación solo Admin/SuperAdmin de plataforma, `Roles` o `GlobalSuperAdmin` según aplique.

---

## P1 — FluentValidation: comandos sin `*CommandValidator.cs` en su carpeta

### Compras / órdenes

- [x] `ValidarCompraCommand`
- [x] `RechazarCompraCommand`
- [x] `AprobarCompraCommand` (validador además de la transacción existente)
- [x] `EnviarOrdenCompraCommand`
- [x] `AprobarOrdenCompraCommand`
- [x] `CancelarOrdenCompraCommand`
- [x] `VincularFacturaAOrdenCompraCommand`

### Gastos

- [x] `ValidarGastoCommand`
- [x] `RechazarGastoCommand`
- [x] `AprobarGastoCommand`

### Inventario

- [x] `EjecutarAjusteCommand`
- [x] `CancelarAjusteCommand`
- [x] `CancelarTransferenciaCommand`
- [x] `RecalcularSnapshotsCommand`

### Clientes

- [x] `UpdateCustomerCommand`
- [x] `DisableCustomerCommand`
- [x] `EnableCustomerCommand`

### Bodegas / proveedores

- [x] `EnableBodegaCommand`
- [x] `DisableBodegaCommand`
- [x] `EnableProveedorCommand`
- [x] `DisableProveedorCommand`

### Sucursales

- [x] `UpdateBranchCommand`
- [x] `DisableBranchCommand`
- [x] `EnableBranchCommand`

### Catálogo productos (catalogs)

- [x] `CreateBrandCommand`
- [x] `CreateProductCategoryCommand`
- [x] `CreateProductLineCommand`
- [x] `CreateProductSubcategoryCommand`
- [x] `CreateProductTypeCommand`
- [x] `CreateTariffCommand`
- [x] `CreateTaxRateCommand`
- [x] `CreateUnitOfMeasureCommand`
- [x] `UpdateProductCategoryCommand`
- [x] `UpdateProductLineCommand`
- [x] `UpdateProductSubcategoryCommand`

### Acceso / auth / tenants / security

- [x] `BootstrapLoginCommand`
- [x] `RegisterTenantWithAdminCommand`
- [x] `SwitchTenantCommand` (módulo Access)
- [x] `UpsertMembershipCommand`
- [x] `RevokeMembershipCommand`
- [x] `UpsertProfilePermissionsCommand`
- [x] `LoginCommand`
- [x] `RegisterCommand`
- [x] `PasswordResetCommand`
- [x] `SuperAdminLoginCommand`
- [x] `ClaimInitialSuperAdminCommand`
- [x] `SwitchTenantCommand` (módulo Auth, si aplica mismo criterio)
- [x] `CreateTenantCommand`
- [x] `UpdateTenantCompanyCommand`
- [x] `UpdateTenantGlobalParametersCommand`
- [x] `UpdateTenantSubscriptionCommand`
- [x] `UpdateTenantPasswordResetModeCommand`
- [x] `UpsertSecurityAdminScopesCommand`

---

## P2 — Transacciones explícitas (`IUnitOfWork.BeginTransactionAsync`)

**Ya cubiertos (referencia):** `AprobarCompra`, `EjecutarAjuste`, `ConfirmarTransferencia`, `EmitirFacturaElectronica`, `AprobarGasto`.

### Candidatos a auditar / envolver en transacción

- [x] `CrearCompraCommandHandler`
- [x] `CrearVentaCommandHandler` (secuencial SRI + factura + actividad en una transacción; validación de stock sigue antes del `Begin`)
- [x] `ValidarVentaCommandHandler`
- [x] `CrearGastoCommandHandler`
- [x] `ValidarGastoCommandHandler`
- [x] `RechazarGastoCommandHandler`
- [x] `ValidarCompraCommandHandler`
- [x] `RechazarCompraCommandHandler`
- [x] `VincularFacturaAOrdenCompraCommandHandler`
- [x] `CrearTransferenciaCommandHandler`
- [x] `CancelarTransferenciaCommandHandler`
- [x] `CrearAjusteCommandHandler`
- [x] `CancelarAjusteCommandHandler`
- [x] `CreateJournalEntryCommandHandler`
- [ ] Otros comandos multi-agregado detectados en revisión

---

## P3 — CQRS estricto (comandos que devuelven `Result<*Dto>`) — **cerrado (decisión documentada)**

**Decisión:** **CQRS pragmático** para este monolito API + cliente SPA. Los comandos MediatR **pueden** devolver `Result<TDto>` (o DTOs de sesión / config) cuando el caso de uso ya tiene el agregado materializado y el contrato de respuesta es estable para la pantalla que dispara la acción. **No** se impone migrar masivamente a `Guid` / `Unit` + `GET` subsiguiente.

**Rationale breve:** menos ida-vuelta HTTP, menos desincronización entre “id devuelto” y vista, y coherencia con controladores que ya serializan el DTO del comando. Un CQRS “puro” (solo escritura + proyecciones en capa lectura) queda reservado para cuando exista read model explícito, proyección pesada o necesidad de caché HTTP (`ETag` / `304`) en un recurso GET dedicado.

**Cuándo valorar `Guid`/`Unit` + GET en comandos nuevos o refactors puntuales:** payload muy grande; composición de varias fuentes de lectura; mismo recurso consumido por clientes que solo leen y se benefician de GET cacheable; o testabilidad del handler de escritura aislando proyección.

**Ámbito al que aplica la convención (firmas actuales válidas; sin refactor forzado):**

- [x] Compras: `CrearCompra`, `ValidarCompra`, `RechazarCompra`, `AprobarCompra`, órdenes (`OrdenCompraDto`, etc.)
- [x] Gastos: `CrearGasto`, `ValidarGasto`, `RechazarGasto`, `AprobarGasto`
- [x] Config: `UpsertConfiguracionSRI`, `UpsertConfiguracionFacturacion` (y similares)
- [x] Inventario: ajustes / transferencias (DTOs de respuesta en comandos)
- [x] Maestros: productos, marcas, proveedores, bodegas, clientes, sucursales, contabilidad
- [x] Access: `RegisterTenantWithAdmin`, `BootstrapLogin`, `SwitchTenant` → `SessionResponseDto` (respuestas de flujo de identidad / sesión)

---

## P4 — Auditoría de dominio — **cerrado (política documentada)**

### Jerarquía habitual (`AuditableEntity` y derivados)

- **`AuditableEntity`**: `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` — pensado para **mutaciones iniciadas por un usuario** (HTTP / comando con `ICurrentUser`).
- **`MasterEntity`**: hereda auditoría + `IsActive` + `Disable`/`Enable` — **catálogos y maestros** que no se borran físicamente.
- **`DocumentEntity`**: hereda auditoría + ciclo de vida **Borrador → Contabilizado → Anulado** — documentos transaccionales.
- **`ITenantEntity`**: solo exige `TenantId` — puede combinarse con las bases anteriores **o** usarse **sola** cuando aplique la excepción siguiente.

### Excepción: `ITenantEntity` sin `AuditableEntity` (filas técnicas / materializadas)

**Regla:** una entidad puede implementar **solo** `ITenantEntity` (sin heredar `AuditableEntity`) cuando:

1. El ciclo de vida lo gobierna **código de infraestructura** (hosted services, colas, jobs) y no un flujo CRUD de pantalla con actor humano estable en cada paso, **y**
2. La trazabilidad relevante ya está modelada con **campos propios del dominio** (timestamps de job, estado, mensaje de error, JSON de resultado, etc.).

**Casos concretos en el repo:**

- [x] **`KardexReporte`**: cola de informes asíncronos; estados `Pendiente` → `Procesando` → `Completado`/`Error`; `SolicitadoEn` / `CompletadoEn` sustituyen el significado de auditoría genérica. No aplica `CreatedBy` único por transición de worker.
- [x] **`KardexSnapshot`**: saldo valorizado **calculado** por worker nocturno; `ComputadoEn` es la huella temporal. Recomputar sobrescribe métricas: no es un “update por usuario” al estilo maestro.

**No** se introduce `IAuditableEntity` en esta iteración: el tipado actual (`AuditableEntity` / `MasterEntity` / `DocumentEntity` / solo `ITenantEntity`) es suficiente si se respeta la regla anterior al añadir entidades nuevas.

---

## P5 — Front / contratos (opcional)

- [ ] Alinear manejo de errores de features nuevas con respuesta del `ExceptionMiddleware`
- [ ] Paridad validación cliente (p. ej. Zod) donde falte

---

## Cómo usar este archivo

1. Cada PR puede marcar ítems concretos con `[x]`.  
2. Si un ítem se divide en sub-tareas, enlazar el PR en una nota bajo el ítem.  
3. Regenerar listas P1 si se añaden comandos nuevos (script: carpeta `*Command.cs` sin `*CommandValidator.cs` en el mismo directorio).
