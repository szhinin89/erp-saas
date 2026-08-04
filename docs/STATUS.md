# Project Status

**Single source of truth** for delivery state. Updated: **2026-07-25** · Kernel refactor: **2026-06-05**.

---

## Piloto operativo Sumak — uso supervisado (2026-08-03)

**Estado: READY_FOR_PILOT / uso supervisado.** No implica producción estable ni cierre de módulo — es habilitación para operar con supervisión directa mientras se completan las limitaciones aceptadas abajo.

`SUMAK_E2E_01_STATUS: PASSED`. Commits relacionados: `da1a2381` (reporte de stock actual por bodega), `cef699d6` (reporte de compras por proveedor), `c49da503` (reportes mínimos en el menú).

**Capacidades validadas (E2E manual):**
- Compra manual y creación de Item desde línea de compra
- IVA compra/venta + precio de venta resuelto correctamente
- Confirmación de compra
- Stock actual y Kardex
- Venta POS con cobro en efectivo y cálculo de vuelto
- Factura electrónica autorizada
- Caja actualizada tras la venta
- Reportes de Ventas, Compras y Stock funcionando
- Devolución de compra bloqueada correctamente por stock insuficiente
- 0 errores HTTP 5xx y 0 errores de consola durante la prueba E2E

**Limitaciones aceptadas (no bloquean el piloto, sí producción):**
- SRI producción no validado (solo ambiente de pruebas)
- Recepción física sin factura previa: pendiente
- Reportes sin exportación a Excel/PDF
- Reportes de ventas/compras alcance company-scoped (no consolidado multi-sucursal)
- Caja consolidada diaria: pendiente
- CxP/CxC avanzado: pendiente
- Limpieza global de lint/architecture/e2e: fuera de este cierre

---

## Backlog futuro UX

### MEJORA_FUTURA_UX_01 — Command Palette / Buscador rápido de navegación

- **Estado:** BACKLOG / FUTURE
- **Prioridad:** P2
- **Tipo:** UX / Navegación / Productividad
- **Dependencia:** App Drawer estabilizado y `navigation.config.ts` como SSOT.
- **Objetivo:** Permitir buscar y abrir formularios con `Ctrl+K` / `Cmd+K` usando la misma fuente de verdad del menú.
- **Fuera de alcance actual:** No implementar código, no tocar backend, no cambiar rutas, no cambiar permisos ni modificar el App Drawer.
- **Motivo:** Mejora no bloqueante para usuarios avanzados cuando existan más pantallas.

---

## Estado actual (2026-06-24)

**Completado**
- Arquitectura base terminada (Clean Architecture + CQRS)
- Autenticación JWT + Refresh Token
- Multi-tenant por `tenant_id` + `company_id`
- Cambio de empresa (multi-company)
- Dashboard unificado
- ERP Core congelado
- Items Module FROZEN v1.0 (2026-06-17)
- **Items Module — Rediseño flujo de creación: FROZEN v2.0 (2026-07-02)** — reemplaza v1.0: código de barras obligatorio (mínimo 1, exactamente 1 principal), códigos de proveedor opcionales (`ItemSupplierCode`, 0..N, FK a `BusinessPartner`), categoría y marca obligatorias en creación, eliminación de flags booleanos de impuesto (`AppliesVatOnSale/Purchase/ExciseTax` — el código tributario es la única fuente de verdad, alineado con la Infraestructura Tributaria CLOSED), precio inicial creado atómicamente junto con el ítem (`ItemPrice` contra lista DEFAULT/PVP)
- **Items Module — Auditoría por fases, Fase 1 (Información Base del Item): COMPLETADA (2026-07-02)** — SKU editable y único por tenant (índice BD), marca/categoría con FK real e integridad activa validada, breadcrumb de categoría, profundidad máxima del árbol de categorías configurable por empresa (`OrgSettings`, default 3). Detalle completo: [`docs/items/PHASE1-ITEM-IDENTITY.md`](items/PHASE1-ITEM-IDENTITY.md)
- **Items Module — Auditoría por fases, Fase 2 (Identificación del Item): COMPLETADA (2026-07-02)** — código de barras único globalmente por tenant (antes solo por ítem), código de proveedor único por `(tenant_id, supplier_id, code)`, proveedor obligatorio por cada entrada de código de proveedor. Detalle completo: [`docs/items/PHASE2-ITEM-IDENTIFICATION.md`](items/PHASE2-ITEM-IDENTIFICATION.md)
- **Items Module — Auditoría por fases, Fase 3 (Tributación del Item): COMPLETADA (2026-07-02)** — códigos SRI (`SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode`) confirmados como única fuente de verdad, sin cambios; campos de cuenta contable (`VatAccountId`/`PurchaseVatAccountId`/`ExciseAccountId`) retirados del contrato público del módulo Items por no tener módulo de Contabilidad que los respalde (quedan reservados internamente); `SriServiceCode` retirado del formulario por no tener catálogo SRI de respaldo. Sin impacto en Ventas/Compras/Facturación (siguen resolviendo impuestos vía `ISriTaxResolver`, Infraestructura Tributaria CLOSED intacta). Detalle completo: [`docs/items/PHASE3-ITEM-TAXATION.md`](items/PHASE3-ITEM-TAXATION.md)
- **Items Module — Auditoría por fases, Fase 4 (Comercial del Item): COMPLETADA (2026-07-02)** — confirmado: precio inicial siempre a la lista de precios predeterminada, sin selector en el formulario; corregido símbolo de moneda hardcodeado (`$`) en `PricingTab.tsx`, ahora refleja `PriceList.CurrencyCode` real. Sin cambios de backend. Detalle completo: [`docs/items/PHASE4-ITEM-COMMERCIAL.md`](items/PHASE4-ITEM-COMMERCIAL.md)
- **Items Module — Auditoría por fases, Fase 5 (Inventario y Venta del Item): COMPLETADA (2026-07-02)** — confirmado: la configuración de Inventario/Venta (`TracksStock`, lotes, series, decimales, disponibilidad POS/Web/Mobile) es intencionalmente independiente del `ItemType`, sin restricciones ni defaults condicionados por tipo. Sin cambios de código. Detalle completo: [`docs/items/PHASE5-ITEM-INVENTORY-SALE.md`](items/PHASE5-ITEM-INVENTORY-SALE.md)
- **Items Module — Auditoría por fases, Fase 6 (Variantes del Item): COMPLETADA (2026-07-02)** — SKU de variante único globalmente por tenant (antes solo por ítem), consistente con SKU de ítem (Fase 1) y barcode/código de proveedor (Fase 2). Detalle completo: [`docs/items/PHASE6-ITEM-VARIANTS.md`](items/PHASE6-ITEM-VARIANTS.md)
- **Items Module — Auditoría por fases, Fase 7 (Pricing del Item): COMPLETADA (2026-07-02)** — corregida violación de la regla "no eliminar registros": `RemoveItemPriceCommand` ahora deshabilita el precio en vez de hacer `DELETE` físico; historial de cambios de precio registrado en `UserActivity` (auditoría existente, append-only), no en tabla propia. Detalle completo: [`docs/items/PHASE7-ITEM-PRICING.md`](items/PHASE7-ITEM-PRICING.md)
- **Motor de Pricing v2 — Dominio Items+Pricing: CLOSED (2026-07-05)** — reemplaza el modelo de Fase 7: `Item.BaseSalePrice` es el SSOT del precio base; `ItemPrice` fue eliminado y reemplazado por `PricingRule` (regla de ajuste, no precio absoluto, sin quiebres de cantidad — eso pertenece al futuro módulo Promotions); `PriceList` gana una regla general opcional; `IPricingResolver` centraliza la resolución de precio (antes duplicada en 4 lugares) como única API pública que el resto del ERP debe consumir. Reabre parcialmente el freeze de Items v1.0 y de Fase 7 solo en lo referente a precios — ambos quedan reemplazados por este ADR en ese punto. Integración con Ventas/Compras/POS/Facturación (consumo real de `IPricingResolver`) queda pendiente como trabajo de esos módulos, sin reabrir este dominio. Detalle completo: [`docs/adr/ADR-021-pricing-engine-ssot.md`](adr/ADR-021-pricing-engine-ssot.md)
- **Items Module — Auditoría por fases, Fase 8 (Compras): COMPLETADA (2026-07-02)** — Compras migrado para resolver el código de proveedor vía `ItemSupplierCode` (Fase 2) según el proveedor real de la factura, con fallback al campo legacy `Item.Code.PurchaseCode`; corregido defecto preexistente que impedía cargar `Item.SupplierCodes` en cualquier lectura del agregado (`.Include()` faltante). Detalle completo: [`docs/items/PHASE8-ITEM-PURCHASES.md`](items/PHASE8-ITEM-PURCHASES.md)
- **Items Module — Auditoría por fases, Fase 9 (Arquitectura — revisión transversal): COMPLETADA (2026-07-02)** — revisión de duplicación/acoplamientos/cumplimiento de infraestructuras FROZEN en las Fases 1-8; único hallazgo (duplicación menor de resolución de código de proveedor introducida en Fase 8) corregido con un helper compartido en `PurchaseDraftUseCases.cs`. **Cierra la auditoría completa del módulo Items (Fases 1-9).** Detalle completo: [`docs/items/PHASE9-ARCHITECTURE.md`](items/PHASE9-ARCHITECTURE.md)
- Customer Module FROZEN (2026-06-17)
- Compras: auditoría UX + SSOT completada (2026-06-24)
- Sales Invoice + Detail: módulo cerrado (2026-06-24)
- Payment Methods + Formas de Cobro Multi-Pago: CERRADO (2026-06-24)
- Sales Receivable (CxC deuda, sin cobros): CERRADO (2026-06-25)
- Estándar de Precisión Numérica: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Estándar de Fechas y Horas: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Infraestructura de Mensajes Visuales: CLOSED (2026-06-29) — ADR-018
- Infraestructura de Secuencias Documentales: CLOSED (2026-06-29) — ADR-019
- **Infraestructura de Entity Tracking (EF Core Change Tracking): CLOSED (2026-06-30) — ADR-020**
- **Infraestructura Tributaria (Tax Infrastructure): CLOSED (2026-07-01)**
- **Infraestructura de Valores por Defecto de Facturación: CLOSED (2026-07-01) — migrado a org_settings (Phase 8, 2026-07-01)**
- **Infraestructura Org Config Jerárquica (OrgSetting / 5 scopes): CLOSED (2026-07-01)** — `org_settings`, `IOrgSettingsRepository`, `OrgSettingKeys`; 10 endpoints GET/PUT por scope; UI en Company Settings Hub
- **Infraestructura Master Configuration UI: CLOSED (2026-07-02)** — Patrón oficial de tabs para módulos de configuración; `ConfigTabsLayout` + `items-catalog.css`; implementado en Branches, Establishments, Emission Points, Warehouses; prohibido crear variantes sin decisión arquitectónica global
- **Infraestructura de Auditoría por Dominio (Entity Audit): CLOSED (2026-07-07) — ADR-022** — contratos comunes (`AuditRecordBase`/`IAuditWriter`/`IAuditReader`/`IAuditService`/`IAuditContext`) reutilizables por todo dominio futuro; pilotos `PricingRuleAudit`/`PriceListItemAudit`; Process Audit (procesos sin `EntityId` único) queda diseñado en `AI-RULES/AUDIT-INFRASTRUCTURE.md`, sin implementar
- **Contexto Operativo del Usuario (UserSession): implementado y estabilizado (2026-07-17)** — registro de sesión operativa (empresa/sucursal/terminal) integrado a Login/SwitchCompany, expiración automática vía Hangfire, dashboard administrativo en `/admin/access/sessions` (`AdminUserSessionController`, única API pública del dominio). Detalle: [`docs/IDENTITY.md#usersession-contexto-operativo-del-usuario`](IDENTITY.md#usersession-contexto-operativo-del-usuario). Hardening Fase 12: eliminado `UserSessionController` self-service (IDOR + cero consumidores reales) en vez de endurecerlo
- **CompanyUserPreferences (preferencias de login: sucursal por defecto + modo de ingreso): ciclo cerrado (2026-07-17)** — única fuente de verdad de `DefaultBranchId`/`LoginMode`; escritura vía `UpsertCompanyUserMembershipHandler` (alta/edición de membresía) y `PUT /api/v1/admin/iam/company-users/{companyUserId}/preferences`; lectura centralizada en `CompanyUserPreferencesLoginResolver` (Login/SwitchCompany) y `GET` del mismo endpoint; `CompanyUserBranch` sigue siendo la única fuente de sucursales autorizadas (nunca se le agregó comportamiento). Auditoría de cierre (Fase H) corrigió que una sucursal desactivada podía aceptarse como `DefaultBranchId`. UI en `SecuritySettingsPage` (`/admin/security`), sin CRUD propio. Sin cambios de JWT en todo el ciclo. Detalle: [`docs/IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login`](IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login)
- **Access/IAM — Fase I-A (wiring administrativo de CompanyUserMembership): backend completado (2026-07-17)** — expone `POST /api/v1/admin/iam/memberships` (alta/edición de rol, perfil y sucursales autorizadas) y `POST /api/v1/admin/iam/memberships/revoke` (`CompanyUserMembershipsController`), que hasta esta fase no existían pese a que `UpsertCompanyUserMembershipHandler`/`RevokeCompanyUserMembershipHandler` (Fase D) estaban implementados y probados sin ningún consumidor de producción. TenantId/CompanyId nunca viajan en el request — cada Admin command (`UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand`) los resuelve del contexto autenticado (`ICurrentTenant`/`ICurrentCompany`) y delega íntegramente vía MediatR en los handlers de Fase D, sin reimplementar su lógica. `CompanyUserMembership` sigue siendo la única fuente de verdad de la relación usuario-empresa, `Role`, `ProfileId` e `IsActive` de membresía; `CompanyUserBranch` sigue siendo la única fuente de autorización de sucursal; `CompanyUserPreferences` no se modificó. Reutiliza el permiso `access.company_user_memberships.view` (mismo criterio que `AccessProfilesController`/`CompanyUserPreferencesController`) — no se introdujo un permiso `.manage` nuevo en esta fase. Sin frontend, sin invitaciones, sin cambios a `IdentityUser` ni a su `IsActive` global.
- **Access/IAM — Fase I-B (administración de CompanyUserBranch): backend completado (2026-07-17)** — expone `GET`/`PUT /api/v1/admin/iam/memberships/{membershipId}/branches` (`CompanyUserBranchesController`). `GetCompanyUserBranchesAdminQuery` proyecta las sucursales activas de la empresa de la membresía marcando cuáles están autorizadas (`{branchId, branchName, authorized}`), pensado para que un futuro selector de frontend lo consuma directamente. `UpdateCompanyUserBranchesAdminCommand` reemplaza la autorización completa todo-o-nada (ninguna escritura ocurre si cualquier `BranchId` es inválido): reactiva/crea las solicitadas, desactiva el resto — `CompanyUserBranch` sigue siendo la única fuente de verdad de sucursales autorizadas, nunca se copia a `Membership`/`Preferences`/`IdentityUser`. Hallazgo de auditoría: `IBranchRepository.GetAsync`/`GetByIdAsync` solo filtran por `TenantId` (no por `CompanyId`, a diferencia de entidades con `ForOperationalScope`) — ambos handlers filtran/comparan `Branch.CompanyId` manualmente contra la empresa de la membresía antes de aceptar cualquier sucursal, y usan el mismo mensaje para "no existe" y "pertenece a otra empresa" (mismo criterio anti-enumeración que `GetCompanyUserPreferencesAdminHandler`). Decisión documentada: lista vacía es un valor válido (revoca todas las sucursales sin desactivar la membresía) — es seguro porque `CompanyUserPreferencesLoginResolver` (Fase E) ya revalida `DefaultBranchId` en cada login y falla con `ValidationFailure` controlado si dejó de estar autorizado, nunca asumió que hubiera siempre al menos una sucursal activa. Reutiliza `access.company_user_memberships.view` — sin permiso nuevo. Sin frontend, sin cambios a `CompanyUserPreferences`/`IdentityUser`/JWT.
- **Access/IAM — Fase I-C (pantalla administrativa de usuarios empresariales): completado (2026-07-17)** — reemplaza el placeholder `/admin/users` (antes un `<Navigate>` a `/admin/roles`) por `UsersPage` (`frontend/src/modules/access/users/`), que administra `CompanyUserMembership` end-to-end: tabla principal (Usuario/Email/Perfil/Role/Estado/Sucursales autorizadas/Modo de ingreso/Acciones), modal de alta/edición de membership (`membershipService.upsertMembership`, nunca crea `IdentityUser`), modal de sucursales autorizadas (`branchAssignmentService`, Fase I-B — el frontend nunca valida pertenencia/activa/autorización previa, solo envía los `BranchId` marcados), modal de preferencias de login (reutiliza 100% el schema/servicio de Fase G, sin extraer un componente compartido con `SecuritySettingsPage` para no tocar ese ciclo ya cerrado) y revocación con confirmación vía `message.confirm` (`lib/messages`, API pública oficial). Bloqueo real detectado y resuelto: no existía ningún endpoint que listara `CompanyUserMembership` con inactivas + `ProfileName` (`GET /api/v1/security/admin-matrix`, Fase B, solo devuelve `IdentityUser` activos sin perfil) — se agregó `GET /api/v1/admin/iam/memberships` (`GetCompanyUserMembershipsAdminQuery`, solo lectura, junta `CompanyUserMembership`+`IdentityUser`+`AccessProfile`, todos ya expuestos individualmente) reutilizando `access.company_user_memberships.view`, sin permiso nuevo. Limitación conocida y documentada en código: "Sucursales autorizadas"/"Modo de ingreso" por fila se resuelven con `Promise.allSettled` por membership (sin endpoint de resumen agregado) — aceptable al volumen típico de usuarios por empresa, candidato a un endpoint agregado en una fase futura si escala. Sin invitaciones, sin cambios a `IdentityUser`/JWT/`CompanyUserPreferences`.
- **Access/IAM — Fase S1 (Security Hardening): completado (2026-07-17)** — corrige los 3 hallazgos críticos/altos de la auditoría de cierre de Access/IAM, sin agregar funcionalidad ni tocar JWT/frontend/otros módulos:
  - **5A** — `POST /api/v1/auth/register` **eliminado**. Permitía crear un usuario (con `Role` arbitrario, incl. `Admin`) en cualquier tenant existente indicando `TenantId` en el body, sin ningún control de identidad. El alta del primer usuario/tenant ya tenía un flujo seguro y dedicado (`SetupController` → `CreateInitialAdminCommand`, token de instalación de un solo uso generado por consola, nunca acepta `TenantId`/`Role` del cliente) — confirmado sin consumidor alguno en frontend antes de eliminar. `RegisterCommand`/`RegisterHandler`/`RegisterCommandValidator`/`RegisterDto` eliminados.
  - **5B** — `POST /api/v1/auth/password-reset` **eliminado**. Cambiaba la contraseña de cualquier usuario solo con `TenantId`+`Email`, sin contraseña actual, token ni OTP. El flujo oficial (`ForgotPassword` + `ResetPasswordWithToken`, token de un solo uso por email) queda como único camino. `DirectPasswordResetCommand`/`Handler`/`Validator` eliminados. Su único consumidor frontend (`PasswordResetPage.tsx`, página pública en `/password-reset`) se eliminó en el cierre final del módulo (ver entrada siguiente) — no quedan referencias vivas al flujo eliminado.
  - **5C** — `GetCompanyUserMembershipsAdminQuery`, `GetCompanyUserPreferencesAdminQuery`, `UpdateCompanyUserPreferencesAdminCommand`, `GetCompanyUserBranchesAdminQuery`, `UpdateCompanyUserBranchesAdminCommand` ahora implementan `IRequiresCompanyContext` — mismo marker que `UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand` (Fase I-A), sin inventar un mecanismo nuevo. Antes, su única defensa era comparar manualmente contra `ICurrentCompany.CompanyId` (header `X-Company-Id`, no un claim firmado), sin pasar por `ICompanyAccessGuard` — un caller con rol Admin de su propio tenant podía leer/escribir memberships, sucursales y preferencias de una empresa ajena manipulando el header, porque el bypass de rol Admin (`RuntimePermissionAuthorizer`) nunca revalidaba tenant/membership real. El marker fuerza `CompanyScopeBehavior` → `ICompanyAccessGuard.RequireCurrentCompanyAsync` antes del handler; el chequeo manual original se mantiene como defensa adicional.
  - Tests nuevos: `ERP.Architecture.Tests/AuthAttackSurfaceGuardTests.cs` (CI-bloqueante, impide reintroducir 5A/5B), `ERP.API.Tests/Auth/AuthControllerTests.cs`, `ERP.Application.Tests/Setup/CreateInitialAdminHandlerTests.cs` (prueba que el flujo alternativo seguro sigue funcionando), `ERP.Application.Tests/Behaviors/CompanyScopeBehaviorTests.cs` + `ERP.Application.Tests/Access/CompanyScopeMarkerConsistencyTests.cs` (prueban el mecanismo de 5C y que los 5 handlers corregidos usan el mismo patrón que Fase I-A).
  - **Módulo Access/IAM: apto para producción** en lo referente a estos 3 hallazgos. Deuda no crítica restante documentada en la auditoría de cierre (naming, duplicación de UI en modal de preferencias, etc.) — ver entrada de cierre final más abajo para lo que sí se resolvió en la limpieza posterior.
- **Access/IAM — Cierre final del módulo (limpieza de deuda técnica menor): completado (2026-07-17)** — módulo declarado terminado y cerrado a mantenimiento únicamente. Sin funcionalidad nueva, sin endpoints nuevos, sin cambios de comportamiento ni de contrato HTTP/BD. Alcance:
  - **Código muerto eliminado**: `PasswordResetPage.tsx`/`.css` y `passwordResetSchema.ts` (frontend, único consumidor de `POST /auth/password-reset`, eliminado en Fase S1 — la página había quedado sin backend detrás); ruta `/password-reset` retirada de `publicRoutes.tsx`; entradas `/api/v1/auth/register` y `/api/v1/auth/password-reset` retiradas de `PUBLIC_AUTH_PATHS` (`authRefreshPolicy.ts`, rutas ya inexistentes); 7 claves i18n huérfanas (`reset.title`, `reset.subtitle`, `reset.directSubtitle`, `reset.error.disabled`, `reset.error.mismatch`, `reset.subscriberCheck.enabled/unavailable`) retiradas de `es/en/qu.json`; `RegisterDto` (backend, ya sin uso desde antes de Fase S1) eliminado.
  - **Naming corregido (solo archivos, sin tocar clases/namespaces/contratos)**: `Entities/Membership.cs` → `CompanyUserMembership.cs` (la clase ya se llamaba así); carpetas `UseCases/UpsertMembership`/`RevokeMembership` → `UpsertCompanyUserMembership`/`RevokeCompanyUserMembership` (ya coincidían con el namespace, no con el nombre de carpeta); los 6 archivos `Upsert/RevokeMembership{Command,CommandValidator,Handler}.cs` dentro renombrados a `Upsert/RevokeCompanyUserMembership{Command,CommandValidator,Handler}.cs` (las clases ya tenían el nombre completo).
  - **No se encontró** ningún Command/Query/DTO/validator/servicio registrado sin consumidor en Access/IAM más allá de lo ya listado — confirmado por auditoría previa y revalidado en esta fase.
- **ADR-026 (Accounting Core Architecture): ACCEPTED (2026-07-24)** — diseño arquitectónico aprobado por Architecture Review Board (`docs/adr/ADR-026-accounting-core.md`): bounded context (`Account`/`AccountingPeriod`/`JournalEntry`/`PostingRule`), `CompanyId`-scoped obligatorio en los 4 aggregates, integración exclusivamente vía Domain Events (sin dependencias directas hacia Sales/Purchases), `JournalEntrySequence` independiente de `IDocumentSequenceRepository` (ADR-019), alcance v1 limitado a Sales/Purchases/Caja/Inventory.
  - **Fase 0 (housekeeping, 2026-07-24)**: eliminado `ERP.Application/Common/Interfaces/IAccountingService.cs` (dead code confirmado — cero implementaciones, cero consumidores).
  - **Fase 1 — Fundamentos de dominio (2026-07-24)**: `Account`/`AccountingPeriod`/`PostingRule` con comportamiento completo (`Create`, `Rename`, `Activate`/`Disable`/`Enable`, `Close`, `Lock`, `UpdateMapping`); `JournalEntry` como esqueleto de identidad únicamente (sin líneas, sin `Post()`/`Reverse()` — explícitamente fuera de esta fase). VO `AccountCode`, enums `AccountType`/`AccountNature`/`PeriodStatus`/`JournalEntryStatus`. 7 domain events (`AccountCreatedEvent`/`AccountActivatedEvent`/`AccountDisabledEvent`/`AccountingPeriodCreatedEvent`/`AccountingPeriodClosedEvent`/`AccountingPeriodLockedEvent`/`PostingRuleCreatedEvent`).
  - **Fase 1.2/1.3/1.4 — Persistencia (2026-07-25)**: 4 configuraciones EF Core, 4 tablas (`accounts`, `accounting_periods`, `journal_entries`, `posting_rules`), 3 índices únicos (`uq_accounts_company_code`, `uq_accounting_periods_company_year_period`, `uq_posting_rules_company_source_fact`) + 1 FK (`journal_entries.accounting_period_id → accounting_periods.id`, `RESTRICT`). Migración `20260725000917_AddAccountingCoreFoundations` **aplicada** en desarrollo, auditada por Database Migration Review Board — `ACCEPTED`.
  - **Fase 2.0/2.1/2.2 — Application + API (2026-07-25)**: 4 repositorios (`IAccountRepository`/`IAccountingPeriodRepository`/`IJournalEntryRepository`/`IPostingRuleRepository`) con filtrado `TenantId`+`CompanyId` en toda consulta; 11 Commands + 6 Queries + 11 Validators FluentValidation + 17 Handlers (patrón CQRS/MediatR, sin ningún `AccountingService`/`AccountService`/`PostingRuleService`); concurrencia con patrón pre-check → `SaveChanges` → `IDatabaseExceptionTranslator` en los 3 Commands de creación; permisos `accounting.view/create/update/delete`; `AccountingController` (`api/v1/accounting`) con 14 endpoints REST (6 GET, 3 POST, 5 PATCH, sin `DELETE` — baja lógica vía `PATCH .../disable`). Auditado por Architecture Review Board (Auditoría Final de Implementación) — `APPROVED WITH MINOR CHANGES` (hallazgo de documentación ya resuelto con esta entrada; longitudes de validación duplicadas entre `Validator`/EF `Configuration` sin constante compartida queda como deuda menor no bloqueante).
  - **Explícitamente NO implementado hasta Fase 2.2**: Posting Engine (ADR-026 §8), `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence` (ADR-026 §7), integración vía eventos con Sales/Purchases/Caja/Inventory, reportes financieros. `JournalEntry` no tenía ningún endpoint ni caso de uso — solo existía como tabla y aggregate de identidad.
  - **Fase 3.1 — Posting Engine inicial (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/` — `IPostingEngine.PostAsync(PostingFact, ct)` como único contrato público (`PostingFact`: `TenantId`/`CompanyId`/`SourceModule`/`FactType`/`SourceEventId`/`EntryDate`, sin Currency/Amount/Lines/impuestos — fuera de esta fase). Pipeline interno fijo (Idempotency → PostingRuleResolver → PostingPeriodResolver → PostingPeriodGuard → JournalFactory → JournalValidator → Persistencia), componentes `internal` sin registro propio en DI — solo `IPostingEngine → PostingEngine` se registra. `PostingOutcomeDto`/`PostingOutcomeStatus` (`Created`/`AlreadyProcessed` — reintento del mismo hecho **es éxito**, nunca `Conflict`). Códigos de error: `RULE_NOT_FOUND`, `PERIOD_NOT_OPEN`, `VALIDATION_FAILED`. `JournalFactory` construye vía `JournalEntry.Create()` (sin DTO intermedio) con `SystemActor = Guid.Empty` (mismo patrón que `ExpireUserSessionsHandler`) y descripción determinística `"{SourceModule} — {FactType} — {SourceEventId}"`. `JournalValidator` es NO-OP documentado (partida doble aún no existe). Idempotencia real: `IJournalEntryRepository.FindByKeyAsync` + índice único `uq_journal_entries_company_source_event_fact` (`company_id`, `source_module`, `source_event_id`, `source_event_type`) — reemplaza el índice no-único anterior (migración `20260725013347_AddJournalEntryIdempotencyKey`); en carrera, `IDatabaseExceptionTranslator` traduce la violación UNIQUE y la segunda ejecución re-consulta y retorna `AlreadyProcessed`. `IAccountingPeriodRepository.FindContainingDateAsync` agregado para resolución de período por fecha. Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PostingEngineTests.cs` — RuleNotFound/PeriodNotOpen/Created/AlreadyProcessed, mocks) + 2 de integración PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/PostingEngineIntegrationTests.cs` — doble ejecución secuencial idempotente, concurrencia real con dos tareas paralelas verificando un único `JournalEntry`). **Pendiente al cierre de Fase 3.1**: `PostingRule.IsActive == false` no se validaba — resuelto en Fase 3.3 (ver abajo). `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence`, endpoints HTTP del Posting Engine y reportes financieros siguen sin implementar.
  - **Fase 3.3 — Primer consumidor real: SalesInvoiceAuthorizedPostingTranslator (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/Translators/SalesInvoiceAuthorizedPostingTranslator.cs` — `INotificationHandler<SalesInvoiceAuthorizedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>` (sin `DbContext`, sin repositorios de Sales), construye `PostingFact{ SourceModule="Sales", FactType="InvoiceIssued" }` y llama `PostAsync`; si falla, `LogWarning` con `InvoiceId`/`InvoiceNumber`/`Code`/`Error` y **no lanza excepción** — la autorización de la venta nunca se revierte por un problema de configuración contable. `SalesInvoiceAuthorizedEvent` enriquecido con `CompanyId`/`IssueDate` y `TenantId` ahora fijado en el constructor (antes quedaba siempre `null` — defecto real detectado y corregido, no solo teórico); los 3 datos se toman del propio agregado `SalesInvoice` en `Authorize()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente. `PostingRuleResolver` ahora trata `PostingRule` inactiva igual que regla inexistente (`RULE_NOT_FOUND`, sin código nuevo) — el filtro vive en el Resolver (Application), no en `IPostingRuleRepository.FindByKeyAsync` (compartido con `CreatePostingRuleHandler`, que sigue necesitando ver reglas inactivas para su pre-check de duplicados). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/SalesInvoiceAuthorizedPostingTranslatorTests.cs`, mocks) + 3 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/SalesInvoiceAuthorizedPostingIntegrationTests.cs`).
  - **✅ Hallazgo crítico de Fase 3.3 — RESUELTO (Fase 3.3.5, 2026-07-25)**: la re-entrancia de `SaveChangesAsync` detectada al conectar el primer Translator (`PostingPipeline` llamaba a `IJournalEntryRepository.SaveChangesAsync()` desde dentro de `ErpDbContext.SaveChangesAsync`, produciendo `DbUpdateConcurrencyException` real cuando coexistía con el handler de Caja sobre el mismo evento) quedó corregida con dos cambios: (1) `PostingPipeline` ya no comitea — solo hace `AddAsync` (staging) y retorna; la persistencia física pertenece exclusivamente al ciclo externo de `ErpDbContext.SaveChangesAsync`, misma convención que ya seguían `SalesInvoiceAuthorizedHandler` (Caja) y los `*AuditHandler`. (2) `IJournalEntryRepository.AcquireIdempotencyLockAsync(companyId, sourceModule, sourceEventId, factType, ct)` — nuevo método, implementado en `JournalEntryRepository` con `pg_advisory_xact_lock(int4, int4)` (mismo mecanismo que `DocumentSequenceRepository`/ADR-019, `StableHash` duplicado deliberadamente sin helper compartido), invocado por `PostingIdempotencyGuard` **antes** de `FindByKeyAsync`, sobre la transacción ambiente (nunca abre ni comitea transacción propia). Con el lock, dos ejecuciones concurrentes para la misma clave se serializan antes de competir por el mismo `INSERT` — la violación UNIQUE deja de ocurrir en el camino normal (el índice `uq_journal_entries_company_source_event_fact` queda como protección final, no como mecanismo primario). El stub de `ICashSessionRepository` en los tests de integración fue retirado — la suite corre con el repositorio real. Se agregó además un test de doble publicación concurrente del mismo `SalesInvoiceAuthorizedEvent` (Caja + Accounting reaccionando simultáneamente en dos transacciones distintas) que confirma ausencia de excepción y un único `JournalEntry`. Detalle completo del proceso de diseño: revisiones ARB Fase 3.3.1 (SaveChanges ownership) a 3.3.4 (readiness review). Habilitado conectar un segundo Translator (Purchases) con este mismo patrón.
  - **Fase 3.4 — Segundo consumidor real: PurchaseInvoiceConfirmedPostingTranslator (2026-07-25)**: replica exactamente el patrón de Fase 3.3 sobre `PurchaseInvoice.Confirm()`. `PurchaseInvoiceConfirmedEvent` enriquecido de forma aditiva con `CompanyId`/`IssueDate` (tomados del propio agregado en `Confirm()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente) — único consumidor preexistente del evento (`PurchaseInvoiceAuditHandler`, Entity Audit ADR-022) no requirió cambios, es aditivo. `ERP.Application/Modules/Accounting/Posting/Translators/PurchaseInvoiceConfirmedPostingTranslator.cs` — `INotificationHandler<PurchaseInvoiceConfirmedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>`, construye `PostingFact{ SourceModule="Purchases", FactType="InvoiceReceived" }` y llama `PostAsync`; si falla, `LogWarning` y **no lanza excepción** — la confirmación de la compra nunca se revierte por un problema de configuración contable. `PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository` — sin ningún cambio (mismo Posting Engine, ningún `SaveChangesAsync`/transacción/lock nuevo). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PurchaseInvoiceConfirmedPostingTranslatorTests.cs`, mocks) + 4 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/PurchaseInvoiceConfirmedPostingIntegrationTests.cs` — JournalEntry Draft, fallo sin revertir, idempotencia, concurrencia con advisory lock). Retenciones (`IssuedWithholding`) quedan explícitamente fuera de alcance — hecho contable distinto, Translator futuro si se requiere.
  - **Fase 3.5.2 — PostingFact Enrichment, cierre de ADR-026 §4 (2026-07-25)**: prerrequisito para el futuro motor de partida doble (`JournalEntryLine`, diseñado en Fase 3.5.1, aún no implementado). `SalesInvoiceAuthorizedEvent` y `PurchaseInvoiceConfirmedEvent` enriquecidos de forma aditiva con `Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount` — tomados de las propiedades ya computadas del propio agregado (`SalesInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Authorize()`, `PurchaseInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Confirm()`), sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany`. `PostingFact` extendido con los mismos 4 campos más `GrandTotal` — deliberadamente **sin** `Currency`/`ExchangeRate`/`Branch`/`CostCenter`/`Metadata` (fuera de alcance v1 por ADR-026 §10 y por ausencia de módulo `CostCenter`, ver Fase 3.5.1). `SalesInvoiceAuthorizedPostingTranslator`/`PurchaseInvoiceConfirmedPostingTranslator` actualizados únicamente en la construcción de `PostingFact` (una línea cada uno) — sin cambio de patrón, dependencias ni manejo de errores. Posting Engine (`PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository`) sin ningún cambio — los montos nuevos viajan en `PostingFact` pero `JournalFactory` todavía no los consume (eso pertenece a la fase de `JournalEntryLine`). Compatibilidad: 10 call sites de construcción de `SalesInvoiceAuthorizedEvent`/`PurchaseInvoiceConfirmedEvent`/`PostingFact` en código productivo y tests actualizados; regresión completa en verde (452 `ERP.Application.Tests`, 254 `ERP.Domain.Tests`, 97 `ERP.Architecture.Tests`, 10 de integración PostgreSQL real en `ERP.Infrastructure.Tests/Accounting`). ADR-026 §4 queda implementado en su parte de montos (`Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount`, alcance exacto de esta fase); **pendiente** el otro requisito original de §4 para `SalesInvoiceAuthorizedEvent` — *"información de pago necesaria para la contabilización (forma de pago / referencia de cobro)"* — no incluido en el alcance aprobado de Fase 3.5.2, queda para una fase posterior o para reevaluación explícita si el motor de partida doble no lo necesita.
  - **Fase 3.5.3 — Modelo de dominio de partida doble (2026-07-25)**: implementa únicamente el modelo de dominio aprobado en Fase 3.5.1 — sin persistencia EF Core, sin migración, sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`. `JournalEntryLine` (nueva entidad hija de `JournalEntry`, `ERP.Domain/Modules/Accounting/Entities/`) con invariante propio: exactamente uno de `Debit`/`Credit` mayor a cero, nunca ambos con valor ni ambos en cero (`JournalEntryLine.Create`, `IMustHaveTenant`, sin `CompanyId` propio — igual patrón que `PurchaseInvoiceDetail`/`SalesInvoiceDetail`). `JournalEntry` incorpora `Lines` (`IReadOnlyCollection<JournalEntryLine>`), `AddLine(accountId, description, debit, credit)` (construye la línea internamente, asigna `SortOrder` incremental) y `EnsureBalanced()` (Σ Debit == Σ Credit) — ninguno con consumidor todavía: `JournalFactory` sigue construyendo solo el encabezado (0 líneas), por lo que `EnsureBalanced()` se cumple trivialmente (0 == 0) sin invocarse desde ningún flujo real. `PostingRuleLine` (nueva entidad hija de `PostingRule`) con `AccountId`/`Nature` (`AccountNature`, reutilizado)/`AmountKind` (`PostingAmountKind`, enum nuevo)/`SortOrder`. `PostingRule` incorpora `Lines` + `AddLine(...)` — coexiste con `DebitAccountId`/`CreditAccountId` planos sin retirarlos (transición, ningún consumidor migra todavía). `PostingAmountKind` (`Subtotal`/`TaxVat`/`TaxIce`/`Discount`/`Retention`/`GrandTotal`) — únicos 6 valores aprobados en Fase 3.5.1, ninguno adicional. Hallazgo de compatibilidad EF Core resuelto: `JournalEntry.Lines`/`PostingRule.Lines` son navegaciones nuevas que `RelationshipDiscoveryConvention` detecta y registra como entidades independientes con tabla propia aunque se las ignore a nivel de propiedad (`builder.Ignore(x => x.Lines)` en cada `IEntityTypeConfiguration` no basta) — requiere además `modelBuilder.Ignore<JournalEntryLine>()`/`Ignore<PostingRuleLine>()` a nivel de `ErpDbContext.OnModelCreating()` para que el modelo runtime siga coincidiendo exactamente con la migración ya aplicada (`dotnet ef migrations has-pending-model-changes` verificado en `No changes`). Tests: 24 nuevos en `ERP.Domain.Tests/Accounting/` (`JournalEntryLineTests`, `JournalEntryTests`, `PostingRuleLineTests`) — Debit/Credit válidos, ambos con valor, ambos en cero, montos negativos, cuenta vacía, creación con líneas, `SortOrder` incremental, colección de solo lectura, `EnsureBalanced()` con/sin líneas balanceadas y desbalanceadas, naturaleza y `AmountKind` correctos. Regresión completa en verde: 278 `ERP.Domain.Tests` (254+24), 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 219 `ERP.Infrastructure.Tests` (incluye las 10 suites de integración PostgreSQL de Accounting ya existentes, sin cambios de comportamiento).
  - **Fase 3.5.4 — Persistencia de JournalEntryLine y PostingRuleLine (2026-07-25)**: única y exclusivamente la capa de persistencia del modelo aprobado en Fase 3.5.3 — sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`, sin generación automática de líneas, sin consumo de `PostingAmountKind`. `JournalEntryLineConfiguration`/`PostingRuleLineConfiguration` (`ERP.Infrastructure/Accounting/Persistence/Configurations/`) nuevas — `journal_entry_lines`/`posting_rule_lines`, `Debit`/`Credit` en `numeric(18,2)` (Estándar de Precisión Numérica INMUTABLE, CLAUDE.md). `JournalEntryLine.AccountId` con FK real a `accounts` (`Restrict`) — a diferencia de `PostingRuleLine.AccountId`, columna plana sin FK (mismo criterio ya vigente para `PostingRule.DebitAccountId`/`CreditAccountId`: configuración de datos, existencia se valida en Application al resolver, no en la base de datos). `JournalEntryConfiguration`/`PostingRuleConfiguration`: `Ignore(x => x.Lines)` reemplazado por `HasMany(x => x.Lines).WithOne().HasForeignKey(...).OnDelete(Cascade)` (mismo patrón que `PurchaseInvoice`→`PurchaseInvoiceDetail`) — cascade porque ninguna línea tiene sentido de existir sin su encabezado. `ErpDbContext`: retirados los dos `modelBuilder.Ignore<T>()` de Fase 3.5.3 (ya no aplican, las líneas ahora se mapean), agregados `DbSet<JournalEntryLine>`/`DbSet<PostingRuleLine>`. Migración `20260725165737_AddJournalEntryLineAndPostingRuleLine` — crea ambas tablas, 2 FKs (`journal_entry_lines→accounts` Restrict, `journal_entry_lines→journal_entries` Cascade, `posting_rule_lines→posting_rules` Cascade), 4 índices; no toca ninguna columna existente de `posting_rules` (`DebitAccountId`/`CreditAccountId` intactos, coexistencia deliberada durante la transición). Verificado `dotnet ef migrations has-pending-model-changes` → `No changes`. Tests: 8 nuevos de persistencia PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/JournalEntryLinePersistenceTests.cs`, `PostingRuleLinePersistenceTests.cs`) — guardar con líneas, recuperar navegación (`Include(x => x.Lines)`), integridad referencial (FK real en `JournalEntryLine` vs. ausencia deliberada de FK en `PostingRuleLine`), cascade delete de líneas al eliminar el encabezado. Regresión completa en verde: 278 `ERP.Domain.Tests`, 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 227 `ERP.Infrastructure.Tests` (18 en `Accounting/`, incluye las 10 suites de Sales/Purchases/PostingEngine ya existentes sin cambio de comportamiento).
  - **Fase 3.5.5 — JournalFactory & JournalValidator: motor de partida doble real (2026-07-25)**: `JournalFactory` deja de construir solo el encabezado — ahora itera `PostingRule.Lines` (`PostingRuleLine`, persistido en Fase 3.5.4), resuelve el monto de cada línea exclusivamente por `PostingAmountKind` (`Subtotal→fact.Subtotal`, `TaxVat→fact.TotalVat`, `TaxIce→fact.TotalIce`, `Discount→fact.TotalDiscount`, `GrandTotal→fact.GrandTotal`, `Retention→0m` — no disponible en `PostingFact` todavía, fuera de alcance de esta fase) y llama `JournalEntry.AddLine(...)` por cada línea con monto distinto de cero (líneas en cero se omiten, nunca se contabilizan). `JournalValidator` deja de ser NO-OP: valida mínimo 2 líneas, `AccountId` requerido, exactamente un monto (Débito o Crédito) por línea, ninguna cuenta simultáneamente en Débito y Crédito del mismo asiento, totales distintos de cero, y balance (`entry.EnsureBalanced()`, código `VALIDATION_FAILED` en cualquier fallo). **2 excepciones mínimas y necesarias, declaradas explícitamente**: (1) `PostingPipeline.ExecuteAsync` — una línea agrega el parámetro `PostingRule` ya resuelto a la llamada de `JournalFactory.Create(...)` (el orden de las 7 etapas no cambia, solo se propaga un dato ya calculado); (2) `PostingRuleRepository.FindByKeyAsync` — agrega `.Include(x => x.Lines)`, sin el cual `PostingRule.Lines` llegaría siempre vacío a `PostingRuleResolver` (`PostingRule` es `sealed` sin navegación `virtual`, no hay lazy loading posible). `PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalEntryRepository`/`Translators`/`PostingFact`/Domain Events sin ningún otro cambio. Compatibilidad: las 3 suites de integración PostgreSQL ya existentes (`PostingEngineIntegrationTests`, `SalesInvoiceAuthorizedPostingIntegrationTests`, `PurchaseInvoiceConfirmedPostingIntegrationTests`) actualizaron su `SeedRuleAndPeriodAsync` para sembrar `Account`s reales + `PostingRuleLine`s balanceadas (antes sembraban solo `DebitAccountId`/`CreditAccountId` legacy, sin `Lines` — habrían producido asientos de 0 líneas, rechazados por el nuevo `JournalValidator`). Tests: 12 unitarios nuevos (`ERP.Application.Tests/Accounting/JournalFactoryTests.cs`, `JournalValidatorTests.cs` — ejercidos indirectamente vía `PostingEngine.PostAsync` con repositorios mockeados, ya que `JournalFactory`/`JournalValidator` son `internal` sin `InternalsVisibleTo`, sin precedente de ese patrón en el proyecto) + 2 de integración PostgreSQL real nuevos en `PostingEngineIntegrationTests.cs` (persistencia de `JournalEntry` con `JournalEntryLine`, recuperación completa del agregado con balance verificado). Riesgo documentado: "cuentas existentes"/"cuentas activas" no se validan en `JournalValidator` (fuera del alcance aprobado para esta fase) — hoy solo protegidas por la FK real de `JournalEntryLine.AccountId` a nivel de base de datos, que falla como `DbUpdateException` no como `Result` limpio. Regresión completa en verde: 278 `ERP.Domain.Tests`, 464 `ERP.Application.Tests` (452+12), 97 `ERP.Architecture.Tests`, 229 `ERP.Infrastructure.Tests` (20 en `Accounting/`).
- **P0-01 — Devolución de Venta (SalesReturn) + Nota de Crédito SRI: COMPLETED / CLOSED (2026-07-31)** — módulo cerrado formalmente de punta a punta, sin código productivo pendiente. Diseño: [`P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md`](../P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md). Plan de ejecución por fases (1-15, todas cerradas) y backlog técnico no bloqueante: [`P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`](../P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md). Activación de Nota de Crédito v1.1.0: [`docs/adr/ADR-031-credit-note-v1-activation.md`](adr/ADR-031-credit-note-v1-activation.md) (Accepted).
  - **Capacidades entregadas:** `SalesReturn`/`SalesReturnDetail`/`SalesReturnRefundAllocation` (Domain); devolución parcial y total sobre una `SalesInvoice` `Authorized`; ciclo Draft → Update → Cancel → Authorize; control de remanente devolvible bajo concurrencia real (advisory lock por factura + revalidación bajo lock, cierre de la ventana de condición de carrera que el chequeo preventivo del Draft no podía cerrar por sí solo); reversión de inventario (Kardex, `StockMovementType.SaleReturn`) al autorizar; reembolso explícito sin prorrateo automático — Efectivo / Crédito CxC / mixto (`SalesReturnRefundAllocation`, `Σ Amount == GrandTotal` como invariante de dominio); asiento contable automático vía `SalesReturnAuthorizedPostingTranslator` (mismo Posting Engine que Factura/Compra, ADR-026); Entity Audit (`SalesReturnAudit`, ADR-022); Nota de Crédito electrónica SRI V1.1.0 (XML, validación XSD, firma XAdES-BES, secuencial "04" vía `IDocumentSequenceRepository`, envío/autorización) activada por ADR-031; RIDE de Nota de Crédito; API REST documentada (`SalesReturnController`, `api/v1/sales/returns`); frontend completo (listado, formulario Draft/Authorize, sección de Nota de Crédito Electrónica); suite E2E de 23/23 escenarios contra PostgreSQL real (`SalesReturnEndToEndTests`).
  - **Mejora de infraestructura registrada junto con el cierre:** `DocumentSequenceRepository.CaptureNextAsync` corregido para participar de una transacción ambiente ya abierta por el caller (defecto real detectado durante el cierre de P0-01) — sin cambio de API pública ni de estrategia de locking de la infraestructura FROZEN de Secuencias Documentales (ADR-019).
  - **Pendiente operativo (no bloqueante para el cierre técnico):** prueba real de emisión de Nota de Crédito contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con certificado `.p12` configurado — no ejecutada en esta fase por no existir certificado de prueba disponible en este entorno (ver ADR-031, sección "Validación de la activación"). Mismo protocolo ya usado para cerrar ADR-023 con Factura (comprobantes reales, rechazo real confirmado) queda pendiente de repetirse para Nota de Crédito cuando haya certificado disponible.
  - **Backlog técnico no bloqueante** (detalle completo en la sección homónima de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`): wiring de React Hook Form + Zod en el formulario Draft de `SalesReturnFormPage`; unificación de `formatApiError`/`formatApiRequestError` en `SalesReturnCreditNoteSection`; evaluación de la ubicación REST de `GET .../returnable-lines`; consolidación de fixtures de test repetidas en `ERP.Application.Tests/Sales`; constante propia (no heredada de `SalesInvoice`) para la longitud de `CreditNoteDocumentNumber`. Ninguno bloquea el cierre — todos fueron evaluados y descartados de corrección inmediata en la auditoría de hardening previa por implicar refactor o riesgo de cambio de comportamiento fuera de ese alcance.

**Futuro (no implementado, fuera del ERP actual)**
- Plataforma externa — ver [`docs/future-platform/`](./future-platform/)

---

## FASE 1 — ERP Kernel Cleanup — COMPLETE 2026-06-05

> Branch `feat/platform-kernel-refactor`. Todos los componentes SaaS eliminados. Build: **0 errores**.
> Eliminado: Billing domain, Subscriptions domain, Platform entities, Commercial plans, Entitlements,
> SaaS controllers/middleware/jobs/services/behaviors. Tests SaaS eliminados. ERP puro compila limpio.
>
> **FASE 2 — Subscriber → Tenant rename: COMPLETADA (2026-07-23).** JWT claim (`tenant_id`), columna BD (`tenant_id`), DbContext (`ITenantScopedEntity`), frontend (componentes, i18n, navegación) y documentación normativa (AI-RULES/docs) consolidados en `Tenant`.
>
> Deuda cosmética conocida y no bloqueante:
> - nombres de variable/parámetro `subscriber` en código backend.
> - nombres históricos de índices SQL con `_subscriber_`.
>
> La columna física y el aislamiento real usan `tenant_id`. Esta deuda queda pendiente para una limpieza mecánica futura.

---

## ERP CORE FREEZE — GOVERNANCE LOCK ACTIVE (2026-06-08)

> **ERP Core está oficialmente congelado como producto independiente.** Acta completa, módulos incluidos/excluidos, frontera de integración (`/api/integration/v1/*`, [ADR-ERP-002](architecture/decisions/ADR-ERP-002-platform-separation.md)) y reglas obligatorias (*ERP never depends on Platform* / *Platform may consume ERP APIs only*) en [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md).

## ERP CORE BASELINE v1.0 — FROZEN 2026-06-05

> Architecture frozen. Changes to any module below require an Architecture Review before implementation.

| Module | Closed | Evidence |
|--------|:------:|----------|
| BusinessPartner V2 (Customer + Supplier roles) | ✅ | `docs/adr/ADR-017-business-partner-scope.md` |
| Customer Module | ✅ | BP V2 Customer closed 2026-06-04 |
| Supplier Module | ✅ | BP V2 Supplier closed 2026-06-04 |
| Company Isolation (ICompanyOperationalEntity + EF filters) | ✅ | `docs/security/MULTI-TENANT-HARDENING.md` |
| Security Hardening (CompanyScopeBehavior, namespaced fallback removed) | ✅ | Migration `20260605113654_AddCompanyIdToOperationalEntities` |
| Multi-Tenant Boundaries (all scopes explicit, fail-closed dual filter) | ✅ | `FINAL HARDENING REPORT 2026-06-05` — 0 CRITICAL/HIGH/MEDIUM/LOW issues |

**Test baseline at freeze:** ERP.Application.Tests 190/190 · ERP.API.Tests SecurityTests 33/33 · Build 0 errors.

---

## Documentation map (canonical — `AI-RULES/` + 7 files in `docs/` + índices)

| Topic | File |
|-------|------|
| **Agent rules (canonical)** | `AI-RULES/README.md` |
| Index | `CONTEXT.md` |
| Repo structure (2026-05) | `README.md`, `infrastructure/`, `scripts/`, `tools/` |
| Product summary | `README.md` |
| Agent adapters | `CLAUDE.md`, `.cursor/rules/` → `AI-RULES/*` |
| Delivery state | `docs/STATUS.md` (this file) |
| Priorities | `docs/ROADMAP.md` |
| Architecture | `docs/ARCHITECTURE.md` |
| Architecture rules (PR blocking) | `AI-RULES/PR-RULES-CATALOG.md` (entry: `docs/ARCHITECTURE-RULES.md`) |
| ADRs (architectural rationale) | `docs/adr/README.md` |
| Development + stack | `docs/DEVELOPMENT.md` |
| Identity + security | `docs/IDENTITY.md` |
| SaaS plans + billing (histórico) | `docs/archive/SAAS-COMMERCIAL.md` |
| Database | `docs/DATABASE.md` |

Consolidated 2026-05-21: former `MULTITENANCY`, `SCOPES`, `SECURITY`, `BILLING`, `DATABASE/*`, etc. merged into the files above. **2026-05-21:** `AI-RULES/` centralizes implementation rules for Cursor, Claude and future agents.

## Módulos FROZEN (arquitectura cerrada)

Los siguientes módulos tienen su arquitectura y modelo de datos cerrados definitivamente.
No se aceptan cambios estructurales sin una ADR aprobada.

| Módulo | Fecha cierre | ADR | Notas |
|--------|:------------:|-----|-------|
| **Business Partners V2** (Clientes / Proveedores) | 2026-06-05 | `docs/adr/ADR-017-business-partner-scope.md` | subscriber-scoped, Roles (Customer/Supplier), CompanySettings, LegalRepresentativeName, unique index DB |
| **Customer Module** | 2026-06-05 | BP V2 ADR | FROZEN + FREEZE GATE PASS (2026-06-17); 5 ARs, 31+ endpoints, 20 domain events, 38 [Authorize]; UI completa: listado + wizard + detalle + ubicaciones CRUD + contactos CRUD + roles + trading settings; RUC/CI SRI; consumidores: Sales, Quotations, Orders, E-Invoicing, CRM, AR |
| **Supplier Module** | 2026-06-05 | BP V2 ADR | Fiscal + classification, full FROZEN |
| **Company Isolation** | 2026-06-05 | Security Hardening Report | ICompanyOperationalEntity, fail-closed EF filters, PaymentApplication, ArAp/AccountingPeriod scopes |
| **Security Hardening** | 2026-06-05 | Security Hardening Report | CompanyScopeBehavior explicit only, 0 namespace fallback, all APIs fail-closed |
| **Multi-Tenant Boundaries** | 2026-06-05 | Security Hardening Report | 223/223 tests, migration 20260605120243_FinalHardening |
| **SaaS Commercial Flow** | 2026-05-28 | `docs/archive/historical-decisions/SAAS-FREEZE.md` | Plans, Entitlements, Subscription lifecycle |
| **Sucursales** | 2026-06-16 | — | Entidad organizativa (no fiscal); CRUD + soft-disable; ruta `/settings/branches` |
| **Establecimientos SRI** | 2026-06-16 | — | Código SRI único por empresa; BranchId opcional; disable bloqueado si tiene PEs activos; ruta `/settings/establishments` |
| **Puntos de Emisión** | 2026-06-16 | — | Código único por Establecimiento; DocumentSequence automático; ruta `/settings/emission-points` |
| **Items / Catálogo v1.0** | 2026-06-17 | — | 14 entidades, 56 endpoints, 20 validators; tenant-scoped catalog compartido entre companies; 6 catálogos CRUD (Brand, Family, Category, Subcategory, AttributeGroup, AttributeDefinition); Detail page con Variants, Images, Conversions, Substitutes, Packaging; SRI lookups (UOM, VAT, ICE); listo para Inventario, Compras, Ventas, Facturación Electrónica |
| **Sales Invoice + Detail** | 2026-06-24 | — | Aggregate root SalesInvoice + SalesInvoiceDetail; lifecycle Draft→Authorized→Cancelled; freeze contract irreversible (IsFrozen + EnsureDraft); snapshot fiscal (VAT/ICE rates + amounts + names); computed totals no persistidos (LineSubtotal, TaxableBase, TaxInclusiveTotal); AuthorizedSubtotal/GrandTotal congelados al autorizar; ReplaceLines único mutator; DocumentSequence SRI; facturación electrónica (AccessKey, AuthorizationNumber); frontend preview-only (salesCalc.ts); 4 use cases (Draft CRUD, Authorize, Discount, Cancel); FluentValidation; company-scoped + tenant-scoped |
| **Payment Methods + Formas de Cobro** | 2026-06-24 | — | PaymentMethod catálogo dinámico (CRUD+Toggle, multi-tenant, seed 5 métodos). SalesInvoicePayment entidad hija (N pagos por factura, snapshot Code+Name, Amount>0, Reference condicional). Authorize() valida ≥1 pago + Sum==GrandTotal. Sin enums, sin JSONB, sin auto-default. Base definitiva para CxC/Cobros/Caja/Contabilidad |
| **Sales Receivable (CxC deuda)** | 2026-06-25 | — | SalesReceivable + SalesReceivableInstallment. Solo crédito (CreditTermDays>0 o Installments>1). PaidAmount=0 (sin cobros). Cancel cascada desde factura. 2 tablas, 6 índices, 2 endpoints GET. Módulo pasivo: registra deuda, no cobra |
| **Estándar de Precisión Numérica** | 2026-06-25 | — | 73/73 columnas auditadas, 100% compliance, sin desviaciones activas. Frontend alineado (ZhDecimalInput, sanitizeDecimal, getDecimalConfig por empresa). Backend alineado (decimal puro, InvariantCulture, 0 parsers culturales). PostgreSQL alineado: montos numeric(18,2), cantidades numeric(18,4), precios numeric(18,6), porcentajes numeric(5,2). Cambios futuros: solo mediante revisión arquitectónica formal. Gate: toda nueva columna decimal requiere justificación tipo/precisión/escala/motivo |
| **Estándar de Fechas y Horas** | 2026-06-25 | — | Formato dd/MM/yyyy obligatorio via formatDate/formatDateTime/formatDateTimeSeconds (dateFormatters.ts). UTC getUTC*() en frontend — sin desfase por timezone. Backend DateTime.UtcNow + timestamptz PostgreSQL. Eliminados todos los toLocaleDateString/toLocaleString de fechas financieras. DateOnly para fechas sin hora. ISO 8601 en API |
| **Infraestructura de Mensajes Visuales** | 2026-06-29 | `docs/adr/ADR-018-message-infrastructure.md` | API pública `message.*` congelada. Store interno encapsulado. Cola FIFO + deduplicación. 22 tests. ESLint gate activo. |
| **Infraestructura de Secuencias Documentales** | 2026-06-29 | `docs/adr/ADR-019-document-sequence-infrastructure.md` | `DocumentSequence` + `CaptureNextAsync()` como única API autorizada. advisory lock + transacción ReadCommitted. `UNIQUE(tenant_id,company_id,emission_point_id,doc_type_code)` + `CHECK(current_seq>=1)`. Guard de dominio. 4 gates CI-bloqueantes. Suite concurrente: 8/8 tests passing con PostgreSQL 16 real (500 req simultáneas, 0 duplicados). |
| **Infraestructura de Entity Tracking (EF Core Change Tracking)** | 2026-06-30 | `docs/adr/ADR-020-entity-tracking-infrastructure.md` | `NewChildEntityTrackingInterceptor` (`ISaveChangesInterceptor`) corrige hijos nuevos mal clasificados como `Modified` al ser descubiertos por fixup de navegación sobre un agregado ya trackeado. Señal `ErpDbContext.WasTrackedFromQuery` vía `ChangeTracker.Tracked`. Fail-fast (`InvalidOperationException`) ante combinación anómala — no autocorrige por adivinanza. Regla permanente: ningún agregado se reatacha vía `Attach()`/`Update()` sin pasar antes por una query del mismo `DbContext`. `ATT-GATE-01` gate CI-bloqueante (lista blanca cerrada de 3 repositorios de catálogo). 6/6 tests de integración passing con PostgreSQL 16 real (Testcontainers). Validado end-to-end vía API (Caja + Sales). |
| **Infraestructura de Valores por Defecto de Facturación** | 2026-07-01 | `CLAUDE.md#infraestructura-closed--valores-por-defecto-de-facturación-inmutable` | Configura los 5 parámetros por defecto de la factura de venta (`DefaultDocTypeCode`, `DefaultSriPaymentMethodCode`, `DefaultEmissionPointId`, `DefaultWarehouseId`, `DefaultPaymentTermId`) almacenados en `SriSettings`. `CreateForDefaults()` habilita configurar defaults antes de completar la config de FE. `UpdateInvoiceDefaults()` método de dominio único para mutar los 5 campos. `PUT /api/v1/electronic-invoicing/sales-defaults` (permiso `Configure`). Nueva pestaña "Valores por Defecto" en Company Settings Hub. Todos los campos opcionales (`"— Sin configurar —"`). Catálogos cargados en paralelo vía `useAsync`. Sin nuevas entidades, sin nuevas tablas. Cumple F-V1..F-V8 + B-V1..B-V5. |
| **Infraestructura Tributaria (Tax Infrastructure)** | 2026-07-01 | `CLAUDE.md#infraestructura-closed--configuración-tributaria-inmutable` | 5 reglas permanentes: (1) toda configuración tributaria pertenece al ítem, no al documento; (2) los documentos transaccionales solo consumen — nunca generan — impuestos; (3) ítem sin config tributaria = error de configuración, nunca fallback; (4) motor único de cálculo vía `ISriTaxResolver` (backend) + `sriLookupService.*Rates()` (frontend); (5) códigos tributarios solo desde catálogos oficiales `sri_vat_rates`/`sri_ice_rates`. Prohibido: `vatCode ?? '10'`, `purchaseVatCode` como fallback en venta, `DefaultVatCode`, `DefaultIceCode`, listas tributarias hardcodeadas. IVA base correcta = `net + ice` (normativa SRI Ecuador). Preview frontend alineado con cálculo backend. |
| **Tipos de Ítem (Item Types)** | 2026-07-04 | `CLAUDE.md#infraestructura-closed--tipos-de-ítem-item-types-inmutable` | `ItemTypeDefinition` catálogo tenant-editable (`Code`, `Name`, `SortOrder`, `IsActive`), reemplaza el enum fijo `Physical/Service/Digital/Kit/Bundle`. `items.item_type_id (uuid)` FK física a `item_types.id` (nunca código ni nombre como relación). CRUD completo `api/v1/item-types` (`ItemTypesController`), UI de administración `ItemTypesPage.tsx` (`/inventory/item-types`, patrón `ConfigTabsLayout`, sin modales). Hook único `useItemTypeOptions()` (con caché de módulo) consumido por formulario de Items, listado de Items y buscador de Compras — una sola fuente de verdad, sin fetch duplicado. Sin flags de comportamiento (clasificación pura, no controla inventario/venta). Seed de 5 tipos por defecto en onboarding de tenant nuevo (`CompanyBootstrapService`). |
| **Items Administration** | 2026-07-07 | — | Item CRUD (14 entidades hijas: variantes, códigos de proveedor, barcodes, imágenes, conversiones, sustitutos, packaging), pricing base (`Item.BaseSalePrice` SSOT), catálogo de Tipos de Ítem tenant-editable, `ItemAudit` (Entity Audit) sobre `ItemCreatedEvent`/`ItemUpdatedEvent`/`ItemPriceChangedEvent`/`ItemEnabledEvent`/`ItemDisabledEvent`. Deuda técnica documentada (no bloqueante): `ItemVariantAddedEvent`/`ItemVariantDisabledEvent` no implementan `IAuditEvent` — cubrirlos requiere modificar las clases de evento, decisión explícita futura |
| **Pricing Administration** | 2026-07-07 | — | `PriceList` (contenedor + regla general opcional), `PriceListItem` (asignación administrativa ítem↔lista, sin reglas ni precios), `PricingRule` (excepción por ítem, override de la regla general). `PricingResolver`/`PricingCalculation` como única API de resolución de precio neto. Auditoría de dominio completa vía Domain Events: `PriceListAudit` (creación/actualización/activación/desactivación), `PriceListItemAudit` (asignación/activación/desactivación), `PricingRuleAudit` (creación/actualización/activación/desactivación, con old/new tipados). Invariante `PricingRule` requiere `PriceListItem` activa (validado en `SetPricingRuleHandler`/`EnablePricingRuleHandler`) — no existen reglas huérfanas. Pricing no calcula impuestos (frontera con `ISriTaxResolver`/`sriLookupService`). Pricing no soporta `ItemVariantId` (retirado deliberadamente 2026-07-07, ver `PricingRule.cs`). Endpoint legacy `/api/v1/pricing/item-prices` queda explícitamente fuera de este freeze — pendiente del cierre de Compras |
| **Infraestructura de Auditoría por Dominio (Entity Audit)** | 2026-07-07 | `docs/adr/ADR-022-audit-infrastructure-entity-vs-process.md` | Contratos comunes `AuditRecordBase`/`AuditActor`/`AuditSource`/`IAuditEvent` (Domain) + `IAuditWriter<T>`/`IAuditReader<T>`/`IAuditContext`/`IAuditService` (Application) + `EfAuditWriter<T>`/`EfAuditReader<T>`/`HttpAuditContext`/`AuditService` genéricos (Infrastructure, open-generic en DI). Dispatcher reutiliza domain events + Outbox ya FROZEN (ADR-007/008). Pilotos: `PricingRuleAudit`, `PriceListItemAudit`, `PriceListAudit` (tablas `pricing_rule_audit`, `price_list_item_audit`, `price_list_audit`). Cada dominio nuevo agrega solo su entidad + eventos + handler, sin tocar la infraestructura común. `UserActivity` queda reservada al feed liviano, no a auditoría de negocio tipada. Process Audit (auditoría de procesos sin `EntityId` único — recálculos masivos, cierres, ETL, jobs) queda diseñado y documentado en `AI-RULES/AUDIT-INFRASTRUCTURE.md`, sin implementar: reutilizará el `EntityId` como `ProcessRunId` sintético, sin modificar ningún contrato FROZEN. `UserName` resuelto 2026-07-07: snapshot histórico obligatorio en `AuditActor` (no-nullable, fallback `"Unknown"`), poblado desde claims JWT (`ClaimTypes.Email`/`ClaimTypes.Name`) embebidas al emitir el token en `AccessTokenService` — no de una consulta en vivo. Corregido el mismo día un error de claim (`GivenName` representa solo el nombre, no el nombre completo; se corrigió a `ClaimTypes.Name`, con fallback transitorio de compatibilidad en `CurrentUserService`). `AuditActor` confirmado como único modelo oficial del actor (ampliado additive con `FullName`/`Email`/`RoleName` opcionales) — regla Open/Closed nueva: prohibido agregar columnas de identidad del usuario en las entidades de auditoría de cada dominio. Columna `user_name` migrada a `NOT NULL` (`MakeAuditUserNameRequired`). Deuda técnica restante (no bloquea el freeze del contrato): `Source` hardcodeado a `UserAction` en `HttpAuditContext` (falta contexto para jobs/sistema), `CorrelationId`/`RequestId` sin truncado antes de persistir en `varchar(100)`. |
| **ElectronicDocuments v1.0 (Facturación Electrónica SRI)** — **CIERRE OFICIAL** | 2026-07-11 | `docs/adr/ADR-023-electronic-documents-v1-closure.md` | Núcleo FROZEN: generación XML, validación XSD, firma XAdES-BES, recepción/autorización SRI (esquema offline), reintentos con backoff (`ElectronicDocumentRetryPolicy`, 5 intentos), Monitor de consulta. Cerrado tras 3 rondas: auditoría de robustez (2 críticos + 3 altos corregidos con evidencia/reproducción — TIMEOUT deadletering prematuro, pipeline sin try/catch, Hangfire sin guard de concurrencia, IDOR Company Scope en retry, 503→409 en carrera de registro), cumplimiento del Anexo Técnico SRI verificado texto por texto contra el PDF oficial (clave de acceso módulo 11 reproducido bit a bit, catálogo `sri_error_code` reescrito con 33 códigos reales), y pruebas reales contra `celcer.sri.gob.ec` (8 comprobantes reales, incluido un rechazo real confirmado con código `[65]`). **Addendum RESP-01 (2026-07-11, causa 2 — bug demostrado)**: reenvío de Recepción ahora trata también los códigos `[43]`/`[45]` (no solo `[70]`) como "ya existe, consultar autorización" en vez de rechazo automático — 2 tests de regresión agregados, ningún contrato modificado. Solo `Invoice` tiene builder/provider/validador activo — CreditNote/DebitNote/ShippingGuide/Retention/PurchaseSettlement tienen XSD/catálogo pero sin implementación (`activeVersion: null`), documentado como límite explícito. Deuda técnica aceptada y no bloqueante (ver ADR-023, sección "Cierre oficial"): búsqueda del Monitor acoplada a Sales, contraseñas de certificado legacy en texto plano, `AVG` en memoria, `GetRetryCandidatesAsync` sin paginación. Cambios futuros al núcleo solo por: cambio obligatorio SRI, bug demostrado, vulnerabilidad de seguridad, o rendimiento crítico. |
| **Infraestructura de Diagnóstico SRI reutilizable** | 2026-07-11 | `docs/adr/ADR-024-electronic-document-diagnostic-infrastructure.md` | Extensión aditiva y controlada de ADR-023 (causa 1: campo real de la Ficha Técnica, `<mensaje>/<tipo>`, descartado silenciosamente). `SriMessage` (Domain value object) capturado por `SriSoapClient` en paralelo al texto aplanado existente — corrigió en el camino un bug real de parsing (mensaje fantasma por reutilización del tag `<mensaje>` en el esquema SRI). Solo `ElectronicDocument.MarkRejected` gana un parámetro opcional; `MarkFailed`/`MarkDeadLetter` sin cambios. Segundo suscriptor de `ElectronicDocumentRejectedEvent` (`ElectronicDocumentSriMessageAuditHandler`, tabla nueva `electronic_document_sri_message`) — mismo patrón `PricingRuleAudit`/`ElectronicDocumentAudit`, sin tocar `IAuditReader<T>`/`IAuditWriter<T>` genéricos. `ElectronicDocumentDiagnosticDto` único contrato reutilizable (retira `ElectronicDocumentErrorInfoDto`), ensamblado por `ElectronicDocumentDiagnosticAssembler` y consumido por Monitor, el reintento manual (cierra un bug real de contrato: `RetryElectronicDocumentCommandHandler` devolvía `ElectronicDocumentDto` en vez del detalle completo) y el nuevo `GET /api/v1/electronic-documents/by-source` agnóstico de módulo. Frontend: `ElectronicDocumentDiagnosticPanel` (`components/zh/electronicDocuments/`) integrado en Monitor y en Ventas (`SalesElectronicDiagnosticDrawer`, segundo consumidor real). Retenciones/Notas/Guías quedan explícitamente fuera (sin emisión activa, ver límites de ADR-023). |
| **Recepción XML de Compras → Compra** — **CIERRE OFICIAL** | 2026-07-28 | `docs/adr/ADR-028-purchase-reception-to-purchase-flow-freeze.md` | Flujo congelado: Recepción XML → Descargar XML → Crear Compra → Formulario precargado → Guardar Compra. `PurchaseReceptionDocument.XmlContent` es evidencia fiscal inmutable; `PurchaseReceptionLine` es el único snapshot operativo (nunca se elimina una línea por ausencia de Item o fallo de matching); `IPurchaseReceptionDetailProcessor` es la única interpretación de XML→snapshot+Item Matching, reutilizada por la descarga inicial y por la reconstrucción transparente e interna de `CreatePurchaseReceptionDraftHandler` (dispara solo si `ProcessingStatus.Failed`, persiste de inmediato, nunca reconstruye dos veces — verificado por tests dedicados). Un único botón "Crear Compra", sin endpoints ni acciones de "reprocesar" expuestos al usuario. Deuda aceptada y documentada (no bloqueante, ver ADR-028 "Consecuencias"/"Riesgos"): `PurchaseReceptionDocument.MarkProcessed(...)` existe pero no tiene invocador real — `CreatePurchaseDraftCommand` (creación de `PurchaseInvoice`) no recibe todavía un `PurchaseReceptionDocumentId`. Evolución futura (workflow de aprobación de Compras, no implementado) documentada en `docs/adr/ADR-029-purchase-approval-workflow-future-evolution.md`. |

### Items Administration
Estado: FROZEN

Contrato cerrado:
- Item master data
- Item pricing base
- Item child entities
- Item audit

### Pricing Administration
Estado: FROZEN

Contrato cerrado:
- Price Lists
- Price List assignments
- Pricing Rules
- Pricing resolution rules
- Pricing audit

Restricciones:
- Pricing no calcula impuestos.
- Pricing no soporta ItemVariantId.
- PricingRule requiere PriceListItem activo.
- Auditoría mediante Domain Events.

### Items — PVP fix (2026-06-24)

Fix de actualización de PVP en formulario de edición de ítems:
- Schema de validación correcto (`updateItemSchema` sin `sku`) al editar
- Precio se carga desde `itemPriceService.list()` al abrir edición
- Precio se persiste via `itemPriceService.setInitial()` al guardar

### Compras — Auditoría UX + SSOT (2026-06-24)

Auditoría completa del formulario de Compras. Build: **0 errores frontend + backend**. Tests: **47/47 PASS**.

| Mejora | Detalle |
|--------|---------|
| Código muerto eliminado | `ItemContextPanel`, `creditDays`, `profileLoading`, `expandedLines`/`toggleExpand` (−184 líneas neto) |
| Duplicidad visual eliminada | SKU en select bodega, nombre producto en panel contexto |
| Descuento por línea | Input editable 0-100% (backend ya lo soportaba, UI no lo exponía) |
| Cálculo local IVA/ICE | Estimación en borrador nuevo usando `ctx.vatPercent`/`ctx.icePercent` — elimina totales engañosos $0 |
| Alerta costo fuera de rango | Warning visual cuando costo difiere >20% del promedio SSOT |
| Selector condición de pago | Backend: `Guid? PaymentTermId` opcional en commands (backwards compatible). Frontend: select en cabecera con regeneración automática de cuotas |
| Secciones colapsables | Info Electrónica y Observaciones colapsables, auto-expand si tienen datos |
| Lógica extraída + testeable | `purchaseCalc.ts` con funciones puras; 27 tests unitarios (Vitest) |
| Import huérfano eliminado | `UpdatePurchasePayload` |
| CSS huérfano eliminado | `.pdl-line__disc-badge*`, `.pf-mini-card--obs` |

---

## Architecture (current)

| Area | State |
|------|--------|
| Modular monolith (Clean + CQRS) | ✅ |
| EF baseline `20260606040144_ErpBaselineClean` | ✅ |
| Tenant / Company / Membership model (`SubscriberId → TenantId` consolidado FASE 4) | ✅ |
| `CompanyScopeBehavior` (pipeline MediatR) | ✅ |
| Wave 1 `company_id` (inventory core) | ✅ (in baseline) |
| PostgreSQL RLS (enterprise tables) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Architecture guardrails CI (scripts + NetArchTest) | ✅ (2026-05-21) |
| **Frontend architecture checks (Node ESM)** | ✅ 12/12, score 100/100 (2026-05-24) — controllers backend ≤150 líneas |
| **Architecture governance v2** (ADRs, backend Node checks, score, PR annotations) | ✅ (2026-05-21) |
| Architecture baseline v1.0 remediation (lint, E2E smoke, legacy platform controller, SYSTEM_TRUTH) | ✅ (2026-05-21) |
| Post-audit remediation (session SEC, Sales unify, Kardex CQRS, Cash validators) | ✅ (2026-05-21) |
| Post-audit wave 2 (menu builder split, services→modules, access/security pages) | ✅ (2026-05-21) |
| Post-audit wave 3 (menu builder modular split, test sessionStorage) | ✅ (2026-05-21) |
| Enterprise monorepo root (`infrastructure/`, `scripts/`, `tools/`, docs stubs) | ✅ (2026-05-21) |
| Post-reorg stabilization (paths, CI green, company-scoped inventory movements) | ✅ (2026-05-21) |
| Post-audit P2 + wave 4 (services eliminados, AppLayout/Companies split) | ✅ (2026-05-21) |
| Post-audit wave 5 (PR-7 TSX: catálogo, clientes, contabilidad, menu builder, platform shell) | ✅ (2026-05-21) |
| Post-audit wave 6 (handlers C-03, lazy routes, grandfather vacío) | ✅ (2026-05-21) |
| **AI-RULES multi-agent governance** (`AI-RULES/*` canonical; `CLAUDE.md` + `.mdc` adapters) | ✅ (2026-05-21) |

Details: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE.md](./DATABASE.md).

### Post-audit remediation (2026-05-21)

| Item | Estado |
|------|--------|
| Frontend: tokens en memoria + perfil/bootstrap/permisos en `sessionStorage`; `SessionBootstrap` + cookie refresh | ✅ |
| Backend: `ERP.Application/Sales` consolidado bajo `Modules/Sales` + validators Notas/Retenciones | ✅ |
| Backend: `EnqueueKardexReportCommand` (controller sin `SaveChangesAsync`) | ✅ |
| Backend: validators Cash (caja/bancos/conciliación) | ✅ |
| Pendiente PR-7 TSX >500 | ✅ (grandfather `tsxMaxLines500` vacío 2026-05-21) |

### Post-audit wave 5 (2026-05-21)

| Item | Estado |
|------|--------|
| `MenuBuilder` + `NavigationMenuEditorPanel` modularizados (controller + subpaneles) | ✅ |
| `PlatformPanelPage` + `PlatformPlansSection` en hook + tabs/modales | ✅ |
| `AccountingPage`, `BranchesPage`, `CustomersPage`, `SriConfigPage`, `BodegasPage` | ✅ |
| `CatalogPages`, `CatalogStructurePage`, categorías/subcategorías | ✅ |
| `architecture-grandfather.json`: `tsxMaxLines500` vacío | ✅ (`tools/architecture/`) |

### Post-audit wave 6 (2026-05-21)

| Item | Estado |
|------|--------|
| Handlers C-03: `CrearVenta`, `CreateProduct`, `UpdateProduct`, `EmitirFactura`, `EnviarNotaSri` (Handle ≤150) | ✅ |
| `ProductCommandMutationHelper` compartido create/update | ✅ |
| Rutas lazy: `accessRoutes`, `companiesRoutes`, `companyManagementRoutes`, `publicRoutes`, `mainRoutes` (placeholder) | ✅ |
| Grandfather vacío (`handlerHandleMaxLines150`, `tsxMaxLines500`, `tsxPageWrapperMaxLines15`) | ✅ |
| Chunk `index-*.js` ~362 KB (límite 650 KB) | ✅ |

### Post-audit P2 (2026-05-21)

| Item | Estado |
|------|--------|
| Carpeta `frontend/src/services/` eliminada (cero consumidores; API solo en `modules/*/api`) | ✅ |
| `SalesReportPage` → `modules/reportes/pages/` + wrapper 1 línea | ✅ |
| Placeholders → `modules/shared/pages/` + wrappers delgados | ✅ |
| `components/ui` sustituido por ZH en company-management, access, security, companies | ✅ |

### Post-audit wave 4 (2026-05-21)

| Item | Estado |
|------|--------|
| `AppLayout.tsx` (~634 → ~216) + `AppLayoutMainMenu`, `useAppLayoutNavigation`, banner | ✅ |
| `CompaniesPage.tsx` (~820 → ~252) + `useCompaniesPage`, `CompaniesPageDataTab` | ✅ |
| Grandfather: retirados `AppLayout`, `CompaniesPage`, `SalesReportPage` | ✅ |

### Post-audit wave 3 (2026-05-21)

| Item | Estado |
|------|--------|
| `usePlatformGateMenuBuilder` (~844 → ~371 líneas) + effects/actions/persist extraídos | ✅ |
| `PlatformMenuBuilderCrmWorkspace` (~934 → ~259 líneas) + panels/preview/audit/modals | ✅ |
| Test `syncSessionEntitlements` con stub `sessionStorage`/`localStorage` | ✅ |
| Grandfather: `PlatformMenuBuilderCrmWorkspace` retirado de PR-7 | ✅ |

### Post-audit wave 2 (2026-05-21)

| Item | Estado |
|------|--------|
| `PlatformMenuBuilderSection` dividido en entry + hook + CRM/legacy panels | ✅ |
| Imports `services/` → `modules/*/api` (cero consumidores directos en `src/`) | ✅ |
| `ProfilesPage`, `SubscriberAccessPage`, `SecuritySettingsPage` en `modules/` + wrappers delgados | ✅ |
| Re-exports `@deprecated` en `frontend/src/services/` para compatibilidad | ✅ (carpeta eliminada 2026-05-21) |
| Grandfather JSON actualizado (CRM workspace, sin legacy service imports) | ✅ |

## SaaS platform y ERP backend (snapshot histórico — pre FASE 1)

> ⚠️ **Snapshot pre-refactor (2026-05-23/24).** Las dos tablas siguientes describen el estado **anterior** a "FASE 1 — ERP Kernel Cleanup" (2026-06-05, ver banner al inicio de este documento), que eliminó por completo Billing domain, Subscriptions domain, Platform entities, Commercial plans y Entitlements, y a "FASE 4" (consolidación `SubscriberId → TenantId` + BP V2). Items como *Billing governance*, *Entitlements snapshot*, *Commercial limits*, *Sales/Accounting/Cash* descritos abajo **ya no existen** como módulos activos del backend — ver el inventario real de módulos en [`docs/ARCHITECTURE.md`](./ARCHITECTURE.md#bounded-contexts) y el estado vigente en "ERP CORE BASELINE v1.0" arriba. Se conservan como registro histórico de delivery, no como estado actual.

| Component (histórico) | Status (al 2026-05-23) |
|-----------|--------|
| Subscribers / plans / features | ✅ |
| Platform UI naming + API JSON aliases + middleware rename | ✅ (2026-05-23) |
| Subscriber ficha unificada + impersonación con retorno | ✅ (2026-05-23) |
| Company management API + UI (`/companies`) | ✅ |
| Switch company + JWT claims | ✅ |
| Commercial limits (companies, users, branches, warehouses) | ✅ |
| Entitlements snapshot API | ✅ |
| Billing governance + API | ✅ backend |
| Billing UI | ⏳ not built |
| Stripe / real payment provider | ⏳ `NullPaymentProviderAdapter` |

| Module (histórico) | Status (al 2026-05-24) |
|--------|--------|
| **Business Partners (Clientes/Proveedores) — FROZEN** | ✅ FROZEN 2026-06-02 — ver `docs/adr/ADR-017-business-partner-scope.md` (sigue vigente como BP V2) |
| Products, catalogs, customers, suppliers | ✅ |
| Inventory, transfers, adjustments, kardex | ✅ |
| Purchases (OC, bills, expenses) | ✅ (UX/SSOT audit 2026-06-24) |
| Sales + electronic invoice (SRI code) | ✅ code / 🟡 real SRI validation pending |
| **Sales commercial pipeline** (quote → order → invoice, `DocumentRelation`) | ✅ API + UI + E2E (2026-05-24) |
| Accounting, cash | ✅ |
| Retenciones / guía remisión | 🟡 partial / placeholder UI |

### Backend architecture hardening (audit 2026-05-21)

| Item | Status |
|------|--------|
| SRI post-auth atomic transactions (`IUnitOfWork` ambient + journal entry nested) | ✅ |
| `SriSettings.CertPassword` encrypted at rest (Data Protection, legacy plaintext fallback) | ✅ |
| `Company` → `ISubscriberScopedEntity` + global EF subscriber filter | ✅ |
| `AccountingService` orchestration in Application layer | ✅ |
| API DbContext leakage → CQRS (`GetAppFeatureTree`, `ListPendingSriRetry`, `IAppFeatureRepository`) | ✅ |

## Frontend

| Area | Status |
|------|--------|
| Auth, subscriber select, company select | ✅ |
| Core ERP modules (sales, purchases, inventory, settings) | ✅ |
| **Ventas pipeline UI** (`/sales/quotes`, `/sales/orders`, `/sales/invoices`, credit notes) | ✅ (2026-05-24) |
| **`fullLogout()` centralizado** (stores + localStorage + `erp.saas.*`) | ✅ |
| **Products/customers — fuente única en `modules/*`** (`apiEnvelope`, adapters `@deprecated`) | ✅ |
| **Consolidación modular P3** (auth, branches, accounting, dashboard, platform API + pages) | ✅ |
| **Catálogo + bodegas + auth UI** en `modules/catalog`, `modules/inventario/warehouses`, `modules/auth/pages` | ✅ |
| **Lazy routes P4** (`routes/lazyPage.tsx`, main/catalog/platform split) | ✅ |
| **Platform naming cleanup** (`/platform/*`, `platformAuth.ts`, sin `isPlatformOperator`) | ✅ (2026-05-23) |
| **ZH UI estándar** (`components/ui` delega clases ZH; catálogo usa `ZHCard`/`ZHSearchBar`) | ✅ |
| Company management module | ✅ |
| SaaS billing pages | ⏳ |
| Kardex / stock dedicated UI | ⏳ placeholder routes |
| Legacy `tenant` i18n aliases | 🟡 rename deferred |

## PostgreSQL

| Item | Status |
|------|--------|
| Schema from single baseline | ✅ |
| Naming `_subscriber_` on indexes/FK | ✅ |
| RLS enabled (inventory, sales core) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Session vars via interceptor | ✅ |
| Company scope on operational entities | ✅ (baseline + query filters) |

## Security

| Item | Status |
|------|--------|
| JWT + refresh rotation (FamilyId, grace configurable, revocación por familia, rate limit IP/user/family, audit logs) | ✅ |
| Multi-tab SPA (Web Locks + BroadcastChannel + bootstrap retry) | ✅ |
| Permission policies | ✅ |
| Company isolation (app layer) | ✅ |
| SRI certificate password encryption (Data Protection) | ✅ |
| RLS (DB layer) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Platform operator bypass (JWT global) | ✅ controlled |
| Permissions cache in handler hot path | ⏳ service exists, wiring partial |
| SPA session cleanup (`fullLogout`) | ✅ frontend |

## Cache

| Cache | Status |
|-------|--------|
| Entitlements snapshot (Redis-ready) | ✅ |
| Permissions (distributed impl) | ✅ registered |
| Dedicated `commercial-limits:{id}` cache | ⏳ optional future |

## Tests

| Project | Status (2026-05-21) |
|---------|---------------------|
| `ERP.Infrastructure.Tests` (limits/entitlements + optional Postgres unified-doc) | ✅ 23/23 |
| `ERP.Domain.Tests` | ✅ 24/24 |
| `ERP.Application.Tests` | ✅ 190/190 (2026-06-05) |
| `ERP.API.Tests` | ✅ 33/33 SecurityTests (2026-06-05); integration suite stable |
| `ERP.Architecture.Tests` (NetArchTest + controller guardrails) | ✅ 30/32 — 2 pre-existing failures (Items module permissions pending plan catalog registration) |
| Frontend ESLint (`npm run lint`) | ✅ 0 errors (2026-05-21 remediation) |
| Frontend Vitest | ✅ 47/47 (27 purchase calc tests added 2026-06-24) |
| Frontend build | ✅ |
| Playwright smoke | ✅ PASS |
| Playwright enterprise E2E | 🟡 requiere API local; skip controlado sin backend |

### Sales commercial pipeline greenfield (2026-05-24)

| Item | Estado |
|------|--------|
| API: quotes (list/detail/create/approve/cancel), orders (list/detail/create/confirm/cancel/invoice) | ✅ |
| API: invoices (list/detail/validar/emitir/reintentar/anular) + permisos `sales.invoices.*` | ✅ |
| API: `DocumentRelation` (`QUOTE_TO_ORDER`, `ORDER_TO_INVOICE`) en detalle | ✅ |
| UI: `/sales/quotes`, `/sales/orders`, `/sales/invoices` + legacy redirects | ✅ |
| UI: trazabilidad cotización↔pedido↔factura; factura directa walk-in | ✅ |
| UI: filtros servidor en listado facturas; permiso `sales.credit-notes.send` | ✅ |
| E2E: `SalesCommercialPipelineEndToEndTests`, `SalesOrderInvoiceEndToEndTests`, `SalesCommercialCancelEndToEndTests` | ✅ |
| Tenants con perfil Facturador anterior al seed | 🟡 re-seed o migración manual de permisos `sales.quotes.*`, `sales.orders.*` |

Flujo canónico: **Cotización → Aprobar → Pedido → Confirmar → Factura → Validar/Emitir SRI**.

## MVP commercial (~85–90%)

**Done:** Core ERP operational flows, platform control plane, plans, multi-company foundation.

**Blocking / high priority:**

1. Validate SRI in `celcer.sri.gob.ec` with test certificate
2. Billing + retenciones UI gaps
3. Playwright enterprise E2E con API en CI (smoke ya verde)

See [ROADMAP.md](./ROADMAP.md) for prioritized backlog.

### Enterprise hardening — MasterData + security (2026-05-23)

| Item | Estado |
|------|--------|
| Explicit scope markers (`ICompanyScopedRequest` / CI AR-SEC-4) | ✅ |
| PostgreSQL unique violation → `Result.Conflict` (409) | ✅ |
| Testcontainers concurrency tests | ✅ (`Category=PostgreSql`) |
| Security metrics wired (refresh, 403, dual-write, namespace fallback) | ✅ |
| MasterData reconciliation (READ-ONLY) + health + Hangfire job | ✅ |
| SRI foundation (`SupplierProfile` retention defaults) | ✅ |
| Docs: [security/MULTI-TENANT-HARDENING.md](./security/MULTI-TENANT-HARDENING.md), [observability/METRICS.md](./observability/METRICS.md) | ✅ |

## Risks

| Risk | Mitigation |
|------|------------|
| Cross-company data leak | `CompanyScopeBehavior` + EF query filters |
| Production migration from old chain | Use baseline + planned data migration — never `DROP SCHEMA` in prod |
| Billing suspend without UI visibility | Entitlements snapshot exposes status; build `/saas/billing` |
| Test drift | Fix controller/DTO names before release gate |

## Quick start

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
```

First-run admin: banner en consola al arrancar API (`GET /api/setup/status` + `POST /api/setup/admin`, token-gated).

## Related

- [ROADMAP.md](./ROADMAP.md) — what’s next
- [DEVELOPMENT.md](./DEVELOPMENT.md) — how to contribute safely
