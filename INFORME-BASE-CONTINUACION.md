# Informe de base para continuar — ERP SaaS (ZH)

**Fecha de referencia:** 30 de abril de 2026  
**Ámbito:** repositorio `erp-saas` (backend .NET + frontend React).  
**Propósito:** dejar documentado qué hay hoy, cómo se ejecuta, qué está verificado y por dónde seguir mañana sin perder contexto.

---

## 1. Resumen ejecutivo

| Área | Estado |
|------|--------|
| Backend (.NET 10, EF Core, PostgreSQL) | Compila; multi-capa (API → Application → Domain → Infrastructure). |
| Frontend (React 19, Vite 8, TypeScript) | `npm run lint` y `npm run build` OK. |
| Tests automatizados | `ERP.Domain.Tests`, `ERP.Application.Tests`, `ERP.Infrastructure.Tests`, `ERP.API.Tests` pasan (ver §8). |
| Sucursales + geografía Ecuador (INEC/DPA) | Tablas `branches`, `geo_*` + seed INEC; API `/api/geography/*` y UI en `/saas/branches` con cascada país → provincia → cantón → parroquia. |
| Dev local (CORS / proxy) | Vite proxy `/api` → `localhost:5003`; `VITE_API_URL` vacío en dev; CORS según `appsettings.Development.json` (no versionado; plantilla `appsettings.Development.json.example`). |

**Importante:** para compilar o probar `ERP.API.Tests` con la API ya corriendo, Windows puede bloquear DLLs en `ERP.API/bin`. Parar la API (`Ctrl+C` o liberar puerto 5003) antes de `dotnet test` sobre proyectos que referencian `ERP.API`.

---

## 2. Stack y versiones

- **Backend:** .NET 10, ASP.NET Core Web API, JWT, EF Core 10, PostgreSQL (Npgsql).
- **Frontend:** React 19, React Router 7, Vite 8, Axios, Zustand, i18n propio (es / en / **Kichwa de Cañar, Ecuador**; locale técnico `qu` en `qu.json` — no confundir con “quechua” genérico u otras variantes).
- **Base de datos:** PostgreSQL; en dev copiar `ERP.API/appsettings.Development.json.example` → `appsettings.Development.json` y ajustar usuario/clave/JWT (archivo real ignorado por git). Frontend: copiar `frontend/.env.development.example` → `.env.development` si hace falta.

---

## 3. Estructura del monorepo (alto nivel)

```
erp-saas/
├── backend/src/
│   ├── ERP.API/              # Controllers, Program.cs, Swagger (dev), JWT
│   ├── ERP.Application/    # Casos de uso, DTOs, validaciones de aplicación
│   ├── ERP.Domain/         # Entidades, interfaces de dominio
│   ├── ERP.Infrastructure/ # EF Core, DbContext, repositorios, migraciones, SQL embebido INEC
│   └── *.Tests/              # Proyectos de prueba por capa
├── frontend/                 # SPA Vite + React
├── scripts/                  # import_inec_ecuador_geography.ps1 / .py (regenerar SQL INEC)
└── INFORME-BASE-CONTINUACION.md   # este documento
```

---

## 4. Base de datos: qué existe y cómo se llama

- **Multi-tenant:** filtro global en `ErpDbContext` para entidades `IMustHaveTenant` (no aplica a catálogos globales de geografía).
- **Sucursales:** tabla **`branches`** (no “sucursales”).
- **Geografía (global):**  
  - `geo_countries`, `geo_provinces`, `geo_cantons`, `geo_parishes`  
  - País Ecuador: id **`EC`**.  
  - Datos DPA/INEC vía migración **`20260430225304_SeedInecEcuadorGeography`** (SQL embebido `Seeding/Scripts/inec_ecuador_geography.sql`).  
  - **Nota técnica:** el SQL de seed **no** debe incluir `BEGIN`/`COMMIT` propios (EF ya envuelve la migración en transacción).

**Migraciones relevantes recientes (orden lógico):**

- `20260430223742_BranchesAndGeography` — esquema branches + geo_*.
- `20260430225304_SeedInecEcuadorGeography` — datos INEC Ecuador.

**Comando habitual (desde `backend/src/ERP.API`):**

```bash
dotnet ef database update --project ../ERP.Infrastructure/ERP.Infrastructure.csproj
```

---

## 5. Backend: API y módulos expuestos

**Controladores (ruta base `/api/...`):** Auth, Access, Products, catálogos (brands, product types, units, tax rates, tariffs, categories, subcategories, product lines), Accounting (accounts, journal entries), Tenants, SuperAdmin, Security, **Branches**, **Geography** (solo lectura para combos).

**Sucursales y geografía:**

- `BranchesController` — CRUD/listado sucursales; permisos `perm:saas.branches.*`.
- `GeographyController` — `countries`, `provinces`, `cantons`, `parishes`; solo `[Authorize]` (token de sesión).

**Autorización:** política por defecto “Session”; permisos por claim en endpoints de negocio; Admin/SuperAdmin pasan permisos en `PermissionHandler`.

---

## 6. Frontend: rutas y pantallas útiles

| Ruta | Pantalla |
|------|----------|
| `/login`, `/password-reset`, `/select-tenant` | Auth / tenant |
| `/dashboard` | Inicio |
| `/products`, `/catalog/*` | Productos y catálogos |
| `/accounting` | Contabilidad |
| `/security` | Ajustes seguridad |
| `/companies` | SuperAdmin empresas |
| `/saas/branches` | **Sucursales** (formulario con ubicación en cascada) |
| `/access`, `/profiles` | Accesos tenant y perfiles/permisos |

**Servicios clave:** `branchService.ts` (branches + geography con `normalizeGeographyList` y lectura tolerante de envelope `responseObject` / `ResponseObject`).

**Dev / red:**

- `vite.config.ts`: **proxy** `'/api'` → `http://localhost:5003`.
- `frontend/.env.development`: **`VITE_API_URL=`** (vacío) → peticiones relativas `/api/...` al mismo origen que Vite (evita CORS en local).
- `frontend/src/lib/api.ts`: si `VITE_API_URL` tiene valor, se usa; si no y es DEV, `baseURL` vacío; en build de producción sin env, fallback documentado en código.

---

## 7. Cómo arrancar mañana (checklist)

1. **PostgreSQL** en marcha (puerto/host según tu `appsettings.Development.json`).
2. **Migraciones aplicadas** (`dotnet ef database update` desde `ERP.API` apuntando a `ERP.Infrastructure`).
3. **API:** desde `backend/src/ERP.API`:

   ```bash
   dotnet run --launch-profile http
   ```

   → `http://localhost:5003`.

4. **Frontend:** desde `frontend`:

   ```bash
   npm install   # si hace falta
   npm run dev
   ```

   → `http://localhost:5173` (recomendado **localhost**, no mezclar con 127.0.0.1 si usás URL directa a la API sin proxy).

5. Abrir el ERP en el navegador y comprobar login + **Sucursales** y carga de países.

---

## 8. Verificación de calidad ya hecha en el repo

- `dotnet build` sobre `ERP.API` (incluye dependencias).
- `npm run lint` y `npm run build` en `frontend`.
- `dotnet test` en los cuatro proyectos `*.Tests` (Domain, Application, Infrastructure, API).

**Limpieza reciente (código muerto / plantilla):** eliminados `AiSuggestionCard`, `App.css` sin uso, assets `react.svg`/`vite.svg`, carpeta `assets` vacía; `ERP.API.http` actualizado a ejemplo real de geography; título HTML `ERP`.

**Artefacto `frontend/dist`:** se regenera con `npm run build`; está en `.gitignore`.

---

## 9. Pendientes / mejoras conocidas (no bloquean)

- Avisos **CS1573** en `IProductCatalogRepository` (comentarios XML `param` incompletos) — solo documentación.
- Si corrés `dotnet test` con la **API ejecutándose**, puede fallar la copia de DLLs; parar la API antes.
- Producción: definir **`VITE_API_URL`** en build del front y **`Cors:AllowedOrigins`** en la API con el dominio real del SPA.

---

## 10. Scripts y datos INEC

- `scripts/import_inec_ecuador_geography.ps1` y `.py` — regeneran el SQL; por defecto **sin** `BEGIN/COMMIT` (opción `-WrapTransaction` / `--wrap-transaction` si hace falta para `psql` suelto).
- El SQL consumido por EF está en `ERP.Infrastructure/Seeding/Scripts/inec_ecuador_geography.sql` (recurso embebido en el `.csproj`).

---

## 11. Dónde seguir el trabajo (ideas)

- Ampliar **perfiles/permisos** ya existentes para nuevas pantallas.
- Más validaciones de negocio en **sucursales** (obligatoriedad de parroquia, etc.).
- **Productos / catálogos** siguen siendo el núcleo del dominio actual.
- Documentar o automatizar **arranque** (docker-compose, scripts `dev.ps1`) si el equipo lo pide.

---

*Fin del informe. Actualizá este archivo cuando cambie algo relevante (migraciones, puertos, flujos de auth o despliegue).*
