# Reglas de arquitectura enterprise — ERP SaaS ZH Technologies

> **Fuente canónica PR (B-xx / F-xx).** Adaptador de entrada: [`docs/ARCHITECTURE-RULES.md`](../ARCHITECTURE-RULES.md). Índice general: [README.md](./README.md).

**Autoridad:** documento normativo. Prevalece sobre convenciones informales, comentarios sueltos y preferencias personales cuando hay conflicto con seguridad, tenant o capas.

**Audiencia:** Cursor AI, Claude Code, Copilot, revisores PR, auditoría técnica.

**Violación:** cualquier incumplimiento de regla marcada **BLOQUEANTE** impide merge hasta corrección o excepción documentada en el PR (issue link + fecha + responsable).

**Relacionados (descriptivos, no sustitutos):** [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md), [`docs/DEVELOPMENT.md`](../DEVELOPMENT.md), [`docs/IDENTITY.md`](../IDENTITY.md), [`docs/DATABASE.md`](../DATABASE.md), [`CLAUDE.md`](../../CLAUDE.md), [`docs/architecture/`](./README.md), `.cursor/rules/`.

---

## 0. Precedencia

| Orden | Fuente | Aplica cuando |
|-------|--------|----------------|
| 1 | Seguridad / multi-tenant / billing | Siempre |
| 2 | Este documento (`docs/architecture/pr-rules-catalog.md`) | Diseño e implementación |
| 3 | Otros `docs/architecture/*` por área | Detalle operativo |
| 4 | `.cursor/rules/*.mdc` | Hints Cursor por glob |
| 5 | [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) | Contexto y diagramas |

---

# BACKEND

## B-01 — ERP.Domain sin dependencias de infraestructura

### RULE
`ERP.Domain` contiene únicamente dominio puro: entidades (factories), value objects, enums, excepciones de dominio, interfaces de dominio. **Prohibido** referenciar EF Core, ASP.NET, `HttpContext`, `IConfiguration`, `DbContext`, MediatR, Dapper, Redis, archivos, HTTP o paquetes NuGet de aplicación/infraestructura.

### WHY
El dominio es el núcleo estable; cualquier acoplamiento a framework impide testear invariantes y fuerza regresiones en migraciones de stack.

### BAD
```csharp
// ERP.Domain/Modules/Sales/Entities/Invoice.cs
using Microsoft.EntityFrameworkCore;
public class Invoice {
  public void Save(ErpDbContext db) => db.SaveChanges();
}
```

### GOOD
```csharp
// ERP.Domain/Modules/Sales/Entities/Invoice.cs
public sealed class Invoice : DocumentEntity {
  public static Invoice Create(...) { /* guard clauses */ }
  public void Authorize(DateTime at, Guid actorId) { /* invariantes */ }
}
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "Microsoft\.(EntityFrameworkCore|AspNetCore)" backend/src/ERP.Domain`
- CI: analyzer custom / script que falle si `ERP.Domain.csproj` referencia paquetes fuera de whitelist vacía (solo BCL).
- Revisión: cero `PackageReference` en `ERP.Domain.csproj` salvo excepción aprobada en ADR.

---

## B-02 — Entidades de negocio: factory obligatoria, soft delete obligatorio

### RULE
Toda entidad de negocio se instancia con `Entidad.Create(...)` o factory estática equivalente. **Prohibido** `new Entidad()` público en agregados. **Prohibido** DELETE físico en entidades de negocio; desactivación vía `Disable()` / `IsActive = false`. Excepciones solo las listadas en `CLAUDE.md` (`SaasPlan`, `ExpenseCategory` mapping).

### WHY
Invariantes y auditoría dependen de construcción controlada; DELETE físico rompe trazabilidad multi-tenant y compliance.

### BAD
```csharp
var p = new Product { Name = "X" };
db.Products.Remove(p);
```

### GOOD
```csharp
var p = Product.Create("X", tenantId, actorId);
p.Disable(actorId);
```

### ENFORCEMENT
- **BLOQUEANTE PR:** endpoint `DELETE` en controller de entidad de negocio documentada como soft-delete.
- Revisión: handlers no llaman `DbContext.Remove` en agregados ERP salvo excepciones documentadas.

---

## B-03 — ERP.Application sin persistencia directa

### RULE
`ERP.Application` **no** referencia `ErpDbContext`, SQL crudo, `FromSqlRaw`, ADO.NET ni tipos de `ERP.Infrastructure`. Casos de uso vía interfaces de repositorio/contratos definidos en Application o Domain. **Prohibido** lógica HTTP (status codes, headers, cookies).

### WHY
Application orquesta casos de uso; persistencia es detalle reemplazable y testeable con mocks.

### BAD
```csharp
public class CreateProductHandler(ErpDbContext db) {
  public async Task Handle(...) {
    await db.Products.AddAsync(new Product { ... });
    await db.SaveChangesAsync();
  }
}
```

### GOOD
```csharp
public class CreateProductHandler(IProductRepository repo, IUnitOfWork uow) {
  public async Task<Result<ProductDto>> Handle(CreateProductCommand cmd, ...) {
    var entity = Product.Create(...);
    await repo.AddAsync(entity);
    await uow.SaveChangesAsync(ct);
    return Result<ProductDto>.Success(Map(entity));
  }
}
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "ErpDbContext" backend/src/ERP.Application`
- **BLOQUEANTE PR:** `ProjectReference` de Application a Infrastructure (debe ser inexistente).

---

## B-04 — ERP.Infrastructure sin orquestación de dominio

### RULE
Infrastructure implementa persistencia, integraciones externas (SRI, Redis, email), protección de secretos y filtros EF. **Prohibido** reglas de negocio (cálculo de totales fiscales, transiciones de estado de documento, validación de invariantes de agregado). **Prohibido** invocar `IMediator.Send` de handlers Application desde Infrastructure.

### WHY
Duplicar reglas en Infrastructure crea dos fuentes de verdad y bypass de validators MediatR.

### BAD
```csharp
// Infrastructure repository
if (invoice.Total <= 0) throw new Exception("Total inválido");
invoice.Status = "Autorizado"; // transición de dominio en infra
```

### GOOD
```csharp
// Infrastructure repository — solo persistencia
public async Task AddAsync(Invoice invoice, CancellationToken ct) {
  await _db.Set<Invoice>().AddAsync(invoice, ct);
}
```

### ENFORCEMENT
- Revisión PR: cambios en `ERP.Infrastructure` que contengan `if` de negocio → exigir mover a Domain/Application.
- **BLOQUEANTE PR:** `grep -r "IMediator" backend/src/ERP.Infrastructure --include="*.cs"` en código productivo (tests excluidos).

---

## B-05 — ERP.API: controllers delgados, sin DbContext

### RULE
Controllers reciben HTTP, autorizan, mapean a Command/Query MediatR y devuelven resultado vía `ApiResultExtensions` (`ToOkOrBadRequest`, `ToCreatedOrBadRequest`, etc.). **Prohibido** inyectar `ErpDbContext` en controllers. **Prohibido** `new ApiResponse<T>` manual. **Prohibido** transacciones multi-paso en controller; usar `IUnitOfWork` en handler.

### WHY
Controllers gordos ocultan casos de uso, evitan tests de Application y filtran tenant incorrectamente.

### BAD
```csharp
[ApiController]
public class ProductsController(ErpDbContext db) : ControllerBase {
  [HttpPost]
  public async Task<IActionResult> Create(CreateProductDto dto) {
    db.Products.Add(new Product { Name = dto.Name });
    await db.SaveChangesAsync();
    return Ok(db.Products.ToList());
  }
}
```

### GOOD
```csharp
[HttpPost]
[ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
public async Task<IActionResult> Create([FromBody] CreateProductRequest req, CancellationToken ct) {
  var result = await _mediator.Send(new CreateProductCommand(...), ct);
  return this.ToCreatedOrBadRequest(result); // code default = ApiResponseCodes.Created
}
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "ErpDbContext" backend/src/ERP.API/Controllers`
- **BLOQUEANTE PR:** `grep -r "new ApiResponse" backend/src/ERP.API/Controllers`
- Checklist PR: `[ProducesResponseType]` para 401/403/404/422 cuando aplique.

---

## B-06 — Comunicación entre módulos Application

### RULE
**Prohibido** importar handlers de otro módulo Application (`using ERP.Application.Modules.X.Handlers`). Comunicación vía contratos de dominio (`I*Repository`), eventos de dominio, o orquestación explícita en Application con interfaces. **Prohibido** AutoMapper; mapeos manuales en handlers.

### WHY
Acoplamiento cruzado convierte el monolito modular en big ball of mud y rompe límites de despliegue futuro.

### BAD
```csharp
using ERP.Application.Modules.Customers.Handlers;
await _mediator.Send(new GetCustomerQuery(id)); // desde módulo Ventas handler
```

### GOOD
```csharp
// Contrato en Domain o Application.Abstractions
var customer = await _customerReadStore.GetByIdAsync(id, ct);
```

### ENFORCEMENT
- **BLOQUEANTE PR:** revisar `using ERP.Application.Modules.*` cruzado entre carpetas de módulos distintos en handlers.
- Script CI: matriz de imports entre `Modules/*` (falla si Application importa otro módulo sin carpeta `Contracts`).

---

## B-07 — Sin compatibilidad legacy en pre-producción

### RULE
Mientras el sistema no esté confirmado en producción con clientes reales, está **prohibido** introducir capas de compatibilidad legacy. Todo cambio de modelo, contrato o código debe corregirse **en el origen** (todos los call-sites, tests, seeders y contratos), no mediante adaptadores de transición.

Patrones **explícitamente prohibidos**:
- `NormalizeType()` / `MapOldToNew()` / cualquier método que traduzca formato antiguo a nuevo.
- Aliases de constantes duplicadas (`TypeRuc = "RUC"` cuando el código real es `"04"`).
- Endpoints o deserializadores que aceptan dos formatos simultáneamente.
- Código comentado `// legacy`, bloques `// TODO: remove when migrated`, wrappers `V1`/`V2` coexistentes.
- Value Objects con rama `if (legacy) { ... }`.

**No aplica** a `EVENT-VERSIONING.md` (Outbox es un log inmutable con sus propias reglas).

### WHY
Sin clientes productivos, cada capa de compatibilidad es deuda técnica innecesaria que enturbia el diseño real y complica la refactorización futura. El costo de actualizar todos los call-sites ahora es casi cero; el costo de mantener adapters en producción crece indefinidamente.

### CUÁNDO SE PUEDE EXCEPCIONAR
Solo con **las tres condiciones simultáneas**:
1. Deploy real en producción con usuarios reales documentados.
2. Consumidor externo que no puede migrarse en el mismo PR (con evidencia).
3. Issue/PR de seguimiento con fecha límite ≤ 2 sprints y responsable asignado.

### BAD
```csharp
// ❌ Adapter de transición innecesario
public static string NormalizeType(string type) => type switch {
    "RUC" => "04",
    "CI"  => "05",
    _     => type,
};
```

### GOOD
```csharp
// ✅ Corrección en origen — todos los call-sites usan "04" directamente
var bp = BusinessPartner.Create(tenantId, "04", ruc, name, userId);
```

### ENFORCEMENT
- **BLOQUEANTE PR:** presencia de métodos `Normalize*`, `MapLegacy*`, `Adapter*` en `ERP.Domain` o `ERP.Application` sin excepción documentada.
- **BLOQUEANTE PR:** constantes de compatibilidad duplicadas (alias que mapean un valor a otro equivalente).
- Revisión: todo `// TODO: remove` sin issue link y fecha es violación inmediata.

Política completa: [CORE-ARCHITECTURE.md — Política de compatibilidad legacy](./architecture.md#política-de-compatibilidad-legacy)

---

## B-08 — Single Command per Operation

Regla completa (cuerpo normativo único): [architecture.md § B-08](./architecture.md#b-08--single-command-per-operation-bloqueante).

### BAD
```csharp
CreateProductCommand   // crea producto
AddProductCommand      // ❌ duplicado semántico
RegisterProductCommand // ❌ duplicado semántico
```

### GOOD
```csharp
CreateProductCommand   // ✅ único Command de creación
UpdateProductCommand   // ✅ único Command de actualización
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nuevo Command cuyo nombre es sinónimo de un Command existente sobre el mismo agregado.
- Revisión: comparar con Commands existentes antes de crear uno nuevo.

---

## B-09 — No Semantic DTO Duplication

Regla completa (cuerpo normativo único): [architecture.md § B-09](./architecture.md#b-09--no-semantic-dto-duplication-bloqueante).

### BAD
```csharp
ProductDto         // id, name, code, price
ProductSummaryDto  // ❌ mismos campos que ProductDto, nombre distinto
ProductResponseDto // ❌ wrapping sin valor añadido
```

### GOOD
```csharp
ProductDto         // ✅ campos para listado (id, name, code, isActive)
ProductDetailDto   // ✅ campos para detalle completo + children (barcodes, conversions)
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nuevo DTO cuyos campos son subconjunto o superconjunto equivalente de un DTO existente sin propósito diferenciado.

---

## B-10 — Scope Boundary Enforcement

Regla completa (cuerpo normativo único): [architecture.md § B-10](./architecture.md#b-10--scope-boundary-enforcement-bloqueante). **SUBSCRIBER** es histórico — Control Plane SaaS eliminado permanentemente del ERP Core (ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)); el boundary se conserva como guardrail preventivo.

### BAD
```csharp
// ❌ SUBSCRIBER importando lógica ERP
namespace ERP.Domain.Billing {
    using ERP.Domain.Modules.Sales.Entities; // prohibido
}
```

### GOOD
```csharp
// ✅ Referencias cruzadas por ID, no por entidad
// (ejemplo ilustrativo — el dominio Billing fue eliminado permanentemente
//  del ERP Core; el patrón aplica a cualquier futura integración cross-scope)
public class SaasBillingInvoice {
    public Guid? ErpInvoiceId { get; private set; } // referencia cruzada como Guid
}
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `using` de namespace de otro scope dentro de una entidad de dominio.

---

## B-11 — No Per-Subscriber Regulatory Data

Regla completa (cuerpo normativo único): [architecture.md § B-11](./architecture.md#b-11--no-per-subscriber-regulatory-data-bloqueante).

### BAD
```csharp
// ❌ tabla per-tenant duplicando dato global
CREATE TABLE tax_rates (tenant_id uuid, code text, percentage decimal);
```

### GOOD
```csharp
// ✅ referencia directa al catálogo global
public string? SaleVatCode { get; private set; }  // FK a global.sri_vat_rate.code
public string  UomCode     { get; private set; }  // FK a global.sri_uom.code
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nueva tabla con `tenant_id` que almacena datos regulatorios SRI (tasas, códigos, catálogos).
- Script CI: verificar que no existen nuevas tablas per-subscriber en el schema `global`.

---

# CQRS

## C-01 — Un Command/Query = un handler + un validator

### RULE
Cada `*Command` y `*Query` público tiene exactamente un `*Handler` y un `*Validator` FluentValidation en la misma carpeta cuando existan reglas de entrada. Validators registrados en pipeline (`ValidationBehavior`). Handlers devuelven `Result<T>` para fallos de negocio esperados; **prohibido** `throw` genérico hacia controller.

### WHY
Validación duplicada o ausente en Application es la causa #1 de bugs 422/400 inconsistentes.

### BAD
```csharp
public record CreateBranchCommand(string Name);
// Sin CreateBranchCommandValidator
public class CreateBranchHandler { ... }
```

### GOOD
```csharp
public record CreateBranchCommand(string Name, ...);
public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand> { ... }
public class CreateBranchHandler : IRequestHandler<CreateBranchCommand, Result<BranchDto>> { ... }
```

### ENFORCEMENT
- **BLOQUEANTE PR:** Command/Query nuevo sin archivo `*Validator.cs` cuando el endpoint persiste datos.
- Test: `ERP.Application.Tests` — validator cubre reglas mínimas (required, max length alineado EF).

---

## C-02 — Naming obligatorio CQRS

### RULE
| Artefacto | Patrón | Ubicación |
|-----------|--------|-----------|
| Escritura | `{Verbo}{Entidad}Command` | `Modules/{Modulo}/Commands/` o `UseCases/` |
| Lectura | `{Verbo}{Entidad}Query` | `Modules/{Modulo}/Queries/` |
| Handler | `{CommandOrQueryName}Handler` | Misma carpeta |
| Validator | `{CommandOrQueryName}Validator` | Misma carpeta |
| DTO respuesta | `{Entidad}Dto` | Application DTOs — **prohibido** exponer entidad Domain en API |

### WHY
Naming uniforme permite búsqueda, codegen y revisión mecánica.

### BAD
`ProductCreator.cs`, `GetAllProductsService.cs`, retornar `Product` entity al controller.

### GOOD
`CreateProductCommand`, `CreateProductCommandHandler`, `CreateProductCommandValidator`, `ProductDto`.

### ENFORCEMENT
- Revisión PR: rechazar handlers que no terminen en `Handler` o que no implementen `IRequestHandler<,>`.
- Grep CI: controllers no usan tipos de `ERP.Domain` en firmas públicas.

---

## C-03 — Tamaño máximo de handler

### RULE
Un handler **no** supera **150 líneas** en el cuerpo del método `Handle` (excluyendo usings y clase). Si supera 150 líneas, dividir en: (1) domain service puro, (2) sub-handlers privados, (3) pasos de orquestación con métodos privados sin lógica de persistencia duplicada. **Prohibido** handler que mezcle más de **un** agregado raíz en escritura sin patrón saga/documentado.

### WHY
Handlers monolíticos ocultan transacciones incorrectas y duplican reglas entre commands.

### BAD
Handler de 400 líneas con create + email + SRI + contabilidad inline.

### GOOD
`AuthorizeSalesBillCommandHandler` delega a `ISriAuthorizationService`, `IAccountingPostingService` (Application ports).

### ENFORCEMENT
- **BLOQUEANTE PR:** handler `Handle` > 150 líneas sin justificación en descripción PR.
- Script: `tools/quality/check-handler-size.ps1` (propuesto en sección Guardrails).

---

## C-04 — Validación en cuatro capas (datos persistidos)

### RULE
Todo dato persistido valida en: (1) Frontend Zod + RHF, (2) FluentValidation, (3) Domain factory/guard clauses, (4) EF `IEntityTypeConfiguration`. **Prohibido** validar solo en frontend o solo en controller. Unicidad multi-tenant: índice compuesto con `TenantId`/`CompanyId` según scope — **prohibido** unicidad global sin clave de scope.

### WHY
Bypass de capas es vector de corrupción de datos y fuga cross-tenant.

### BAD
Solo `[Required]` en DTO API sin validator ni configuración EF.

### GOOD
`productSchema.ts` + `CreateProductCommandValidator` + `Product.Create` + `ProductConfiguration` con `HasIndex(x => new { x.TenantId, x.Code }).IsUnique()`.

### ENFORCEMENT
- Checklist PR obligatorio (plantilla): casillas 4 capas marcadas.
- **BLOQUEANTE PR:** índice único sin columna de scope en entidades multi-tenant.

---

# MULTI-TENANT & SaaS

## M-01 — Scope en entidad y filtro EF

### RULE
Toda entidad de negocio operativa implementa el contrato de scope del producto (`ITenantScopedEntity`, `ICompanyScopedEntity`, etc.) y tiene query filter global registrado en `ErpDbContext.OnModelCreating`. Toda query de datos de tenant **incluye** filtro por scope en repositorio o filter global — **prohibido** asumir “single tenant”.

### WHY
Un SELECT sin filtro en SaaS es incidente de seguridad P1.

### BAD
```csharp
return await _db.Products.ToListAsync(); // sin tenant
```

### GOOD
```csharp
return await _db.Products.Where(p => p.TenantId == _tenant.Id).ToListAsync();
// o filter global + DisableFilter solo vía M-02
```

### ENFORCEMENT
- **BLOQUEANTE PR:** entidad nueva en `ERP.Domain` sin configuración EF + registro de filter.
- Revisión: `docs/DATABASE.md` actualizado si cambia scope.

---

## M-02 — IgnoreQueryFilters solo vía PlatformQueryAccessor

### RULE
**Prohibido** `.IgnoreQueryFilters()` directo en Application, API o handlers. En Infrastructure, solo a través de `IPlatformQueryAccessor` con `PlatformQueryReason` documentado (operador platform, billing platform, migración). Cada uso requiere comentario de razón y test de autorización.

### WHY
`IgnoreQueryFilters` sin control centralizado ha causado fugas cross-tenant en SaaS industry-wide.

### BAD
```csharp
var all = await _db.Customers.IgnoreQueryFilters().ToListAsync();
```

### GOOD
```csharp
await _platform.RunAsync(PlatformQueryReason.PlatformMetrics, async () => {
  return await _db.Tenants.ToListAsync(ct);
});
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "IgnoreQueryFilters" backend/src` fuera de `PlatformQueryAccessor` y tests.
- Revisión obligatoria de seguridad en cada match.

---

## M-03 — TenantId / CompanyId: JWT es la fuente

### RULE
Identificadores de scope en commands **provienen** de `ITenantContext` / claims JWT / `CompanyScopeBehavior` — **prohibido** confiar en IDs enviados por el cliente como autoridad. Body/query con `tenantId`, `companyId` solo como hints UX; el handler **sobrescribe** con contexto del usuario autenticado.

### WHY
El frontend es hostil; un UUID en body es trivial de falsificar.

### BAD
```csharp
public record UpdateCustomerCommand(Guid TenantId, ...);
// Handler usa cmd.TenantId del body
```

### GOOD
```csharp
var tenantId = _tenantContext.TenantId; // del JWT
```

### ENFORCEMENT
- **BLOQUEANTE PR:** Command con `TenantId`/`CompanyId` seteado desde request sin validar match con contexto.
- Frontend: regla `saas-navigation-no-sensitive-url.mdc` — **prohibido** `?tenantId=` en URL.

---

## M-04 — Índices únicos compuestos con scope

### RULE
Unicidad de negocio **siempre** incluye columna de scope (`TenantId`, `CompanyId`). **Prohibido** `HasIndex(x => x.Code).IsUnique()` sin scope en tablas multi-tenant.

### WHY
Dos tenants con mismo código es válido; unicidad global rompe aislamiento o bloquea onboarding.

### BAD
```csharp
builder.HasIndex(x => x.Code).IsUnique();
```

### GOOD
```csharp
builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
```

### ENFORCEMENT
- **BLOQUEANTE PR:** migración EF con índice único sin scope.
- Revisión DBA en `docs/DATABASE.md`.

---

# FRONTEND — MODULAR

## F-01 — Código nuevo solo en `modules/{domain}/`

### RULE
Toda feature nueva (API client, schema Zod, hooks, pages, componentes de dominio) vive bajo `frontend/src/modules/{domain}/` con estructura mínima: `api/`, `schemas/`, `hooks/`, `pages/`, `components/` según aplique. **Prohibido** añadir lógica nueva en `frontend/src/pages/` (solo re-export wrapper ≤ 15 líneas, cero hooks, cero lógica — mismo límite que [architecture.md § Reglas de arquitectura](./architecture.md#reglas-de-arquitectura-que-no-se-rompen) y PR-6 más abajo). **Prohibido** crear archivos en `frontend/src/services/` o `frontend/src/schemas/` (solo adapters `@deprecated` hacia módulos).

### WHY
Duplicación services/schemas/pages impidió auditoría frontend 2026; wrappers preservan rutas sin duplicar lógica.

### BAD
```typescript
// frontend/src/services/newFeatureService.ts — 80 líneas nuevas
// frontend/src/pages/NewFeaturePage.tsx — 600 líneas
```

### GOOD
```typescript
// modules/inventory/api/stockService.ts
// modules/inventory/pages/StockPage.tsx
// pages/StockPage.tsx → export { StockPage } from '../modules/inventory/pages/StockPage';
```

### ENFORCEMENT
- **BLOQUEANTE PR:** archivo nuevo en `services/` o `schemas/` que no sea re-export de una línea con `@deprecated`.
- **BLOQUEANTE PR:** `pages/*.tsx` nuevo > 15 líneas sin estar en `modules/`.
- Grep CI: diff contra `main` — contar líneas añadidas en paths prohibidos.

---

## F-02 — HTTP solo vía `modules/lib/api` y `apiEnvelope`

### RULE
Llamadas HTTP usan `frontend/src/modules/lib/api.ts` (Axios + refresh). Deserialización de envelope con `readEnvelopePayload` / helpers `apiGet|Post|Put|Patch|Delete` en `apiEnvelope.ts` (extrae el campo `data` del envelope `ApiResponse<T>`). **Prohibido** `fetch()` directo en pages. **Prohibido** duplicar lógica de extracción de `data` en servicios.

El envelope no tiene `errors`/`warnings` como campos raíz: el detalle dinámico de
validación/error de una instancia viaja en `data.errors: string[]`, y el mensaje
genérico estable por `code` en `message.user`/`message.dev`. `apiError.ts`
(`messageFromRecord`) prioriza `data.errors[0]` y cae a `message.user`.

### WHY
Refresh token, 401 y logout centralizado se rompen con clientes paralelos.

### BAD
```typescript
const res = await fetch('/api/inventory/products');
const data = await res.json();
```

### GOOD
```typescript
import { apiGet } from '../../lib/apiEnvelope';
export const productService = {
  getAll: () => apiGet<Product[]>('/api/inventory/products'),
};
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "fetch(" frontend/src/modules --include="*.tsx"`
- **BLOQUEANTE PR:** nuevo `readEnvelopePayload` duplicado fuera de `apiEnvelope.ts`.

---

## F-03 — i18n trilingüe obligatorio

### RULE
Texto visible al usuario **solo** vía claves i18n en `es.json`, `en.json`, `qu.json`. **Prohibido** strings hardcodeados en JSX salvo nombres propios técnicos. Locale `qu` = Kichwa de Cañar (Ecuador).

### WHY
Producto trilingüe es requisito comercial y legal interno.

### BAD
`<button>Guardar</button>` sin `t('...')`.

### GOOD
`<ZHBtn>{t('common.save')}</ZHBtn>` con claves en los tres JSON.

### ENFORCEMENT
- **BLOQUEANTE PR:** JSX con texto en español literal en archivos `pages/` o `modules/` (revisión + grep heurístico `>[\s]*[A-Za-zÁ-ú]`).
- CI opcional: script detecta claves nuevas solo en un locale.

---

## F-04 — Design System único (UI)

### RULE
Toda pantalla/componente de `frontend/src/modules/**`, `frontend/src/pages/**` y `frontend/src/templates/**` usa los componentes/clases únicos del Design System documentados en [`frontend.md`](./frontend.md#design-system--estándares-únicos-obligatorios): `ZHGrid` (grids de formulario), `ZHToggle` (checkboxes/switches), `.zh-icon-sm/md/lg/xl` (tamaño de íconos, nunca `style={{fontSize}}`), `ZHModal` con prop `footer` (headers/footers de modal — `ZHModalHeader` fue **eliminado**, ver [MODAL-STANDARD](./modal-standard.md), no reintroducir su import), `.prd-tabs`/`.prd-tab-btn` (tabs), `.table` + `.table-scroll` (tablas — `.prd-table-wrap` fue **eliminada** en el bloque 15A, verificado contra `frontend/src/styles/zh-ui.css`, no reintroducir), `.prd-activity__*` (timeline de actividad). **Prohibido** reintroducir los patrones deprecados de esa tabla (`.pg-form-grid*`, `.zh-inline-check`, `.companies-checkbox-label`, `.toggle-ui`, `.md-page-check`, `.prd-icon-sm`, `.md-modal*`, `.prf-modal-header-main`, `ZHModalHeader`, `.prd-table-wrap`, `.zh-form-tabs`, `.md-table*`, `.bod-activity__*`, `style={{fontSize}}` en íconos).

### WHY
Fragmentación del Design System (grids, toggles, modales y tabs duplicados con variantes visuales sutiles) dificultó mantenimiento y QA visual; ver Fase 0-7 de la reescritura de UI (histórico en `docs/archive/`).

### BAD
```tsx
<div className="pg-form-grid pg-form-grid--2">
  <label className="zh-inline-check"><input type="checkbox" />Activo</label>
  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>close</span>
</div>
```

### GOOD
```tsx
<ZHGrid cols={2}>
  <ZHToggle label={t('common.active')} value={active} onChange={setActive} />
  <span className="material-symbols-outlined zh-icon-md">close</span>
</ZHGrid>
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `npm run architecture:design-system` (`tools/architecture/check-design-system.mjs`) — incluido en `npm run architecture:check` (CI). Reglas `F-04-grid`, `F-04-toggle`, `F-04-icon`, `F-04-modal`, `F-04-tabs`, `F-04-table`, `F-04-activity`.
- Legacy permitido temporalmente vía `tools/architecture/architecture-grandfather.json#designSystemGrandfathered` (lista `{file, rules}`). Archivos nuevos o reglas no listadas para un archivo existente → falla CI. Refactors deben reducir esta lista, no ampliarla.
- ESLint (`frontend/eslint.config.js`, `no-restricted-syntax`) bloquea `style={{...}}` en `src/modules/**/components/**`, `src/modules/**/pages/**`, `src/pages/**`, `src/templates/**` (excepción documentada: `src/modules/auth/**`).

---

## F-05 — Alineación de datos numéricos (NUM-001)

> **Revisión 2026-07-08:** esta regla reemplaza la versión anterior de F-05 (que mandaba
> alineación a la **izquierda**). Decisión del responsable de arquitectura: el estándar
> vigente es **alineación a la derecha**. No quedan dos versiones válidas — la izquierda
> queda derogada.

### RULE
Todo dato cuyo propósito principal sea representar un valor numérico (enteros, decimales, monedas, cantidades, stock, costos, precios, totales, subtotales, impuestos, descuentos, porcentajes, pesos, secuenciales numéricos, valores calculados) se muestra **alineado a la derecha** dentro de su componente visual — tablas, formularios, inputs, labels, cards, KPIs, widgets, dashboards, diálogos, tooltips, reportes, PDFs, exportaciones Excel (cuando la tecnología lo permita) y cualquier componente reutilizable, presente o futuro. **Prohibido** `text-align: center` o `text-align: left` en un campo/celda/label numérico salvo excepción documentada, y prohibido resolverlo con estilos inline en vez del Design System.

### WHY
Presentación uniforme y profesional en todo el ERP: el usuario compara magnitudes (montos, cantidades, totales) con el punto decimal como referencia fija, sin que cada pantalla decida su propia alineación.

### EXCEPCIONES (deliberadas, documentadas y aprobadas por arquitectura — nunca por estética)
- Controles especializados donde la alineación forme parte del estándar del propio componente.
- Visualizaciones estadísticas o gráficos.
- Casos donde una normativa externa (SRI, formato legal) obligue otra presentación.
- Aprobación explícita del responsable de arquitectura, documentada en el componente (comentario o ADR).

### BAD
```css
.pdl-input--cost { text-align: left; }
.pdl-input--disc { text-align: left; }
```

### GOOD
```css
.pdl-input--cost { text-align: right; }
.pdl-input--disc { text-align: right; }
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nueva clase CSS, `style={{ textAlign: 'center' | 'left' }}` inline, o componente reutilizable sin soporte de alineación derecha para un campo/celda/label numérico, sin excepción documentada en el propio archivo.
- Si un componente reutilizable del Design System no soporta la regla, se modifica el componente base — nunca se resuelve con una excepción local ni con estilos inline.
- Revisión manual en code review — no hay gate CI automatizado todavía (candidato a `npm run architecture:design-system` en una fase posterior).
- Auditoría obligatoria antes de cerrar cualquier tarea de UI: (1) todo numérico alineado a la derecha, (2) sin inconsistencias entre componentes equivalentes, (3) sin estilos inline agregados para resolverlo, (4) reutiliza infraestructura existente del Design System, (5) consistencia visual mantenida en todo el ERP.

---

# FRONTEND — REACT

## R-01 — Tamaño máximo de componente/página

### RULE
Archivo `.tsx` de página o componente **no supera 400 líneas**. Si supera, dividir en: subcomponentes en `components/`, hooks en `hooks/`, utilidades en `utils/`. **BLOQUEANTE PR** si supera **500 líneas**.

### WHY
Archivos >800 líneas bloquearon code-splitting y revisión humana.

### BAD
`AccountingPage.tsx` monolítico 900 líneas en un solo export.

### GOOD
`AccountingPage.tsx` (layout + tabs) + `AccountingAccountsTab.tsx` + `useAccountingAccounts.ts`.

### ENFORCEMENT
- **BLOQUEANTE PR:** `wc -l` > 500 en `frontend/src/**/*.tsx`.
- Warning CI: > 400 líneas.

---

## R-02 — Sin lógica de negocio en JSX

### RULE
JSX contiene composición y binding. Cálculos fiscales, totales, elegibilidad, permisos efectivos y transformaciones de API viven en hooks, `utils/` o schemas. **Prohibido** bloques `useMemo`/`useEffect` > 40 líneas dentro del componente de página — extraer a hook.

### WHY
JSX con lógica no se testea y duplica reglas backend.

### BAD
```tsx
const total = lines.reduce((a,l) => a + l.qty * l.price * (1+IVA)..., 0); // 20 líneas en JSX
```

### GOOD
```tsx
const { lines, totals } = useInvoiceLines(form);
// totals calculados en hook + schema Zod
```

### ENFORCEMENT
- Revisión PR: rechazar PRs con funciones > 15 líneas definidas dentro del componente de página.
- ESLint custom: `max-lines-per-function` en `pages/` y `modules/**/pages/`.

---

## R-03 — Formularios: Zod + RHF + ZH

### RULE
Formularios persistidos usan schema Zod en `modules/{domain}/schemas/`, `zodResolver`, `useForm`, errores por campo visibles. UI con `ZHField`, `ZHBtn`, `PageShell`, `TableCard`, `.zh-form-tabs` según catálogo. **Prohibido** validación solo en JSX. **Prohibido** `window.prompt` / `window.confirm`.

### WHY
Contrato de validación 4 capas exige schema explícito en frontend.

### BAD
```tsx
if (!email.includes('@')) setError('mal');
```

### GOOD
```tsx
const schema = z.object({ email: z.string().email() });
const { register, formState: { errors } } = useForm({ resolver: zodResolver(schema) });
```

### ENFORCEMENT
- **BLOQUEANTE PR:** formulario POST/PUT sin archivo `*Schema.ts` en módulo.
- **BLOQUEANTE PR:** `grep -r "window\.confirm\|window\.prompt" frontend/src`.

---

## R-04 — Permisos UI no sustituyen autorización

### RULE
`usePermissionsStore`, `PermissionGuard` y checks `hasPerm()` son **solo UX**. **Prohibido** ocultar acciones críticas únicamente en cliente sin endpoint backend autorizado. Toda operación mutante exige policy/`[Authorize]` equivalente en API.

### WHY
Cliente modificado bypassa UI; backend es única autoridad.

### BAD
Solo frontend oculta botón "Anular factura" sin policy en API.

### GOOD
Botón oculto si !perm + `POST /api/.../disable` con `[Authorize(Policy = "...")]` y validator.

### ENFORCEMENT
- Revisión PR: tabla endpoint nuevo ↔ permiso/policy.
- **BLOQUEANTE PR:** endpoint mutante sin `[Authorize]` o policy documentada.

---

# FRONTEND — STATE (ZUSTAND)

## S-01 — Stores pequeños y por responsabilidad

### RULE
Un store Zustand por concern: `authStore`, `permissionsStore`, `accessStore`. **Prohibido** store monolítico > 200 líneas o que mezcle auth + permisos + datos de negocio. Datos de catálogo van en hooks (`useAsync` + servicio), **no** en Zustand salvo sesión/menú/entitlements.

### WHY
Stores grandes causan re-renders globales y estado obsoleto cross-tenant.

### BAD
```typescript
useAppStore — products, customers, auth, ui, modals (800 líneas)
```

### GOOD
```typescript
useAuthStore — token, user, login/logout
useProducts() — hook con useAsync + productService
```

### ENFORCEMENT
- **BLOQUEANTE PR:** store nuevo que persista listas CRUD de negocio.
- Warning: store > 200 líneas.

---

## S-02 — Logout: fullLogout() obligatorio

### RULE
Cierre de sesión, 401 irrecuperable en refresh, e invalidación de impersonation **llaman** `fullLogout()` (`frontend/src/lib/session/fullLogout.ts`). Deben limpiar: stores Zustand, claves `localStorage`/`sessionStorage` de sesión (`auth-storage`, `permissions-storage`, `access-bootstrap`, `erp.saas.*`), y estado de transporte Axios. **Prohibido** logout parcial (solo `navigate('/login')`).

### WHY
Sesiones residuales e impersonation fueron hallazgo P1 en auditoría frontend.

### BAD
```typescript
localStorage.removeItem('token');
navigate('/login');
```

### GOOD
```typescript
import { fullLogout } from '../lib/session/fullLogout';
fullLogout();
navigate('/login');
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nuevo flujo logout sin `fullLogout()`.
- Grep CI: `grep -r "removeItem.*auth" frontend/src` fuera de `fullLogout.ts`.

---

## S-03 — Persistencia Zustand: whitelist explícita

### RULE
Solo persisten en `localStorage` las claves definidas en `sessionStorageKeys.ts` / stores documentados. **Prohibido** persistir `refreshToken`, `bootstrapToken`, certificados, o permisos completos como fuente de autorización. Permisos cacheados son hint; backend revalida.

### WHY
XSS + localStorage = robo de sesión persistente.

### BAD
```typescript
persist({ name: 'full-session', partialize: (s) => s }) // incluye refresh
```

### GOOD
```typescript
// authStore — solo campos acotados; refresh manejado según IDENTITY.md
// bootstrapToken solo en memoria accessStore con TTL de sesión
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nuevo `persist(` sin revisión de seguridad en PR template.
- Revisión contra [IDENTITY.md](../IDENTITY.md).

---

# FRONTEND — SECURITY

## SEC-01 — Tokens: prohibido tratar localStorage como almacén seguro

### RULE
**Prohibido** documentar o implementar “seguridad” basada en ofuscar tokens en localStorage. JWT de acceso en memoria/store acotado según diseño actual; **prohibido** añadir nuevos secretos long-lived en localStorage. Objetivo de hardening: migración hacia httpOnly cookies — no añadir deuda que lo impida.

### WHY
localStorage es legible por cualquier script en la página.

### BAD
```typescript
localStorage.setItem('apiKey', key);
localStorage.setItem('refreshToken', token); // nueva feature
```

### GOOD
Sesión vía patrón existente + refresh en interceptor Axios; secretos solo backend.

### ENFORCEMENT
- **BLOQUEANTE PR:** nuevas claves `localStorage.setItem` con token/secret (grep en diff).
- Script: extensión `check-identity-guardrails.ps1` para patrones JWT en storage.

---

## SEC-01b — Roles: fuente única `SecurityRoles` / `isAdminRole`

### RULE
Comparaciones de rol Admin **solo** vía `ERP.Domain.Kernel.Security.SecurityRoles.Admin` (backend) o `isAdminRole(role)` de `frontend/src/access/permissionUi.ts` (frontend). **Prohibido** literal `"Admin"`/`"admin"`/`role === 'Admin'`/`role.toLowerCase() === 'admin'` ad-hoc fuera de esos puntos únicos. Contrato completo: [SECURITY.md — Security & Access Contract V1 — LOCKED](SECURITY.md#security--access-contract-v1--locked).

### WHY
Comparaciones de rol duplicadas divergen (ej. `ConfigContext` comparaba `role.toLowerCase() === 'admin'` mientras `isAdminRole` usaba `'Admin'` exacto) y generan accesos inconsistentes entre pantallas.

### BAD
```typescript
const canRead = (user?.role ?? '').toLowerCase() === 'admin';
```
```csharp
if (role == "Admin") { /* ... */ }
```

### GOOD
```typescript
const canRead = isAdminRole(user?.role);
```
```csharp
if (string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase)) { /* ... */ }
```

### ENFORCEMENT
- **BLOQUEANTE PR:** nueva comparación de string `"Admin"`/`"admin"`/`"ADMIN"` fuera de `SecurityRoles.cs` (backend) o `permissionUi.ts` (frontend), salvo `Platform.Contracts/Integration/Dtos/CompanyProvisionRequest.cs` (excepción documentada cross-boundary).
- Revisión: nuevo gate de página por rol usa `isAdminRole()`, no lógica propia.

---

## SEC-02 — IDs sensibles fuera de URL

### RULE
**Prohibido** UUID de tenant/company en query string o path compartible cuando el diseño SaaS lo evita. Contexto entre pantallas: `sessionStorage` clave `erp.saas.*` o store. Ver `.cursor/rules/saas-navigation-no-sensitive-url.mdc`.

> **Nota de dominio:** `Subscriber` es terminología exclusiva del Control Plane Platform (gestión de clientes SaaS, fuera del ERP Core — ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)). Dentro del ERP Core el scope equivalente es `Tenant`/`Company`. No mezclar ambos vocabularios en un mismo ejemplo.

### WHY
URLs filtran por historial, analytics y referrers.

### BAD
```typescript
navigate(`/companies?tenantId=${id}`);
```

### GOOD
```typescript
// Nombre ilustrativo — persistir el id fuera de la URL (sessionStorage `erp.saas.*` o store del módulo),
// no en query string. Implementar según el store real del módulo que navega.
persistDetailContext(id);
navigate('/companies');
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "tenantId=\${" frontend/src`.
- Revisión UX copy enlaces compartibles.

---

# FRONTEND — UI (ZH Design System)

## U-01 — ZH es el único estándar visual para código nuevo

### RULE
Componentes y estilos nuevos usan: `components/zh/*`, tokens `design-tokens.css`, utilidades `zh-ui.css`, layout `page-template.css`. **Prohibido** crear archivos en `components/ui/*`. **Prohibido** CSS ad-hoc con colores hex/radios fuera de tokens. **Prohibido** duplicar clases `.btn`, `.badge`, `.table` con otro nombre.

### WHY
UI dual (ui + zh) duplicó mantenimiento y rompió consistencia ERP.

### BAD
```tsx
// components/ui/NewWidget.tsx
<button style={{ background: '#0055ff' }}>
```

### GOOD
```tsx
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
<ZHBtn variant="primary">...</ZHBtn>
```

### ENFORCEMENT
- **BLOQUEANTE PR:** archivo nuevo en `components/ui/`.
- **BLOQUEANTE PR:** CSS página con `#` color literal (grep en `*-page.css` nuevos).
- `components/ui` existente solo delega clases ZH — no expandir API.

---

## U-02 — PageShell y tabs de entidad

### RULE
Catálogos con ficha + listado usan `PageShell`, `TableCard`, `.zh-form-tabs` con orden: Datos → extras → `{modulo}.tabList`. Acciones crear/guardar **solo** en pestaña de datos. Listado usa `{modulo}.tabList`, **no** `app.nav.*`.

### WHY
Copy UX inconsistente rompió capacitación y E2E.

### BAD
Tab listado etiquetada `app.nav.products`; botón Guardar visible en listado.

### GOOD
Tab `products.tabList`; `PageShell action={tab === 'data' ? ... : undefined}`.

### ENFORCEMENT
- Checklist PR frontend (plantilla unificada).
- Revisión manual en pantallas `.zh-form-tabs`.

---

# PERFORMANCE

## P-01 — Lazy loading de rutas

### RULE
Toda ruta de módulo pesado (> 30 KB estimado o página > 400 líneas) se registra con `lazyNamedPage` (`frontend/src/routes/lazyPage.tsx`). Obligatorio para: shell platform, Accounting, Dashboard, catálogos inventario, Companies panel. **Prohibido** import estático de estas páginas en `mainRoutes.tsx` / `catalogRoutes.tsx`.

### WHY
Bundle inicial > 500 KB bloqueó First Load en auditoría.

### BAD
```typescript
import { AccountingPage } from '../pages/AccountingPage';
```

### GOOD
```typescript
const AccountingPage = lazyNamedPage(() => import('../pages/AccountingPage'), 'AccountingPage');
```

### ENFORCEMENT
- **BLOQUEANTE PR:** ruta nueva de módulo ERP sin lazy en archivo de rutas.
- CI: `vite build` + regla tamaño chunk `index-*.js` (warning > 600 KB).

---

## P-02 — Memoización en árboles y tablas

### RULE
Listas > 50 filas, árboles de cuentas/menú, y celdas con formateo costoso usan `memo`, `useMemo`, `useCallback` en el componente hijo estable. **Prohibido** pasar funciones inline no memoizadas a `react-window`/virtualización si causa re-render completo medible.

### WHY
Menú platform y plan de cuentas degradaron interacción sin memo.

### BAD
```tsx
{rows.map(r => <Row onEdit={() => edit(r.id)} />)}
```

### GOOD
```tsx
const onEdit = useCallback((id: string) => ..., []);
<MemoRow onEdit={onEdit} />
```

### ENFORCEMENT
- Revisión PR en componentes `*Tree*`, `*Table*` con > 200 líneas.
- Profiler opcional en PR description para cambios de menú/contabilidad.

---

# PR REVIEW — REGLAS BLOQUEANTES

Un PR **falla** si contiene cualquiera de:

| # | Condición |
|---|-----------|
| PR-1 | `ErpDbContext` inyectado o usado en `ERP.API` controllers |
| PR-2 | `IgnoreQueryFilters()` fuera de `PlatformQueryAccessor` / tests |
| PR-3 | Entidad multi-tenant sin filtro EF o índice único sin scope |
| PR-4 | Command/Query persistido sin FluentValidation |
| PR-5 | Nuevo servicio o schema en raíz `frontend/src/services` o `schemas` (no adapter) |
| PR-6 | Página nueva > 15 líneas en `frontend/src/pages/` (no wrapper) |
| PR-7 | Componente `.tsx` > 500 líneas |
| PR-8 | Logout sin `fullLogout()` |
| PR-9 | Nuevo `localStorage`/`sessionStorage` con token, refresh o bootstrap persistente |
| PR-10 | `?tenantId=` / UUID sensible en URL nueva |
| PR-11 | Archivo nuevo en `components/ui/` |
| PR-12 | Endpoint mutante sin autorización backend |
| PR-13 | Validación solo frontend para dato persistido |
| PR-14 | AutoMapper introducido o dependencia cruzada Application↔Application entre módulos |
| PR-15 | Herramienta fuera de `docs/DEVELOPMENT.md#stack-oficial` / `stack-allowlist.json` |
| PR-16 | `new ApiResponse<T>`/mensaje hardcodeado en controller, handler con `MessageCatalog.*`, o `code` nuevo sin entrada en `MessageCatalog` |
| PR-17 | `correlationId` generado fuera de `RequestCorrelationMiddleware` (`Guid.NewGuid()` para este fin, `context.TraceIdentifier` leído directo fuera del middleware, o header `X-Correlation-Id` escrito en otro archivo); cambio a la forma de `ApiResponse<T>`/`ApiResponseMessage`/`ApiResponseMeta` o a los nombres JSON del envelope sin revisión arquitectónica (ver "API Response Contract V1 — LOCKED" en BACKEND-RULES.md) |
| PR-18 | Comparación de rol Admin ad-hoc (`"Admin"`/`"admin"` literal, `role.toLowerCase() === 'admin'`) fuera de `SecurityRoles` (backend) / `isAdminRole()` (frontend); nueva fuente de verdad de autorización UI fuera del modelo descrito en "Security & Access Contract V1 — LOCKED" (SECURITY.md) |

Plantilla PR debe incluir sección:

```markdown
## Architecture checklist
- [ ] Capas backend respetadas (B-01–B-06)
- [ ] CQRS + validator (C-01–C-04)
- [ ] Multi-tenant (M-01–M-04)
- [ ] Frontend modular (F-01–F-03)
- [ ] Sin violaciones bloqueantes PR-1–PR-15

## API response contract checklist (PR-16/PR-17)
- [ ] Respuesta usa `ApiResultExtensions` (`ApiOk`/`ApiCreated`/`ToOkOrBadRequest`/...) — sin `new ApiResponse<T>` manual
- [ ] Si se agrega un `code` nuevo en `ApiResponseCodes` (en `Common` o en una clase anidada por dominio), existe su entrada correspondiente en `MessageCatalog`
- [ ] No hay mensajes de usuario hardcodeados en controller/handler (`message.user` viene siempre de `MessageCatalog` vía `code`)
- [ ] Detalle dinámico de instancia (validación, valor concreto) va en `data.errors: string[]`, no en `message.user`
- [ ] `correlationId` no se genera fuera de `RequestCorrelationMiddleware` (sin `Guid.NewGuid()`/`TraceIdentifier` directo en otros archivos)
- [ ] Tests agregados/actualizados (`ApiResponseContractTests`, `ApiResponseContractSnapshotTests`, `ResponseFactoryConsistencyTests`, `CorrelationGovernanceTests` si aplica)
- [ ] Contrato JSON no roto: envelope sigue siendo `{code, severity, message:{user,dev}, data, meta}`
```

---

# AUTOMATED GUARDRAILS

## Implementados hoy

| Guardrail | Comando / ubicación | Reglas |
|-----------|---------------------|--------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` | Stack, PR-15 |
| Identity legacy | `tools/architecture/check-identity-guardrails.ps1` | SEC-01 |
| Handler size | `tools/quality/check-handler-size.ps1` | C-03 |
| Architecture grep + limits | `tools/architecture/check-architecture-guardrails.ps1` | B-05, F-01, M-02, PR-1–PR-10, P-01 |
| Grandfather legacy | `tools/architecture/architecture-grandfather.json` | Deuda documentada |
| NetArchTest capas | `backend/src/ERP.Architecture.Tests` | B-01, B-03, B-05 |
| API response contract (Reglas A-D) | `ERP.Architecture.Tests/ApiResponseContractTests.cs` | B-03, PR-16 |
| ResponseFactory/ExceptionMiddleware consistency | `ERP.API.Tests/ResponseFactoryConsistencyTests.cs` | PR-16 |
| API response contract snapshot (forma exacta del JSON, camelCase, sin campos legacy) | `ERP.API.Tests/ApiResponseContractSnapshotTests.cs` | PR-16, PR-17 |
| Correlation ID governance (única fuente, sin TraceIdentifier/Guid.NewGuid fuera del middleware) | `ERP.Architecture.Tests/CorrelationGovernanceTests.cs` | PR-17 |
| IgnoreQueryFilters audit | `ERP.Infrastructure.Tests/Persistence/IgnoreQueryFiltersAuditTests.cs` | M-02, PR-2 |
| Unit tests sesión | `frontend/src/lib/session/fullLogout.test.ts` | S-02 |
| Unit tests envelope | `frontend/src/modules/lib/apiEnvelope.test.ts` | F-02 |
| ESLint max-lines (warn) | `frontend/eslint.config.js` → `modules/**/pages` | R-01 |
| CI workflow | `.github/workflows/ci.yml` | Todos los anteriores |
| Cursor rules | `.cursor/rules/*.mdc` | Refuerzo por glob |

## CI (GitHub Actions)

| Job | Paso | Script / test |
|-----|------|----------------|
| Backend | Restore, build, test | `dotnet test ERP.slnx` (incluye `ERP.Architecture.Tests`) |
| Backend | Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk` |
| Backend | Identity | `tools/architecture/check-identity-guardrails.ps1` |
| Frontend | ESLint | `npm run lint` (max-lines warn en pages) |
| Frontend | Build + chunk | `npm run build` + `tools/architecture/check-architecture-guardrails.ps1 -FrontendChunkOnly` |

## Pendiente / mejora continua

| Guardrail | Notas |
|-----------|--------|
| Reducir grandfather | Partir handlers/páginas legacy hasta vaciar `architecture-grandfather.json` |
| ESLint max-lines error 500 | Activar cuando no queden pages > 500 fuera de grandfather |

## NetArchTest (backend) — implementado

Proyecto `backend/src/ERP.Architecture.Tests` (`LayerDependencyTests`, `ApiControllerGuardrailTests`).

## Grep / scripts locales

Equivalente a pre-commit (sin Husky): ejecutar desde raíz del repo:

```powershell
./tools/quality/check-handler-size.ps1
./tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk
# Tras npm run build en frontend:
./tools/architecture/check-architecture-guardrails.ps1 -FrontendChunkOnly
```

---

# EXCEPCIONES

1. Excepción **debe** documentarse en el PR: regla violada, razón, plan de remediación con fecha.
2. Máximo **30 días** para remediar excepciones temporales; después se trata como deuda bloqueante.
3. Excepciones de seguridad (M-*, SEC-*, S-02) **no** se aprueban sin sign-off explícito de arquitectura.

---

# MANTENIMIENTO

- Cambios a reglas bloqueantes: PR dedicado + entrada en [STATUS.md](../STATUS.md).
- Cursor/Claude: referenciar este archivo en `CONTEXT.md` y `.cursor/rules/rules-consolidated-map.mdc`.
- Revisión trimestral: alinear con auditorías y métricas CI.

**Última actualización:** 2026-06-13 · Versión 1.1 (SEC-01b / PR-18 — Security & Access Contract V1)
