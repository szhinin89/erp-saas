# ERP SaaS — Desarrollo y operación local

Documento **unificado** de desarrollo y operación local para este monorepo.

**Ver también:** `docs/ARCHITECTURE.md` (capas y multi-tenant), [`docs/adr/README.md`](adr/README.md) (decisiones ADR), `docs/STATUS-2026-05-ERP.md` (estado reciente), `docs/FRONTEND-PANTALLAS.md` (rutas UI), `docs/developer-reference.html` (referencia amplia en navegador), `.cursor/rules/erp-unified-rules.mdc` (reglas de implementación).

---

## Tabla de contenidos

1. [Qué es este proyecto](#qué-es-este-proyecto)
2. [Prerrequisitos](#prerrequisitos)
3. [Base de datos (Docker)](#base-de-datos-docker)
4. [Migraciones EF Core](#migraciones-ef-core)
5. [Arrancar backend y frontend](#arrancar-backend-y-frontend)
6. [Primer uso (curl)](#primer-uso-curl)
7. [Arquitectura del backend](#arquitectura-del-backend)
8. [Cómo agregar un nuevo caso de uso](#cómo-agregar-un-nuevo-caso-de-uso)
9. [Multi-tenant](#multi-tenant)
10. [Patrón Result de aplicación](#patrón-result-de-aplicación)
11. [Arquitectura del frontend](#arquitectura-del-frontend)
12. [Cómo agregar una nueva página](#cómo-agregar-una-nueva-página)
13. [Autenticación](#autenticación)
14. [Módulos de referencia (tabla)](#módulos-de-referencia-tabla)
15. [Comandos útiles](#comandos-útiles)
16. [Endpoints de referencia (lista parcial)](#endpoints-de-referencia-lista-parcial)
17. [Solución de problemas frecuentes](#solución-de-problemas-frecuentes)
18. [Instalación en servidor del cliente (candado SuperAdmin)](#instalación-en-servidor-del-cliente-candado-superadmin)

---

## Qué es este proyecto

ERP multi-tenant en SaaS. Backend en **.NET 10 (Clean Architecture)**, frontend en **React 19 + TypeScript + Vite**. Cada empresa (tenant) ve solo sus datos gracias a filtros globales en EF Core.

---

## Prerrequisitos

| Herramienta    | Versión mínima | Uso                          |
|----------------|----------------|------------------------------|
| Docker Desktop | cualquiera     | PostgreSQL en contenedor     |
| .NET SDK       | **10.0.201+** (ver `backend/src/global.json`; `rollForward: latestPatch`) | Backend; misma línea base que CI en GitHub |
| Node.js        | **22** (recomendado; CI usa 22) o 20+ | Frontend                     |

Además: acceso a PowerShell o bash para los comandos de este documento.

---

## Base de datos (Docker)

El contenedor de desarrollo se espera con nombre **`postgreszh`**, puerto **`5435`** y base **`dberpsaas`**.

### Opción recomendada: Compose (repo)

Desde la **raíz** del monorepo (`erp-saas/`):

```powershell
docker compose up -d
docker compose ps
```

O en PowerShell desde cualquier carpeta del repo: **`pwsh ./scripts/dev-up.ps1`** (sube el compose desde la raíz).

Archivo: **`docker-compose.yml`** (volumen persistente `erp_saas_pgdata`, healthcheck). Contraseña por defecto igual que en la doc de abajo; para otra, exportá **`POSTGRES_PASSWORD`** antes del `up` y usá la misma en `appsettings.Development.json`.

Apagar sin borrar datos: `docker compose down`. Borrar volumen y datos: `docker compose down -v`.

### Alternativa: `docker run` manual

```powershell
docker ps --filter "name=postgreszh"

docker run -d `
  --name postgreszh `
  -e POSTGRES_PASSWORD=zhin@2024 `
  -e POSTGRES_DB=dberpsaas `
  -p 5435:5432 `
  postgres:16
```

Credenciales habituales en documentación interna:

- Host: `localhost` — Puerto: **`5435`**
- Base de datos: `dberpsaas`
- Usuario: `postgres` / Contraseña de ejemplo: `zhin@2024`

Cadena de conexión típica (copiar a `appsettings.Development.json` local, no versionado):

```
Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024
```

---

## Migraciones EF Core

Desde `backend/src` (o desde `backend/src/ERP.API` según cómo invoques EF):

```powershell
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
```

Variante equivalente usada en otros documentos del repo:

```powershell
cd backend/src/ERP.API
dotnet ef database update --project ../ERP.Infrastructure/ERP.Infrastructure.csproj
```

---

## Arrancar backend y frontend

### Backend

```powershell
cd backend/src
dotnet run --project ERP.API --launch-profile http
```

- API: **http://localhost:5003**
- Swagger: **http://localhost:5003/swagger**

### Frontend

```powershell
cd frontend
npm install   # solo la primera vez
npm run dev
```

- SPA: **http://localhost:5173**  
- En desarrollo, el proxy de Vite suele mapear `/api` → `http://localhost:5003` (ver `frontend/vite.config.ts`). `VITE_API_URL` vacío en dev favorece usar el proxy y evitar CORS.

---

## Primer uso (curl)

### Crear un tenant

```bash
curl -X POST http://localhost:5003/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Mi Empresa","slug":"mi-empresa"}'
```

Guardar el `id` retornado.

### Registrar usuario

```bash
curl -X POST http://localhost:5003/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Admin",
    "lastName": "ERP",
    "email": "admin@miempresa.com",
    "password": "Admin1234!",
    "tenantId": "<id-del-tenant>",
    "role": "Admin"
  }'
```

### Login en el frontend

Abrir `http://localhost:5173`, ingresar `tenantId`, email y contraseña.

---

## Arquitectura del backend

```
ERP.Domain          ← Entidades, Value Objects, interfaces de repos, eventos
ERP.Application     ← Handlers (casos de uso), DTOs, contratos, validators
ERP.Infrastructure  ← EF Core, repositorios concretos, JwtService, CurrentTenantService
ERP.API             ← Controllers, Middleware, Program.cs
```

**Regla de dependencias:** el dominio no conoce EF Core ni ASP.NET. Flujo típico: **API → Application → Domain**; **Infrastructure → Application → Domain**.

---

## Cómo agregar un nuevo caso de uso

1. Crear `Command` o `Query` record en `ERP.Application/Modules/{Modulo}/UseCases/{Nombre}/`
2. Crear `Handler` en la misma carpeta. Los handlers suelen registrarse vía **assembly scan** (`AddApplication()` en el proyecto; no hace falta registrar uno a uno en `Program.cs` salvo excepción).
3. Agregar el endpoint en el controller correspondiente en `ERP.API/Controllers/`.

---

## Multi-tenant

Los modelos con `TenantId` usan **query filter global** en `ErpDbContext` donde aplique.

El tenant se resuelve desde el claim **`tenant_id`** del JWT en cada request vía **`ICurrentTenant`**.

```
JWT claim tenant_id → CurrentTenantService.TenantId → ErpDbContext → query filter
```

El filtro se evalúa **por instancia de DbContext**. Si agregás una entidad nueva con `TenantId`, registrar el filtro en `ErpDbContext.OnModelCreating` según el patrón existente.

---

## Patrón Result de aplicación

Los handlers pueden retornar `Result<T>` para errores de negocio esperados (ver `ERP.Application` / `Result.cs` en el repo).

```csharp
// En el handler:
return Result<ProductDto>.Failure("El código ya existe.");

// En el controller (patrón ilustrativo):
return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
```

La traducción exacta a `ActionResult` puede variar por controller; seguir el estilo del módulo existente.

---

## Arquitectura del frontend

Rutas relativas al directorio **`frontend/`**:

```
src/
├── modules/lib/api.ts     ← Cliente Axios (interceptor JWT, 401, etc.; la ruta exacta puede ser lib/ o modules/lib según evolución del repo)
├── store/authStore.ts     ← Estado de autenticación (Zustand + persistencia)
├── hooks/                 ← Hooks reutilizables
├── services/              ← Llamadas a API por dominio
├── types/                 ← Tipos alineados a DTOs del backend
├── components/            ← UI compartida (PageShell, AppLayout, ZH…)
├── pages/                 ← Páginas por ruta principal
└── nav/navConfig.ts       ← Menú estático y grupos
```

---

## Cómo agregar una nueva página

1. Tipos en `frontend/src/types/` (alineados al backend).
2. Servicio en `frontend/src/services/` (o módulo equivalente).
3. Página en `frontend/src/pages/` (o bajo `src/modules/...`).
4. Ruta en **`frontend/src/App.tsx`** y entrada de menú en **`frontend/src/nav/navConfig.ts`** si debe aparecer en el drawer/menú.

*(Nota histórica: en documentación antigua a veces se citaba solo `AppLayout.tsx` para el menú; en el estado actual del repo el menú estático se define en `navConfig.ts`.)*

---

## Autenticación

El token JWT se guarda en **`localStorage`** (p. ej. persistencia de Zustand con clave tipo `auth-storage` — ver `authStore` en el repo). El interceptor de Axios inyecta el header `Authorization`. Ante **401**, limpiar sesión y redirigir a `/login`.

---

## Instalación en servidor del cliente (candado SuperAdmin)

Cuando el ERP corre **en el servidor del cliente** (p. ej. varias empresas / tenants en la misma instancia), suele quererse que el rol **SuperAdmin** (operador de plataforma: tenants, planes, matriz de seguridad, etc.) solo exista en la **fase de puesta en marcha**. En operación diaria cada empresa se administra con usuarios **Admin** y el resto de roles, sin panel global de SuperAdmin.

Configuración en **`appsettings.json`** (o variables de entorno con el prefijo estándar de .NET):

| Clave | Valor típico | Efecto |
|-------|----------------|--------|
| `Deployment:SuperAdminPanelEnabled` | `true` (por defecto en desarrollo) | Login y API de SuperAdmin activos; rutas `/superadmin` en el frontend. |
| `Deployment:SuperAdminPanelEnabled` | `false` en producción tras el corte | No se emite JWT de SuperAdmin; el middleware bloquea peticiones autenticadas como SuperAdmin; el login deja de intentar `superadmin-login`; el menú oculta grupos de plataforma. |
| `Deployment:MaxActiveTenants` | Entero **> 0** (opcional) | Tope de **empresas activas** en la instancia. Si ya hay tantas activas como el tope, no se pueden crear más (registro público, `POST /api/access/superadmin/tenants` ni `POST /api/tenants`). |
| `Deployment:MaxIdentityUsers` | Entero **> 0** (opcional) | Tope de **usuarios globales** (`identity_users`: cuentas del IAM moderno). Al crearse un usuario nuevo (registro, alta SuperAdmin, alta desde Admin en «Accesos») se comprueba el total. No cuenta al **vincular** un admin ya existente a otra empresa. |

**Ilimitado (recomendado frente a “un número altísimo”):** no definas la clave, déjala vacía, pon el texto **`unlimited`**, o un valor **≤ 0** — todo eso se interpreta como **sin tope**. No hace falta poner `999999`; un entero muy alto es frágil (documentación, auditoría) y puede confundir.

Variables de entorno equivalentes: **`Deployment__SuperAdminPanelEnabled`**, **`Deployment__MaxActiveTenants`**, **`Deployment__MaxIdentityUsers`**.

El frontend consulta de forma anónima **`GET /api/public/deployment`** (DTO con `superAdminPanelEnabled`, `maxActiveTenants` y `maxIdentityUsers`) para alinear UI y rutas con el servidor.

**Un mismo Admin en varias empresas (sin ser SuperAdmin):** el usuario global (`IdentityUser`) se relaciona con cada empresa mediante **membresías** (`Membership`). El login por **`bootstrap-login`** devuelve la lista de empresas a las que tiene acceso; **`switch-tenant`** emite el JWT de sesión para la empresa elegida. Flujo típico operado por SuperAdmin: crear la primera empresa con administrador nuevo; para las siguientes, en el formulario de empresas activar **«Mismo administrador en varias empresas»** (`linkExistingAdmin: true`) con el **mismo email**, o bien `POST /api/tenants` (solo SuperAdmin) y luego **`POST /api/access/memberships/grant`** con el `tenantId` nuevo y el email del admin.

**Límites comerciales por empresa (p. ej. clientes / RUC):** el SuperAdmin ajusta **plan** y **módulos** del tenant (`PATCH /api/tenants/{id}/subscription`) y, en el catálogo SaaS, la feature medida **`CUSTOMERS`** con `limit_per_period` en `saas_plan_features` / overrides. Crear cliente incrementa consumo vía pipeline de suscripción (`CreateCustomerCommand` con `[ConsumeSubscriptionUnits]`).

**Flujo recomendado:** dejar `SuperAdminPanelEnabled` en `true` mientras se crean los tenants (p. ej. las tres empresas), planes, usuarios Admin iniciales y ajustes globales. Tras validar, poner `false`, reiniciar la API (o recargar configuración si en el futuro se usa `IOptionsMonitor` sin caché estática) y usar solo cuentas **Admin** por tenant (cada una solo ve y opera las empresas donde tenga membresía).

Para **volver a abrir** el panel SuperAdmin (mantenimiento o nuevas empresas), volver a `SuperAdminPanelEnabled: true` y reiniciar.

---

## Módulos de referencia (tabla)

Tabla **orientativa**; los paths reales pueden incluir más módulos (Sucursales, Clientes, SuperAdmin, etc.). Ver `docs/STATUS-2026-05-ERP.md` y `docs/developer-reference.html` para lista al día.

| Módulo     | Backend (ejemplos)                         | Frontend (ejemplos)   |
|------------|--------------------------------------------|-------------------------|
| Auth       | POST `/api/auth/register`, `/login`       | LoginPage               |
| Products   | GET/POST `/api/products`                   | ProductsPage            |
| Accounting | GET/POST `/api/accounts`, journal-entries  | AccountingPage          |
| Tenants    | POST `/api/tenants` (solo SuperAdmin)      | —                       |

---

## Comandos útiles

### Backend

```powershell
cd backend/src
# El SDK activo respeta global.json (misma base que GitHub Actions).
dotnet build ERP.slnx
dotnet test ERP.slnx

cd ERP.Infrastructure
dotnet ef migrations add NombreMigracion --startup-project ../ERP.API
dotnet ef migrations remove --startup-project ../ERP.API
```

### Frontend

```powershell
cd frontend
npm run dev
npm run build
npm run lint
npx tsc --noEmit
```

**Smoke E2E (Playwright):** la primera vez en la máquina hace falta `npx playwright install chromium` (o `npx playwright install`). Luego, con el build generado (`npm run build`): `npm run test:e2e` (levanta `vite preview` en **4173** vía `playwright.config.ts`). Tests en `frontend/e2e/`.

### CI (GitHub)

Workflow: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

- **Disparo:** push y PR a `main`, `master`, `development`, `develop`, `release/**`, `hotfix/**`, y **`workflow_dispatch`** (ejecución manual).
- **Backend:** `actions/setup-dotnet` lee **`backend/src/global.json`**, caché de NuGet por `*.csproj`, luego `dotnet test backend/src/ERP.slnx -c Release`. Variables `DOTNET_NOLOGO` y telemetría desactivada.
- **Frontend:** Node **22**, `npm ci`, `npm run lint`, `npm run build`, Playwright (Chromium) y `npm run test:e2e` (smoke con `vite preview`; el caso actual no exige API levantada).

Los runners **ubuntu-latest** (imagen Ubuntu 24.04) incluyen varios SDK .NET; la lista oficial está en [Software instalado — Ubuntu 24.04](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md) (sección *.NET Tools*). Este repo apunta a **SDK 10.0.201** como mínimo para coincidir con esa imagen; parches posteriores 10.0.x se admiten vía `rollForward`.

### Ramas Git (política del equipo)

| Fase | Qué usar |
|------|-----------|
| **Ahora** | Integración en **`main`** hasta dejar la base estable (CI y reglas de rama apuntan aquí). |
| **Después** | **`development`** (o `develop`): línea diaria de features; PRs hacia esta rama cuando exista. |
| **Release** | Rama **`release/*`** (o una `release/x.y`) para estabilizar versión antes de producción; merges controlados hacia `main` según acuerden. |
| **Hotfix** | **`hotfix/*`** desde el commit de producción para correcciones urgentes; merge de vuelta a `main` y, si aplica, a `development`. |

El workflow de CI ya escucha esas ramas para no tener que tocar el YAML el día que las creen. **Protección de rama:** por ahora conviene exigir checks solo en `main`; al abrir `development` / `release` / `hotfix`, replicá la misma regla o usá un *ruleset* por patrón.

---

## Endpoints de referencia (lista parcial)

Lista útil para **smoke manual**; no sustituye a Swagger ni a `developer-reference.html`.

| Método | Ruta                                    | Auth | Descripción              |
|--------|-----------------------------------------|------|--------------------------|
| POST   | /api/auth/register                      | No   | Registrar usuario      |
| POST   | /api/auth/login                         | No   | Login → JWT            |
| POST   | /api/tenants                            | No   | Crear tenant           |
| GET    | /api/products                           | JWT  | Listar productos       |
| GET    | /api/products/{id}                      | JWT  | Obtener producto       |
| POST   | /api/products                           | JWT  | Crear producto         |
| GET    | /api/accounts                           | JWT  | Listar cuentas        |
| GET    | /api/accounts/{id}                      | JWT  | Obtener cuenta         |
| POST   | /api/accounts                           | JWT  | Crear cuenta           |
| GET    | /api/accounts/journal-entries           | JWT  | Listar asientos        |
| GET    | /api/accounts/journal-entries/{id}      | JWT  | Obtener asiento        |
| POST   | /api/accounts/journal-entries           | JWT  | Crear asiento          |

---

## Solución de problemas frecuentes

### El backend no inicia — error de conexión a la DB

Comprobar Docker y que el contenedor `postgreszh` esté activo; revisar cadena en `appsettings.Development.json`.

### Error 401 en el frontend

El JWT vence (p. ej. 60 min según configuración). Cerrar sesión y volver a iniciar sesión.

### Error al compilar — archivos bloqueados (Windows)

Si **`ERP.API` o `dotnet`** sigue ejecutándose, las DLL en `ERP.API/bin` pueden quedar bloqueadas. Detener el proceso de la API antes de `dotnet build` / `dotnet test`:

```powershell
Stop-Process -Name "ERP.API" -Force
```

*(Si el proceso aparece como `dotnet`, usar el Administrador de tareas o `taskkill /PID …` sobre el PID que escucha el puerto 5003.)*

### Error de CORS en el navegador

Verificar `Cors:AllowedOrigins` en `appsettings.Development.json` (plantilla: `appsettings.Development.json.example`) incluyendo `http://localhost:5173`.

### Config local no versionada

Copiar `backend/src/ERP.API/appsettings.Development.json.example` → `appsettings.Development.json` y ajustar secretos/cadena. Opcional: `frontend/.env.development.example` → `.env.development`.

