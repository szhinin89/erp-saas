# Reglas de arquitectura enterprise — ERP SaaS ZH Technologies

> **Fuente canónica PR (B-xx / F-xx).** Adaptador de entrada: [`docs/ARCHITECTURE-RULES.md`](../docs/ARCHITECTURE-RULES.md). Índice general: [README.md](./README.md).

**Autoridad:** documento normativo. Prevalece sobre convenciones informales, comentarios sueltos y preferencias personales cuando hay conflicto con seguridad, tenant o capas.

**Audiencia:** Cursor AI, Claude Code, Copilot, revisores PR, auditoría técnica.

**Violación:** cualquier incumplimiento de regla marcada **BLOQUEANTE** impide merge hasta corrección o excepción documentada en el PR (issue link + fecha + responsable).

**Relacionados (descriptivos, no sustitutos):** [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md), [`docs/DEVELOPMENT.md`](../docs/DEVELOPMENT.md), [`docs/IDENTITY.md`](../docs/IDENTITY.md), [`docs/DATABASE.md`](../docs/DATABASE.md), [`CLAUDE.md`](../CLAUDE.md), [AI-RULES/](./README.md), `.cursor/rules/`.

---

## 0. Precedencia

| Orden | Fuente | Aplica cuando |
|-------|--------|----------------|
| 1 | Seguridad / multi-tenant / billing | Siempre |
| 2 | Este documento (`AI-RULES/PR-RULES-CATALOG.md`) | Diseño e implementación |
| 3 | Otros `AI-RULES/*` por área | Detalle operativo |
| 4 | `.cursor/rules/*.mdc` | Hints Cursor por glob |
| 5 | [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) | Contexto y diagramas |

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
  return this.ToCreatedOrBadRequest(result, "Creado");
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
Todo dato persistido valida en: (1) Frontend Zod + RHF, (2) FluentValidation, (3) Domain factory/guard clauses, (4) EF `IEntityTypeConfiguration`. **Prohibido** validar solo en frontend o solo en controller. Unicidad multi-tenant: índice compuesto con `TenantId`/`CompanyId`/`SubscriberId` según scope — **prohibido** unicidad global sin clave de scope.

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
Toda entidad de negocio operativa implementa el contrato de scope del producto (`ISubscriberScopedEntity`, `ICompanyScopedEntity`, etc.) y tiene query filter global registrado en `ErpDbContext.OnModelCreating`. Toda query de datos de tenant **incluye** filtro por scope en repositorio o filter global — **prohibido** asumir “single tenant”.

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
  return await _db.Subscribers.ToListAsync(ct);
});
```

### ENFORCEMENT
- **BLOQUEANTE PR:** `grep -r "IgnoreQueryFilters" backend/src` fuera de `PlatformQueryAccessor` y tests.
- Revisión obligatoria de seguridad en cada match.

---

## M-03 — TenantId / CompanyId / SubscriberId: JWT es la fuente

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
Unicidad de negocio **siempre** incluye columna de scope (`TenantId`, `CompanyId`, o `(SubscriberId, ...)`). **Prohibido** `HasIndex(x => x.Code).IsUnique()` sin scope en tablas multi-tenant.

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
Toda feature nueva (API client, schema Zod, hooks, pages, componentes de dominio) vive bajo `frontend/src/modules/{domain}/` con estructura mínima: `api/`, `schemas/`, `hooks/`, `pages/`, `components/` según aplique. **Prohibido** añadir lógica nueva en `frontend/src/pages/` (solo re-export wrapper ≤ 10 líneas). **Prohibido** crear archivos en `frontend/src/services/` o `frontend/src/schemas/` (solo adapters `@deprecated` hacia módulos).

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
Llamadas HTTP usan `frontend/src/modules/lib/api.ts` (Axios + refresh). Deserialización de envelope con `readEnvelopePayload` / helpers `apiGet|Post|Put|Patch|Delete` en `apiEnvelope.ts`. **Prohibido** `fetch()` directo en pages. **Prohibido** duplicar lógica `responseObject` en servicios.

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
- Revisión contra [IDENTITY.md](./IDENTITY.md).

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

## SEC-02 — IDs sensibles fuera de URL

### RULE
**Prohibido** UUID de tenant/company/subscriber en query string o path compartible cuando el diseño SaaS lo evita. Contexto entre pantallas: `sessionStorage` clave `erp.saas.*` o store. Ver `.cursor/rules/saas-navigation-no-sensitive-url.mdc`.

### WHY
URLs filtran por historial, analytics y referrers.

### BAD
```typescript
navigate(`/companies?tenantId=${id}`);
```

### GOOD
```typescript
persistCompaniesDetailSubscriberId(id);
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

Plantilla PR debe incluir sección:

```markdown
## Architecture checklist
- [ ] Capas backend respetadas (B-01–B-06)
- [ ] CQRS + validator (C-01–C-04)
- [ ] Multi-tenant (M-01–M-04)
- [ ] Frontend modular (F-01–F-03)
- [ ] Sin violaciones bloqueantes PR-1–PR-15
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

- Cambios a reglas bloqueantes: PR dedicado + entrada en [STATUS.md](./STATUS.md).
- Cursor/Claude: referenciar este archivo en `CONTEXT.md` y `.cursor/rules/rules-consolidated-map.mdc`.
- Revisión trimestral: alinear con auditorías y métricas CI.

**Última actualización:** 2026-05-21 · Versión 1.0
