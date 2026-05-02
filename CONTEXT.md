# CONTEXT.md — ZH Technologies ERP
> Fuente de verdad para Cursor AI.
> Última actualización: 28/04/2026
> ⚠️ Lee este archivo COMPLETO antes de generar cualquier código.

---

## 🏗️ Arquitectura: Monolito Modular Vertical

```
C:\ProyectCursor\erp-saas\
├── .cursorrules
├── backend/
│   ├── ERP.slnx
│   └── src/
│       ├── ERP.API/                          ← Host HTTP, Controllers, Program.cs
│       ├── ERP.Shared/                       ← Shared Kernel (BaseEntity, interfaces comunes)
│       ├── Modules.Accounting/               ← Módulo Contabilidad (completo)
│       └── Modules.Products/                 ← Módulo Productos (estructura lista)
├── database/
├── docs/
│   └── zh-form-template/
│       ├── README.md
│       └── zh_erp_component_library.html     ← Plantilla visual ZH Form (referencia, abrir en navegador)
└── frontend/
```

**Plantilla visual ZH Form:** `docs/zh-form-template/zh_erp_component_library.html` (HTML estático + CSS embebido). Regla Cursor: `10-zh-form-system.mdc`.

### 🌐 Tercer idioma en la UI (Kichwa Cañar)

- Archivo `frontend/src/i18n/locales/qu.json` y código de locale **`qu`** (localStorage / tipo `Locale`): el contenido es **Kichwa de la provincia de Cañar, Ecuador**, no “quechua” genérico ni variantes de otros países.
- Convención y copy para agentes: `.cursor/rules/17-kichwa-canar-locale.mdc` y bullet en `.cursor/rules/00-always.mdc`.
- Selector de idioma: claves `app.langMenu.*` en `es.json` / `en.json` / `qu.json` (Español+Inglés+Kichwa · Spanish+English+Kichwa · con UI en kichwa: Kastilla shimi, Inglés shimi, Kichwa shimi).

---

## 📐 Reglas de arquitectura — NUNCA violar

1. **Estructura vertical**: todo el código de negocio vive en `Modules.*`
2. **Sin comunicación directa** entre módulos — usar interfaces o eventos (MediatR)
3. **Multi-tenant obligatorio**: toda entidad hereda `BaseEntity` que tiene `TenantId`
4. **Sin DbContext fuera de Infrastructure** de cada módulo
5. **Sin entidades de dominio en la API** — siempre DTOs
6. **Soft delete**: nunca borrar registros, usar `IsActive = false`
7. **No AutoMapper** — mappings manuales en los casos de uso

---

## 📦 Dependencias entre proyectos

```
ERP.Shared          → no depende de nadie
Modules.*           → depende de ERP.Shared
ERP.API             → depende de ERP.Shared + todos los Modules.*
```

Módulos entre sí: NUNCA dependencia directa.

---

## ✅ Estado actual — Lo que YA existe (NO regenerar)

### ERP.Shared
```
src/ERP.Shared/
├── Domain/
│   ├── BaseEntity.cs          ✅ Id, TenantId, CreatedAt, UpdatedAt, IsActive
│   └── ITenantContext.cs      ✅ interface Guid TenantId { get; }
├── Persistence/
│   └── IUnitOfWork.cs         ✅ SaveChangesAsync
└── Extensions/                (vacío, listo para usar)
```

### Modules.Accounting — COMPLETO Y FUNCIONANDO
```
src/Modules.Accounting/
├── Domain/
│   ├── Entities/
│   │   └── Account.cs         ✅ Factory method Create(), hereda BaseEntity
│   ├── ValueObjects/
│   │   ├── AccountType.cs     ✅ Asset, Liability, Equity, Revenue, Expense
│   │   └── AccountNature.cs   ✅ Debit, Credit
│   ├── Events/                (listo para domain events)
│   └── Interfaces/            (listo para interfaces de dominio)
├── Application/
│   ├── DTOs/
│   │   ├── AccountDto.cs              ✅
│   │   ├── CreateAccountRequest.cs    ✅
│   │   └── UpdateAccountRequest.cs    ✅
│   ├── Interfaces/
│   │   └── IAccountRepository.cs      ✅
│   └── UseCases/
│       ├── GetAllAccountsUseCase.cs   ✅
│       └── CreateAccountUseCase.cs    ✅
├── Infrastructure/
│   ├── Configurations/
│   │   └── AccountConfiguration.cs   ✅ EF Core, tabla "accounts"
│   ├── Persistence/
│   │   └── AccountingDbContext.cs     ✅
│   ├── Repositories/
│   │   └── AccountRepository.cs      ✅ filtra por TenantId
│   └── AccountingDependencyInjection.cs ✅ AddAccountingModule()
└── Migrations/
    └── 20260428192052_CreateAccountTable ✅ APLICADA en PostgreSQL
```

### ERP.API — OPERATIVO
```
src/ERP.API/
├── Controllers/
│   └── AccountsController.cs  ✅ GET /api/v1/accounts, POST /api/v1/accounts
├── Program.cs                 ✅ registra AddAccountingModule()
└── appsettings.json           ✅ ConnectionStrings configurado
```

---

## 🔴 Lo que FALTA — en orden de prioridad

### PRIORIDAD 1 — Completar Accounts (faltan endpoints)
**Agregar a:** `ERP.API/Controllers/AccountsController.cs`
- `GET api/v1/accounts/{id}` → `GetAccountByIdUseCase`
- `PUT api/v1/accounts/{id}` → `UpdateAccountUseCase`
- `DELETE api/v1/accounts/{id}` → soft delete (`IsActive = false`)

**Crear en:** `Modules.Accounting/Application/UseCases/`
- `GetAccountByIdUseCase.cs`
- `UpdateAccountUseCase.cs`

---

### PRIORIDAD 2 — Módulo Products
Estructura lista en `src/Modules.Products/`. Seguir exactamente el mismo
patrón que `Modules.Accounting`.

**Archivos a crear:**

```
Modules.Products/
├── Domain/
│   └── Entities/
│       └── Product.cs          ← hereda BaseEntity, factory method Create()
├── Application/
│   ├── DTOs/
│   │   ├── ProductDto.cs
│   │   ├── CreateProductRequest.cs
│   │   └── UpdateProductRequest.cs
│   ├── Interfaces/
│   │   └── IProductRepository.cs
│   └── UseCases/
│       ├── GetAllProductsUseCase.cs
│       ├── GetProductByIdUseCase.cs
│       ├── CreateProductUseCase.cs
│       └── UpdateProductUseCase.cs
├── Infrastructure/
│   ├── Configurations/
│   │   └── ProductConfiguration.cs
│   ├── Persistence/
│   │   └── ProductsDbContext.cs
│   ├── Repositories/
│   │   └── ProductRepository.cs
│   └── ProductsDependencyInjection.cs  ← AddProductsModule()
```

Después crear migración:
```bash
dotnet ef migrations add CreateProductTable --project src/Modules.Products --startup-project src/ERP.API --context ProductsDbContext
dotnet ef database update --project src/Modules.Products --startup-project src/ERP.API --context ProductsDbContext
```

Registrar en `Program.cs`:
```csharp
builder.Services.AddProductsModule(builder.Configuration);
```

---

### PRIORIDAD 3 — Auth + TenantId desde JWT
Actualmente el `TenantId` viene del header `X-Tenant-Id`.
En el futuro debe venir del JWT token via `ITenantContext`.

---

## 📋 Patrones de referencia

Para cualquier módulo nuevo, copiar exactamente la estructura de `Modules.Accounting`:
- Entidad → `Account.cs`
- Repository interface → `IAccountRepository.cs`
- Repository impl → `AccountRepository.cs`
- DbContext → `AccountingDbContext.cs`
- DI → `AccountingDependencyInjection.cs`
- Controller → `AccountsController.cs`

---

## Primer clone (config local, no versionada)

1. Copiar `backend/src/ERP.API/appsettings.Development.json.example` → `backend/src/ERP.API/appsettings.Development.json` y definir cadena PostgreSQL + `Jwt:SecretKey` (solo en tu máquina).
2. Opcional: `frontend/.env.development.example` → `frontend/.env.development` si necesitas `VITE_API_URL` explícito.

---

## ⚙️ Comandos útiles

```bash
# Levantar la API
cd C:\ProyectCursor\erp-saas\backend
dotnet run --project src/ERP.API

# Build completo
dotnet build

# Nueva migración para un módulo
dotnet ef migrations add {Nombre} --project src/Modules.{Modulo} --startup-project src/ERP.API --context {Modulo}DbContext

# Aplicar migración
dotnet ef database update --project src/Modules.{Modulo} --startup-project src/ERP.API --context {Modulo}DbContext
```

## 🧪 Probar endpoints (PowerShell)

```powershell
# GET todos
Invoke-RestMethod -Method GET `
  -Uri "http://localhost:5019/api/v1/accounts" `
  -Headers @{ "X-Tenant-Id" = "00000000-0000-0000-0000-000000000001" }

# POST crear cuenta
Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5019/api/v1/accounts" `
  -Headers @{ "X-Tenant-Id" = "00000000-0000-0000-0000-000000000001" } `
  -Body '{"code":"1","name":"ACTIVO","type":0,"nature":0,"allowsMovement":false,"level":1}' `
  -ContentType "application/json"
```

---

## 🔮 Módulos futuros (NO construir todavía)
- `Modules.Invoicing` — Facturación electrónica
- `Modules.Orders` — Órdenes de compra/venta
- `Modules.Auth` — JWT + resolución de TenantId
- `Modules.Inventory` — Control de stock
