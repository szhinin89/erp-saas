# ADR-ERP-002 — Platform Separation (frontera de integración)

**Status:** Accepted
**Date:** 2026-06-08
**Author:** Sebastian Zhinin
**Context:** FASE 8 — Ejecución real de separación ERP ↔ Platform

---

## Contexto

[ADR-ERP-001](./ADR-ERP-001-core-independence.md) estableció que el ERP es el producto y que Platform será un bounded context separado e independiente, en repositorio propio. Esta ADR formaliza la **frontera de integración concreta** entre ambos productos: cómo se comunican, qué está prohibido, y qué API expone el ERP para que una futura Platform externa lo consuma.

## Decisión

**ERP Core es independiente. Platform consume ERP exclusivamente mediante API versionada (`/api/integration/v1/*`). ERP nunca consume Platform.**

### Reglas permanentes

| Regla | Descripción |
|-------|-------------|
| **Sin referencias de compilación cruzadas** | `ERP.*` no referencia ni compila contra ningún proyecto `Platform.*` / `ZH.Platform.*`, y viceversa no aplica (Platform vivirá en repositorio separado) |
| **Sin entidades compartidas** | Ninguna entidad de dominio se comparte entre ERP y Platform; Platform modela sus propios conceptos (planes, billing, límites) sin tocar `ERP.Domain` |
| **Sin DbContexts compartidos** | `ErpDbContext` es exclusivo del ERP; Platform usa su propio almacenamiento |
| **Sin tablas compartidas** | El esquema `tenants`, `companies`, etc. pertenece al ERP; Platform no escribe directamente sobre esas tablas |
| **Integración solo por API** | Toda interacción Platform → ERP pasa por `/api/integration/v1/*`, autenticada con la policy `IntegrationApi` |
| **Dirección única** | `Platform → ERP` es la única dirección permitida; `ERP → Platform` está prohibido |

### Frontera de integración expuesta

`ERP.API/Controllers/Integration/IntegrationController.cs` (`/api/integration/v1`, policy `IntegrationApi`):

| Recurso | Operaciones |
|---------|-------------|
| `tenants` | `POST` crear, `GET {id}/status`, `PUT {id}/activate`, `PUT {id}/suspend` |
| `companies` | `POST` crear, `GET {id}/status`, `PUT {id}/activate`, `PUT {id}/suspend` |

Contratos y casos de uso en `ERP.Application/Modules/Integration/` (`IntegrationContracts.cs`, `TenantIntegrationUseCases.cs`, `CompanyIntegrationUseCases.cs`). Sin lógica de negocio de Platform — solo DTOs, comandos/queries MediatR, autorización y versionado.

## Consecuencias

### Positivas
- El ERP puede extraerse a su propio repositorio sin arrastrar conceptos comerciales.
- Una futura Platform (gestión de planes, billing, onboarding comercial) puede construirse e iterar sin modificar el ERP — solo consume `/api/integration/v1/*`.
- Cambios internos del ERP (entidades, repositorios, handlers) no rompen a Platform mientras el contrato de integración se mantenga estable y versionado.

### Restricciones
- Cualquier necesidad nueva de Platform sobre datos/operaciones del ERP requiere **extender `/api/integration/v1/*`** (nueva versión si rompe compatibilidad), nunca acceso directo a `ErpDbContext`, repositorios o entidades.
- El ERP no debe agregar lógica de planes, límites comerciales o billing — eso vive exclusivamente en Platform (ver [ADR-ERP-001](./ADR-ERP-001-core-independence.md)).

## Invariantes que no deben romperse

1. `ERP.*` no importa ni referencia ningún `Platform.*` / `ZH.Platform.*`.
2. Toda comunicación Platform → ERP pasa por `/api/integration/v1/*` con la policy `IntegrationApi`.
3. Ningún DbContext, tabla o entidad de dominio se comparte entre ERP y Platform.
4. Nuevas capacidades de integración se agregan como nuevas rutas/versión bajo `/api/integration/v{n}/*`, nunca como acceso directo a internals del ERP.

## Referencias

- [ADR-ERP-001 — ERP Core Independence](./ADR-ERP-001-core-independence.md)
- [IntegrationController](../../../backend/src/ERP.API/Controllers/Integration/IntegrationController.cs)
- [Modules/Integration](../../../backend/src/ERP.Application/Modules/Integration/)
- [STATUS.md](../../STATUS.md)
