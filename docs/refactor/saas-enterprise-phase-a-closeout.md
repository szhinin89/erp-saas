# SaaS Enterprise Refactor — Fase A: cierre

**Branch:** `refactor/saas-enterprise-06-phase-a-closeout`  
**Fecha:** 2026-05-20  
**Estado:** Fase A completada (iteraciones 00–06)

---

## Resumen

El backend SaaS pasa de **tres fuentes de verdad** y fall-open a un modelo **fail-closed** centrado en `ITenantEntitlementsService` y suscripción relacional activa.

| Iteración | Rama | Commit (mensaje) | Entregable |
|-----------|------|------------------|------------|
| 00 | `refactor/saas-enterprise-00-inventory` | inventario | [saas-enterprise-inventory.md](./saas-enterprise-inventory.md) |
| 01 | `refactor/saas-enterprise-01-entitlements-service` | `feat(saas): add ITenantEntitlementsService` | SoT lectura relacional |
| 02 | `refactor/saas-enterprise-02-safe-defaults` | `fix(saas): enforce fail-closed module defaults` | Sin `AllModuleKeys` en runtime |
| 03 | `refactor/saas-enterprise-03-unified-gates` | `refactor(saas): unify permission and feature gates` | HTTP + MediatR unificados |
| 04 | `refactor/saas-enterprise-04-usage-upsert` | `refactor(saas): atomic usage UPSERT without nested SaveChanges` | Consumo atómico |
| 05 | `refactor/saas-enterprise-05-platform-query` | `refactor(saas): centralize IgnoreQueryFilters via PlatformQueryAccessor` | Wrapper + audit test |
| 06 | `refactor/saas-enterprise-06-phase-a-closeout` | `docs(saas): phase A closeout, flags, legacy markers` | Este documento + flags |

ADRs: [ADR-0001](../adr/ADR-0001-single-source-entitlements.md) … [ADR-0006](../adr/ADR-0006-phase-a-closeout.md).

---

## Fuente de verdad actual (post Fase A)

| Decisión | Autoridad |
|----------|-----------|
| Módulos en JWT / sesión | `ISessionModulesResolver` → `ITenantEntitlementsService` (por defecto) |
| Permiso HTTP por plan | `ITenantEntitlementsService.AllowsPermissionAsync` |
| Feature MediatR `[RequireFeature]` | `ITenantEntitlementsService.HasFeatureAsync` |
| Límites / consumo | `ISubscriptionService` + UPSERT PostgreSQL |
| Consultas sin filtro tenant | `IPlatformQueryAccessor` (allowlist IQF residual) |

### Legacy (deprecado, no eliminado)

| Artefacto | Uso residual |
|-----------|----------------|
| `Tenant.EnabledModulesJson` | Escritura SuperAdmin; caché visual `hasModuleRestrictions` |
| `TenantSubscriptionCatalog.GetEffectiveEnabledModules` | Solo flag de emergencia / tests |
| `Tenant.PlanCode` | Denormalización + sync en `SaveChanges` (Fase B) |
| `TenantSubscriptionCatalog.AllModuleKeys` | Validación entrada SuperAdmin + JWT SuperAdmin global |

---

## Flags (`Saas:Entitlements`)

En `appsettings.json`:

```json
"Saas": {
  "Entitlements": {
    "FailClosedWithoutActiveSubscription": true,
    "PreferLegacyEnabledModulesJsonForSession": false,
    "LogPlatformQueries": false
  }
}
```

| Flag | Default | Efecto |
|------|---------|--------|
| `FailClosedWithoutActiveSubscription` | `true` | Sin suscripción activa → módulos vacíos |
| `PreferLegacyEnabledModulesJsonForSession` | `false` | Si `true`, JWT/sesión lee JSON legacy en lugar del modelo relacional |
| `LogPlatformQueries` | `false` | Log debug en cada `IPlatformQueryAccessor.Unfiltered` |

**Rollback rápido de módulos de sesión:** poner `PreferLegacyEnabledModulesJsonForSession: true` y reiniciar API (no revierte gates HTTP/MediatR).

---

## Rollback por iteración

Revertir ramas en orden inverso (06 → 00) o cherry-pick inverso del commit afectado.

| Si falla… | Revertir | Síntoma sin revert |
|-----------|----------|-------------------|
| Módulos vacíos en UI | 02 + 03 | Tenants sin `TenantSaasSubscription` activa |
| 403 en API con plan correcto | 03 | Mapping permisos / seed de features |
| Límites de uso incorrectos | 04 | Concurrencia o sin flush InMemory |
| Audit IQF en CI | 05 | Nuevo `.IgnoreQueryFilters(` fuera de allowlist |
| Flags / resolver sesión | 06 | Comportamiento de emergencia JSON |

Rollback completo Fase A: volver a `main` anterior al merge de `refactor/saas-enterprise-*` y restaurar comportamiento documentado en inventario pre-00.

---

## Verificación recomendada antes de merge

1. `dotnet build` en `backend/src/ERP.API`
2. `dotnet test` en `ERP.Infrastructure.Tests` (entitlements, usage, IQF audit)
3. Tenant con suscripción activa + plan con features `Module`: login → `enabledModules` no vacío
4. Tenant sin suscripción: login → módulos vacíos (fail-closed)
5. Permiso `sales.invoices.view` denegado si plan sin módulo `sales`

---

## Fase B (fuera de alcance A)

- Eliminar `EnabledModulesJson` de API pública y JWT
- Normalizar `moduleKey` en frontend (inglés canónico)
- Migrar IQF residual (seeding, Hangfire, `ErpDbContext` sync)
- Sync no destructivo de `TenantSaasSubscription`
- Historial de eventos de suscripción

---

## Open questions (registradas, no bloquean merge A)

1. ¿`moduleKey` oficial inglés en SuperAdmin UI?
2. ¿Deprecar `PlanCode` en columna `Tenant`?
3. ¿Segunda etapa para quitar claim `enabledModules` del JWT?
