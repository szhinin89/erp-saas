# ADR-0002: Fail-closed module defaults

**Estado:** Aceptado  
**Fecha:** 2026-05-20

---

## Contexto

`TenantSubscriptionCatalog.GetEffectiveEnabledModules` trataba `EnabledModulesJson` null, vacío o inválido como “todos los módulos” (`AllModuleKeys`), habilitando de facto el producto completo. El frontend replica el patrón (`enabledModules.length === 0` → mostrar todo).

Tras ADR-0001, existe `ITenantEntitlementsService` como lectura canónica desde suscripción activa + plan + overrides, con deny-by-default sin suscripción.

---

## Decisión

1. **`GetEffectiveEnabledModules` (sync):** deja de devolver `AllModuleKeys` en cualquier caso; null/vacío/inválido/solo claves desconocidas → **lista vacía**. Solo devuelve claves cuando el JSON de caché legacy contiene módulos válidos explícitos.

2. **`ResolveEnabledModulesAsync`:** método de aplicación que delega en `ITenantEntitlementsService.GetEnabledModuleKeysAsync` para JWT, permisos de sesión y DTOs de tenant.

3. **Handlers de auth/sesión y listados SuperAdmin** migrados a `ResolveEnabledModulesAsync`. `AllModuleKeys` se conserva **únicamente** para SuperAdmin global sin `tenantId` (contexto sin empresa).

4. **`SubscriptionService`:** el fallback por módulo usa entitlements, no JSON legacy.

`EnabledModulesJson` permanece escribible desde SuperAdmin pero **no es autoridad** para nuevas lecturas de sesión.

---

## Consecuencias

### Positivas

- Elimina el vector fail-open más grave antes de producción SaaS.
- Alinea respuestas de login/refresh con el plan real cuando hay `tenant_saas_subscriptions` y catálogo de features.

### Negativas

- Tenants sin suscripción activa ni JSON explícito verán **menú vacío** en UI hasta provisionar plan (comportamiento deseado).
- SuperAdmin que dependía de “sin restricción = todo” debe asignar plan/features o lista JSON explícita.

### Operativas

- Verificar sync `PlanCode` → `TenantSaasSubscription` y seed de `saas_plan_features` / `ResourceRef` en features `Module`.

---

## Alternativas

| Alternativa | Rechazo |
|-------------|---------|
| Feature flag para mantener AllModuleKeys | Añade complejidad; pospone el fix de seguridad |
| Solo documentar el riesgo | No corrige drift ni exposición en runtime |
| Borrar `EnabledModulesJson` ya | Rompe APIs y panel SuperAdmin en un solo paso |

---

## Rollback

1. Revertir commit `fix(saas): enforce fail-closed module defaults`.
2. Restaurar ramas `return AllModuleKeys` en `GetEffectiveEnabledModules`.
3. Quitar llamadas a `ResolveEnabledModulesAsync` en handlers (volver a sync legacy).

Sin cambios de esquema BD.
