# ERP SaaS — Guía para desarrolladores

## ¿Qué es este proyecto?

ERP multi-tenant en SaaS. Backend en **.NET 10 (Clean Architecture)**, frontend en **React 19 + TypeScript + Vite**. Cada empresa (tenant) ve solo sus datos gracias a filtros globales en EF Core.

---

## Levantar el entorno en desarrollo

### Requisitos previos
- Docker Desktop corriendo
- .NET 10 SDK
- Node.js 20+

### 1. Base de datos (PostgreSQL en Docker)

```powershell
# El contenedor debe llamarse postgreszh
docker ps --filter "name=postgreszh"
# Si no está corriendo: ver backend/scripts/erp_setup.ps1
```

Credenciales de desarrollo:
- Host: `localhost:5435`
- DB: `dberpsaas`
- User: `postgres` / Pass: `zhin@2024`

### 2. Backend

```powershell
cd backend/src
dotnet run --project ERP.API --launch-profile http
# Escucha en http://localhost:5003
# Swagger: http://localhost:5003/swagger
```

### 3. Frontend

```powershell
cd frontend
npm install
npm run dev
# Escucha en http://localhost:5173
```

---

## Arquitectura del backend

```
ERP.Domain          ← Entidades, Value Objects, interfaces de repos, eventos
ERP.Application     ← Handlers (casos de uso), DTOs, contratos
ERP.Infrastructure  ← EF Core, repositorios concretos, JwtService, CurrentTenantService
ERP.API             ← Controllers, Middleware, Program.cs
```

**Regla de dependencias:** Domain ← Application ← Infrastructure ← API.
El dominio no conoce EF Core ni ASP.NET.

### Cómo agregar un nuevo caso de uso

1. Crear `Command` o `Query` record en `ERP.Application/Modules/{Modulo}/UseCases/{Nombre}/`
2. Crear `Handler` en la misma carpeta. El handler se registra automáticamente (assembly scan).
3. Agregar el endpoint en el controller correspondiente en `ERP.API/Controllers/`.

**No es necesario tocar `Program.cs`** — `AddApplication()` registra todos los handlers via reflection.

### Multi-tenant

Todos los modelos con `TenantId` tienen un **query filter global** en `ErpDbContext`.
El tenant se resuelve desde el claim `tenant_id` del JWT en cada request via `ICurrentTenant`.

```
JWT claim tenant_id → CurrentTenantService.TenantId → ErpDbContext.CurrentTenantId → query filter
```

El filtro se evalúa **por instancia de DbContext** (no en tiempo de compilación del modelo). Si se agrega una nueva entidad con `TenantId`, agregar el filtro en `ErpDbContext.OnModelCreating`.

### Result<T>

Los handlers retornan `Result<T>` en lugar de lanzar excepciones para errores de negocio.

```csharp
// En el handler:
return Result<ProductDto>.Failure("El código ya existe.");

// En el controller:
return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
```

---

## Arquitectura del frontend

```
src/
├── lib/api.ts           ← Cliente Axios con interceptor JWT y redirect 401
├── store/authStore.ts   ← Estado global de autenticación (Zustand + localStorage)
├── hooks/useAsync.ts    ← Hook genérico para fetch con loading/error/refetch
├── services/            ← Una función por endpoint de la API
├── types/               ← Interfaces TypeScript que reflejan los DTOs del backend
├── components/          ← Componentes reutilizables (Modal, PageShell, AppLayout)
└── pages/               ← Una página por ruta
```

### Cómo agregar una nueva página

1. Crear el tipo en `src/types/{modulo}.ts` (debe coincidir con el DTO del backend)
2. Crear el servicio en `src/services/{modulo}Service.ts`
3. Crear la página en `src/pages/{Nombre}Page.tsx`
4. Agregar la ruta en `src/App.tsx` y el ítem de nav en `src/components/AppLayout.tsx`

### Autenticación

El token JWT se almacena en `localStorage` bajo la clave `auth-storage` (Zustand persist).
El interceptor de Axios lo inyecta automáticamente en cada request. En caso de 401, limpia el estado y redirige a `/login`.

---

## Módulos actuales

| Módulo      | Backend endpoints                         | Frontend              |
|-------------|-------------------------------------------|-----------------------|
| Auth        | POST /api/auth/register, /login           | LoginPage             |
| Products    | GET/POST /api/products                    | ProductsPage          |
| Accounting  | GET/POST /api/accounts, /journal-entries  | AccountingPage        |
| Tenants     | POST /api/tenants                         | —                     |

---

## Comandos útiles

```powershell
# Backend — build
cd backend/src && dotnet build ERP.slnx

# Backend — tests
cd backend/src && dotnet test ERP.slnx

# Backend — nueva migración EF Core
cd backend/src/ERP.Infrastructure
dotnet ef migrations add NombreMigracion --startup-project ../ERP.API

# Frontend — type check
cd frontend && npx tsc --noEmit

# Frontend — build producción
cd frontend && npm run build
```
