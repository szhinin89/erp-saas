# ADR-0006: Phase A closeout — legacy markers and operational flags

**Estado:** Aceptado (iteración 06)  
**Fecha:** 2026-05-20

---

## Contexto

Las iteraciones 01–05 introdujeron `ITenantEntitlementsService`, fail-closed, gates unificados, UPSERT de usage y `IPlatformQueryAccessor`. Quedaban APIs legacy sin marcar, sin documentación de rollback unificada y sin interruptor operativo para emergencias.

---

## Decisión

1. **`[Obsolete]`** en `Tenant.EnabledModulesJson`, `GetEffectiveEnabledModules` y `TenantAllowsPermission` (sync).
2. **`ISessionModulesResolver`** como fachada de módulos de sesión con lectura de `SaasEntitlementsOptions`.
3. **`Saas:Entitlements`** en appsettings (fail-closed, prefer legacy JSON, log platform queries).
4. Documento de cierre [saas-enterprise-phase-a-closeout.md](../refactor/saas-enterprise-phase-a-closeout.md) con tabla de ramas, rollback y verificación.

No se elimina columna JSON ni catálogo estático en esta fase (compatibilidad SuperAdmin).

---

## Consecuencias

- Compilación con warnings CS0618 en tests que ejercitan legacy a propósito.
- Rollback de sesión sin redeploy de código (solo config) vía `PreferLegacyEnabledModulesJsonForSession`.
- Fase B debe planificar eliminación de obsolete y migración frontend.

---

## Rollback

Revertir rama `refactor/saas-enterprise-06-phase-a-closeout`.
