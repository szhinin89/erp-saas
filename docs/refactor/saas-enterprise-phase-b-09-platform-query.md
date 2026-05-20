# SaaS Enterprise Refactor — Fase B iteración 09

**Branch:** `refactor/saas-enterprise-09-platform-query-migration`

## Entregables

Migración de todos los `IgnoreQueryFilters()` directos a `IPlatformQueryAccessor.Unfiltered` con `PlatformQueryReason` documentado.

| Archivo | Motivo |
|---------|--------|
| `ErpDbContext` (sync suscripción) | `DbContextSync` |
| `TenantOnboardingService` | `Seeding` |
| `DefaultProfileSeeder` | `Seeding` |
| `FirstRunSetupService` | `CrossTenantSystem` |
| `KardexReporteProcessor` | `BackgroundJob` |
| `SriRetryJob` | `BackgroundJob` |
| `DevDatabaseSeeder` | `DevOnly` |

## Auditoría

`IgnoreQueryFiltersAuditTests` allowlist reducida a **solo** `PlatformQueryAccessor.cs`.

## Pendiente (10)

- Retirar columna `enabled_modules` y flag legacy de sesión.
