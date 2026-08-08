# Backend — reglas de implementación

Este archivo complementa [`../CLAUDE.md`](../CLAUDE.md). Si hay conflicto, prevalece `../CLAUDE.md`.

Cuerpo normativo completo: [`../docs/architecture/backend.md`](../docs/architecture/backend.md) · [`../docs/architecture/architecture.md`](../docs/architecture/architecture.md) · [`../docs/architecture/security.md`](../docs/architecture/security.md) · [`../docs/architecture/error-handling.md`](../docs/architecture/error-handling.md) · [`../docs/architecture/events.md`](../docs/architecture/events.md).

No repetir aquí el `CLAUDE.md` raíz. No incluir estado de avance ni roadmap — eso vive en `STATUS.md`.

---

## Clean Architecture

- `Domain` no depende de EF Core / ASP.NET / MediatR / HTTP.
- `Application` no accede a `DbContext` directamente ni contiene lógica HTTP.
- `Infrastructure` contiene EF/repos/servicios externos — nunca reglas de negocio.
- `API` contiene controllers/middleware delgados — sin entidades de dominio ni lógica de negocio.

Dirección de dependencias: `ERP.API → ERP.Application → ERP.Domain ← ERP.Infrastructure`. No existe `ERP.Shared`.

## CQRS / MediatR

- Un caso de uso por módulo en `ERP.Application/Modules/{Modulo}/UseCases/`.
- Validación con **FluentValidation** — todo Command/Query con entrada de usuario tiene su `[Nombre]Validator`.
- Handlers sin lógica HTTP — devuelven `Result<T>`, nunca `throw` de excepciones genéricas al controller.
- Controllers usan `ApiResultExtensions` (`ToOkOrBadRequest`, etc.) — nunca `Ok(new ApiResponse<T>{...})` manual.

## Multi-tenant

- Query filters **fail-closed**: sin `TenantId`/`CompanyId` válido en contexto → 0 filas, nunca fuga cross-tenant.
- `.IgnoreQueryFilters()` sin justificación **prohibido** — solo vía `IPlatformQueryAccessor` con `PlatformQueryReason` documentado.
- `TenantId`/`CompanyId`/`BranchId` provienen del contexto autenticado (`ICurrentTenant`/`ICurrentBranch`/JWT) — nunca del body/query como autoridad.

## Persistencia

- **EF migrations** (`dotnet ef migrations add`) — nunca SQL manual para cambios estructurales.
- Repositorios en `Infrastructure`, implementando interfaces de `Domain`.
- `IUnitOfWork` si la operación requiere transacción multi-paso — nunca en el controller.

## Seguridad

- No secretos en logs.
- No JWT/email/password en logs ni métricas.
- No bypass de permisos — `SecurityRoles`/`isAdminRole()` son la única fuente de "es Admin", nunca comparaciones ad-hoc de string.

## SRI / contabilidad

- No duplicar cálculos tributarios — único motor: `ISriTaxResolver` + catálogos `sri_*_rates`.
- No hardcodear reglas fiscales si existe catálogo/servicio — nunca `'10'`/`'0'` como código tributario por defecto.
- No romper idempotencia contable — asientos y secuencias documentales solo vía la infraestructura FROZEN correspondiente (`CaptureNextAsync`, `SalesInvoiceDetail.ApplyTaxes()`).

Detalle completo de las 3 reglas anteriores: [`../docs/architecture/frozen-infrastructure.md`](../docs/architecture/frozen-infrastructure.md).
