# REGLAS DEL PROYECTO — ERP SaaS ZH Technologies

Reglas de implementación. Para arquitectura → `docs/ARCHITECTURE.md`. Para estado → `docs/STATUS.md`. Para funcionalidades → `docs/FEATURES.md`.

## ⚡ Regla obligatoria al terminar cualquier tarea

**Al completar un proceso o funcionalidad, SIEMPRE actualizar `PROGRESS.html`:**
1. Marcar el ítem correspondiente como completado (checkbox).
2. Actualizar la fecha `last-updated` en el HTML.
3. Si el ítem era "Parcial", cambiar su `data-val` a `"1"` y la clase del badge a `s-done`.
4. Abrir el archivo en el navegador para verificar que el porcentaje general suba correctamente.

---

## Arranque rápido

```powershell
docker compose up -d
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http          # http://localhost:5003  swagger: /swagger
cd frontend && npm run dev                                   # http://localhost:5173
```

---

## Reglas de backend — lo que no se rompe

### Capas (dependencias solo hacia abajo)
```
ERP.API → ERP.Application → ERP.Domain ← ERP.Infrastructure
```
- `ERP.Domain`: cero dependencias de frameworks externos
- `ERP.API`: solo HTTP; cero entidades de dominio en contratos; cero lógica de negocio
- `ERP.Application`: solo casos de uso; cero acceso a HTTP ni BD directo
- `ERP.Infrastructure`: implementa contratos; cero reglas de negocio

### Patrones obligatorios

**Entidades — siempre factory, nunca `new` público:**
```csharp
var p = Producto.Create("X", tenantId, actorId);  // ✅
var p = new Producto { Nombre = "X" };             // ❌
```

**Soft delete — nunca DELETE físico:**
```csharp
producto.Disable();   // IsActive = false  ✅
db.Remove(producto);  // ❌ (salvo decisión explícita de producto)
```

**Sin AutoMapper — mapeos manuales en handlers.**

**Sin dependencias cruzadas entre módulos:**
```csharp
// ✅ Usar contrato de dominio
ICustomerRepository repo  

// ❌ Importar handler de otro módulo
using ERP.Application.Modules.Customers.UseCases.GetCustomer;
```

**Resultado — `Result<T>`, nunca lanzar al controller:**
```csharp
return Result<ProductDto>.Failure("Código duplicado.");  // ✅
throw new Exception("Código duplicado.");               // ❌
```

### Controllers — ApiResultExtensions (obligatorio)
```csharp
return this.ToOkOrBadRequest(result, "OK");      // ✅
return this.ToCreatedOrBadRequest(result, "Creado");
return this.ToOkOrNotFound(result);

return Ok(new ApiResponse<T> { … });             // ❌ nunca manual
```

### Status HTTP

| Caso | Status |
|------|--------|
| Éxito lectura | 200 |
| Éxito creación | 201 |
| Regla de negocio / entrada inválida | 400 |
| Sin autenticación | 401 |
| Sin permiso | 403 |
| No encontrado | 404 |
| `ValidationException` FluentValidation | **422** (ExceptionMiddleware) |

Declarar `[ProducesResponseType]` por cada status que aplique.

### Multi-tenant — reglas

- Toda entidad de negocio tiene `TenantId: Guid` con query filter en `OnModelCreating`.
- Índices únicos compuestos siempre incluyen `TenantId`: `(TenantId, Code)`.
- Nunca unicidad global en entidades multi-tenant.
- Al agregar entidad con `TenantId`: registrar filtro en `ErpDbContext.OnModelCreating`.

---

## Reglas de frontend

### Estructura de módulo (patrón obligatorio)
```
modules/{dominio}/
├── api/          ← service.ts (llamadas HTTP)
├── schemas/      ← schema Zod
├── hooks/        ← useAsync + estado
└── pages/        ← página + CSS único (prefijo propio)
```

### Permisos en UI (solo conveniencia — autorización real en backend)
```typescript
const isAdmin = role === 'Admin' || role === 'SuperAdmin';
const canView   = isAdmin || hasPerm('modulo.recurso.view');
const canCreate = isAdmin || hasPerm('modulo.recurso.create');
```

### Formularios — componentes obligatorios
```tsx
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';

<ZHField label="RUC" required error={errors.ruc?.message}>
  <input className="zh-input" {...register('ruc')} />
</ZHField>
<ZHBtn variant="primary" size="md" type="submit">Guardar</ZHBtn>
```

---

## Reglas CSS — no duplicar nunca

### Jerarquía de 3 niveles
```
design-tokens.css    → variables (--color-*, --space-*, --text-*)
zh-ui.css            → componentes globales (.table, .badge, .zh-btn…)
page-template.css    → layout global (.pg-page, .pg-kpi, .pg-section…)
{pagina}-page.css    → SOLO clases únicas de esa pantalla
```

**Antes de escribir CSS local:** verificar si ya existe la clase en `zh-ui.css` o `page-template.css`.

### Prefijos por página
| Página | Prefijo |
|--------|---------|
| Proveedores | `prv-*` |
| Clientes | `cls-*` |
| Productos | `prd-*` |
| Dashboard | `dsh-*` |
| Reportes | `rpt-*` |
| SuperAdmin Planes | `sap-*` |

### Clases más usadas (no recrear)
```
Layout:    .pg-page  .pg-header-row  .pg-section  .pg-kpis  .pg-kpi  .pg-kpi--h
           .pg-table-controls  .pg-search  .pg-form-grid  .pg-form-grid--2/3/4
Tabla:     .table
Badges:    .badge  .badge--green/red/gray/orange/blue  .badge--md  .badge--upper
Estado:    .zh-status  .zh-status--active/suspended/inactive/pending
Botones:   .zh-btn  .zh-btn--primary/ghost/destructive  .zh-btn--sm/md/lg
Modal:     .zh-modal-overlay  .zh-modal  .zh-modal-header  .zh-modal-body
Tabs:      .zh-form-tabs  (botones con .is-active)
Input:     .zh-input
Avatar:    .zh-avatar  .zh-avatar--square
Checkbox:  .zh-inline-check
KPI icon:  .pg-kpi-icon--primary/success/warning/error
```

---

## Validación en 4 capas (obligatorio para datos persistidos)

| Capa | Herramienta | Clave |
|------|-------------|-------|
| Frontend | Zod + react-hook-form | Schema en `schemas/{modulo}/`; `zodResolver`; error visible por campo |
| Application | FluentValidation + MediatR | `[Nombre]Validator` por Command/Query; `ValidationBehavior` en pipeline |
| Domain | Guard clauses + factories | `DomainException` en invariantes; `Create(...)` |
| BD | EF Core `IEntityTypeConfiguration` | `IsRequired`, `HasMaxLength`; índices únicos con `TenantId` |

**Prohibido:** validar solo en frontend para datos que se guardan en servidor.

---

## i18n — Kichwa de Cañar

- Toda clave nueva va en **los tres archivos**: `es.json`, `en.json`, `qu.json`.
- Contenido `qu`: Kichwa de Cañar, Ecuador — no quechua genérico.
- Prohibido texto duro visible al usuario en UI.

---

## Menú de navegación

- Cada `to` aparece **máximo una vez** entre grupos estáticos.
- Rutas `/superadmin/*` y `/companies`: solo en `getSuperAdminPanelNavExtras`, nunca en BD.
- Al añadir ruta: registrar alias en `MENU_ROUTE_ALIASES` si tiene variante legacy.

---

## SaaS — reglas específicas

- **IDs de tenant fuera de la URL:** usar `sessionStorage` con clave `erp.saas.*`, nunca `?tenantId=`.
- **Módulo nuevo:** preguntar en qué planes SaaS debe incluirse antes de dar por cerrado. No asumir "todos los planes".
- **Tarifas SRI:** no existe formulario para crear tarifas — vienen de `sri_vat_rate`. El endpoint `POST /api/tax-rates` fue eliminado. Usar `GET /api/tax-rates` para poblar dropdowns.

---

## CI y ramas

| Rama | Uso |
|------|-----|
| `main` | Integración estable |
| `development` | Features diarias |
| `release/*` | Estabilización |
| `hotfix/*` | Correcciones urgentes |

Tests antes de merge:
```powershell
cd backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj
cd frontend && npx tsc --noEmit && npm run build
```
