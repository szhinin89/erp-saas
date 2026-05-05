# ZH TECHNOLOGIES ERP SAAS — INSTRUCCIONES DE DESARROLLO (RESUMIDO)

**Fecha:** Mayo 2026 | **Estado:** Desarrollo activo | Arquitectura limpia modular, 4 capas, Multi-tenant

---

## 🛠️ HERRAMIENTAS Y TECNOLOGÍAS

### Backend (.NET / C#)
- **.NET 8+** — Framework moderno asincrónico
- **Entity Framework Core (EF Core)** — ORM para acceso a datos
- **Fluent Validation** — Validación de DTOs
- **MediatR** — Patrón CQRS (Commands/Queries)
- **xUnit + Moq** — Testing unit e integration
- **PostgreSQL** — Base de datos multi-tenant
- **JWT** — Autenticación con tokens

### Frontend (React / TypeScript)
- **React 18+** — UI library
- **TypeScript** — Type safety
- **Vite** — Build tool rápido
- **React Router v6** — Routing SPA
- **i18next** — Internacionalización (es, en, qu)
- **Vitest + React Testing Library** — Testing
- **Zod/Yup** — Validación de esquemas
- **Axios/Fetch API** — HTTP client

### Infrastructure
- **PostgreSQL 14+** — Base de datos (Docker container: `postgreszh`, puerto 5435)
- **Docker & Docker Compose** — Contenedores
- **GitHub Actions** — CI/CD (futuro)
- **PowerShell** — Scripts automación Windows

### Desarrollo
- **VS Code / Cursor** — IDE principal
- **Git** — Control de versiones
- **GitHub** — Repositorio remoto
- **.NET CLI** — Comandos backend (dotnet)
- **npm/Node.js** — Gestor deps frontend

---

## 🏗️ ARQUITECTURA — 4 CAPAS

```
API (Controllers) → Application (DTOs, Commands/Queries) → Domain (Lógica negocio) → Infrastructure (BD, EF Core)
```

**Domain** (pura lógica, sin dependencias externas):
- `Modules/{Modulo}/Entities/` — Aggregates + Value Objects
- `Modules/{Modulo}/Exceptions/` — Excepciones dominio
- Todas las entidades implementan `ITenantEntity` ✅

**Application** (orquestación, CQRS):
- `Modules/{Modulo}/Commands/` — Acciones que cambian estado
- `Modules/{Modulo}/Queries/` — Acciones que solo leen
- `Modules/{Modulo}/DTOs/` — Validación con FluentValidation
- Regla: DTO → FluentValidator → Autorización → Domain → Persistencia

**Infrastructure** (acceso datos, filtros):
- `ErpDbContext.cs` — EF Core con Global Query Filters por TenantId
- `Repositories/` — Acceso a datos (abstracción)
- **Filtro automático:** Todas consultas filtran por TenantId actual

**API** (endpoints HTTP):
- Controllers delgados (máx 20 líneas)
- JWT obligatorio en header Authorization
- Respuestas con `Result<T>` (éxito/error)

---

## ✅ VALIDACIÓN EN 4 CAPAS (OBLIGATORIO)

```
[1] DTO Validation (FluentValidation) 
  ↓
[2] Business Rules (Domain logic — encapsulado en entities/value objects)
  ↓
[3] Authorization (¿Puede el usuario hacer esto? En Application Handler)
  ↓
[4] Query Filters (Multi-tenant: EF Core filtra automáticamente por TenantId)
```

**Toda solicitud HTTP pasa por las 4 capas. No hay excepciones.**

---

## 🔐 MULTI-TENANT (CRÍTICO)

**Backend:**
- JWT contiene `tenantId` claim
- Middleware extrae tenantId del JWT → `ITenantContextService`
- `ErpDbContext` aplica filtro global: `HasQueryFilter(p => p.TenantId == _tenantContext.TenantId)`
- **Resultado:** Sin hacer nada especial, queries filtran por tenant automáticamente

**Frontend:**
- ❌ **NUNCA:** `?tenantId=uuid` en URL
- ✅ **SÍ:** `sessionStorage.setItem('erp.saas.tenantId', uuid)`
- TenantId va en JWT, no en URL

---

## 🌍 i18n — OBLIGATORIO

**Idiomas:** Español (es), Inglés (en), Kichwa (qu)

**Estructura:**
```
frontend/src/i18n/locales/
├── es.json (Base - agregar claves aquí primero)
├── en.json
└── qu.json
```

**Uso:**
```typescript
import { useTranslation } from 'react-i18next';
const { t } = useTranslation();
return <button>{t('forms.save')}</button>;  // ✅
// ❌ NO hardcodear: <button>Guardar</button>
```

---

## 🛠️ FLUJO FEATURE NUEVOS (Resumen)

**Backend:**
1. Value Objects en `Domain/Modules/{Mod}/ValueObjects/`
2. Entidades en `Domain/Modules/{Mod}/Entities/` (implementar `ITenantEntity`)
3. DTO + FluentValidator en `Application/Modules/{Mod}/DTOs/`
4. Command/Query + Handler en `Application/Modules/{Mod}/Commands|Queries/`
5. Migration EF Core: `dotnet ef migrations add ...`
6. Controller en `API/Controllers/`

**Frontend:**
1. Servicio API en `src/services/productService.ts`
2. Componente en `src/modules/{modulo}/components/Form.tsx` (usar ZH Form)
3. Página en `src/modules/{modulo}/pages/`
4. Agregar ruta en `src/App.tsx` y menú en `src/nav/navConfig.ts`
5. i18n keys en `src/i18n/locales/es.json`

---

## 📋 MÓDULOS ACTUALES

| Módulo | Estado | Notas |
|--------|--------|-------|
| Products | 80% | Controllers, DTOs básicos |
| Customers | 70% | Repositories listos |
| Orders | 50% | Domain en progreso |
| Auth | 95% | JWT, Middleware OK |
| Branches | 60% | Multi-sucursal básico |

---

## 🔗 COMANDOS ESENCIALES

```powershell
# BACKEND
cd backend/src
dotnet build
dotnet run --project ERP.API
dotnet test
dotnet ef migrations add NombreMigracion
dotnet ef database update

# FRONTEND
cd frontend
npm install
npm run dev         # http://localhost:5173
npm run build
npm test

# DOCKER
docker-compose up -d    # PostgreSQL (postgreszh)
```

---

## ✅ CHECKLIST ANTES DE COMMIT

- [ ] Domain: ITenantEntity implementado
- [ ] Application: FluentValidator + Handler (4-layer validation)
- [ ] Infrastructure: Migration EF Core creada + aplicada
- [ ] API: Controller con Result<T>
- [ ] Frontend: i18n keys en es.json (después en en.json, qu.json)
- [ ] Frontend: Componente usa ZH Form + sessionStorage (no URLs con tenantId)
- [ ] Tests: Unit (Domain) + Integration (Application)
- [ ] Routa registrada en App.tsx y navConfig.ts (si es nueva página)

---

## ⚠️ ERRORES COMUNES

| Problema | Solución |
|----------|----------|
| DLL bloqueada | `rm -Force -Recurse bin, obj` + cierra IDE |
| 401 Unauthorized | Verificar JWT, tenantId en claims |
| Datos de otro tenant | Revisar query filter en DbContext |
| i18n no funciona | Clave existe en es.json y useTranslation() importado |
| CORS error | Verificar appsettings.json + CorsMiddleware |

---

## 📚 DOCUMENTACIÓN PRINCIPAL

1. `docs/CONTEXT.md` — Índice maestro
2. `docs/ARCHITECTURE.md` — Capas, módulos, estructura
3. `docs/DESARROLLO.md` — Setup, Docker, troubleshooting
4. `.cursor/rules/erp-unified-rules.mdc` — Restricciones
5. `.cursor/rules/saas-navigation-no-sensitive-url.mdc` — SaaS routing

---

## 🎯 REGLAS DE ORO

1. **Responsabilidad única:** Una capa = una responsabilidad
2. **Dependencias hacia adentro:** API → App → Domain
3. **Multi-tenant primero:** Filtros automáticos en todas consultas
4. **4-layer validation:** Sin excepciones, siempre
5. **i18n obligatorio:** Todo string visible en UI va en locales JSON
6. **Tests obligatorios:** Domain (unit) + Application (integration)
7. **Clean code:** Nombres claros, métodos pequeños

---

**Última actualización:** 2026-05-05 | ZH Technologies ERP SaaS
