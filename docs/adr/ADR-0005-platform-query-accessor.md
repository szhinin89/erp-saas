# ADR-0005: Platform query accessor for IgnoreQueryFilters

**Estado:** Aceptado (iteración 05 — wrapper + auditoría; migración parcial)  
**Fecha:** 2026-05-20

---

## Contexto

`IgnoreQueryFilters()` aparecía **47 veces en 11 archivos** sin wrapper ni logging. Es necesario para auth cross-tenant, config con `TenantId` explícito, jobs y seed — pero un uso olvidado sin filtro manual puede filtrar datos de otro tenant.

---

## Decisión

1. **`IPlatformQueryAccessor` / `PlatformQueryAccessor`** — único lugar en repositorios/servicios de aplicación que invoca `IgnoreQueryFilters`, con `PlatformQueryReason` y log debug.
2. **Migración** en iteración 05 de repositorios y servicios de alto tráfico: `UserRepository`, `ConfigService`, `AccessRepository`, `CarrierRepository`, `ProductCatalogRepository`, `GrowthAnalyticsReader`.
3. **Excepciones temporales** (allowlist en `IgnoreQueryFiltersAuditTests`): `ErpDbContext` sync, seeding, deployment, Hangfire, dev seeder, background kardex.
4. **Test de auditoría** falla si aparece `IgnoreQueryFilters` en un archivo .cs nuevo fuera de la allowlist.

---

## Consecuencias

### Positivas

- Punto de extensión para métricas/alertas de consultas cross-tenant.
- CI impide regresiones por IQF directo en repositorios.

### Negativas

- Ocho archivos legacy siguen con IQF directo hasta migración posterior.
- Constructores de repositorios migrados requieren `IPlatformQueryAccessor` en tests manuales.

---

## Rollback

Revertir rama `refactor/saas-enterprise-05-platform-query` y restaurar `.IgnoreQueryFilters()` inline.
