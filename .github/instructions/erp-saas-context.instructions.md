---
name: erp-saas-context
description: "Context and guidelines for ZH Technologies ERP SaaS platform. Use when: writing code, reviewing architecture, creating features, working on API endpoints, or building frontend modules. Ensures alignment with multi-tenant, 4-layer validation, and modular clean architecture principles."
applyTo: ["backend/**", "frontend/**", "scripts/**"]
---

# ZH Technologies ERP SaaS — Agent Context & Guidelines

**Project Revision:** 2026-05-02  
**Status:** Active development, multi-tenant, modular architecture  
**Primary Languages:** C# (.NET), TypeScript/React, PowerShell

---

## 🏗️ Architecture Overview

This is a **monolithic modular application** with **clean architecture** principles:

### Layer Structure (Domain-Driven Design)

1. **Domain Layer** (`ERP.Domain/`)
   - Business logic, value objects, aggregates
   - Module-based organization: `Modules/{ModuleName}/`
   - No external dependencies
   - Query filters for multi-tenant isolation

2. **Application Layer** (`ERP.Application/`)
   - Use cases, DTOs, mappers
   - Application services orchestrating domain logic
   - Validation rules (4-layer: DTO, Business, Authorization, Filter)

3. **Infrastructure Layer** (`ERP.Infrastructure/`)
   - Database access (Entity Framework Core)
   - External service integration
   - Multi-tenant context providers
   - Query filter implementation for tenant isolation

4. **API Layer** (`ERP.API/`)
   - Controllers (RESTful endpoints)
   - Middleware (auth, CORS, error handling)
   - Global exception handling
   - Documentation via XML comments

### Validation Pipeline (4-Layer Model)

All requests must validate through:
1. **DTO Layer** — Type safety, required fields, format validation
2. **Business Layer** — Business rule validation (domain logic)
3. **Authorization Layer** — Tenant access, permission checks
4. **Filter Layer** — Multi-tenant query isolation (EF Core query filters)

---

## 🔐 Multi-Tenant Architecture

- **Tenant Context:** JWT claims + `TenantId` from token
- **Data Isolation:** EF Core query filters on all entities
- **Navigation Security:** SessionStorage (`erp.saas.*` keys), NO query parameters with UUIDs
- **File:** `docs/SAAS-PLAN-TENANT-FLOW.md` → Commercial tier structure
- **File:** `.cursor/rules/saas-navigation-no-sensitive-url.mdc` → Frontend routing rules

### Frontend Tenant Context
- Use `sessionStorage` for tenant/company IDs
- **Never** pass `?tenantId=`, `?data=`, or `?subscription=` in URLs
- Helper functions in: `frontend/src/navigation/companiesTenantDetailNav.ts`

---

## 📁 Project Structure

### Backend (`backend/src/`)
```
ERP.API/                    ← Controllers, appsettings, Middleware
ERP.Application/            ← Use cases, DTOs, Mappers
ERP.Domain/                 ← Business logic, Modules/{ModuleName}/
ERP.Infrastructure/         ← EF Core, Repository, Tenant Context
ERP.*.Tests/                ← xUnit/Moq test suites
global.json                 ← .NET SDK version (team + CI)
ERP.slnx                    ← Solution file
```

### Frontend (`frontend/`)
```
src/
  ├── components/           ← Reusable React components
  ├── modules/              ← Feature modules (Customers, Products, etc.)
  ├── pages/                ← Page components (layouts)
  ├── services/             ← API client, data fetching
  ├── store/                ← State management (Redux/Zustand)
  ├── hooks/                ← Custom React hooks
  ├── i18n/                 ← Internationalization (es, en, qu)
  ├── schemas/              ← Zod/Yup validation schemas
  ├── types/                ← TypeScript interfaces
  ├── constants/            ← App constants
  ├── nav/                  ← Navigation config
  ├── App.tsx               ← Main router (add new pages here)
  └── main.tsx              ← Entry point
vite.config.ts              ← Build config
tsconfig.json               ← TypeScript config
```

### Documentation (`docs/`)
- **`CONTEXT.md`** — Master index (start here)
- **`ARCHITECTURE.md`** — Detailed layer diagram, module structure, migrations
- **`DESARROLLO.md`** — Local setup, Docker, database, curl examples, troubleshooting
- **`FRONTEND-PANTALLAS.md`** — Route inventory, screen checklist
- **`SAAS-PLAN-TENANT-FLOW.md`** — Commercial tier to feature mapping
- **`COMPANIES-PLAN-MENU-ADMIN.md`** — Menu & feature association UI
- **`STATUS-2026-05-ERP.md`** — Current status, verified features, pending items
- **`adr/`** — Architecture Decision Records (stable decisions)

---

## 🎯 Key Rules & Constraints

### 1. Modular Clean Architecture
- **Module per domain concept** (Customers, Products, Orders, Accounting, etc.)
- **Folder path:** `ERP.Domain/Modules/{ModuleName}/`
- No cross-module horizontal calls; go through Application layer
- **Reference:** `docs/ARCHITECTURE.md`, `docs/adr/0001-*.md`

### 2. Multi-Tenant Query Isolation
- **All queries must apply tenant filters** using EF Core Global Query Filters
- **Location:** `ERP.Infrastructure/Persistence/Filters/`
- **Pattern:** Every entity implementing `ITenantEntity` gets filtered by TenantId
- **Validation:** Authorization layer checks `TenantId` from JWT

### 3. 4-Layer Validation
```
Request → DTO Validation → Business Rules → Authorization → Query Filters
```
- Always validate DTOs with annotations or Fluent Validation
- Business logic in Domain layer (separate from infrastructure)
- Authorization checks in Application service (CanAccess, HasPermission)
- Query filters in Infrastructure (EF Core)

### 4. ZH Form Component System
- **Canonical template:** `docs/zh-form-template/zh_erp_component_library.html`
- **Frontend usage:** Build forms using ZH component library
- **Pattern:** Modal dialogs + form components for data entry
- **Validation:** Client-side (Zod/Yup) + Server-side (4-layer)

### 5. Internationalization (i18n)
- **Frontend locales:** `frontend/src/i18n/locales/`
  - Spanish (es): `es.json`
  - English (en): `en.json`
  - Kichwa (qu): `qu.json` — *Kichwa de Cañar, Ecuador*
- **Pattern:** All UI strings as i18n keys, never hardcoded
- **Backend:** Spanish by default; localization at frontend layer
- **Reference:** `docs/adr/` for i18n decisions

### 6. Navigation Security (SaaS)
- **Tenant ID storage:** `sessionStorage.getItem('erp.saas.tenantId')`
- **URL pattern:** `/tenants/{tenantId}/modules/{module}` OK, but ID from sessionStorage
- **Never:** Query strings with sensitive data (`?tenantId=uuid`, `?companyId=uuid`)
- **Frontend helpers:** `src/navigation/companiesTenantDetailNav.ts`

### 7. Database & Migrations
- **ORM:** Entity Framework Core (.NET)
- **Database:** PostgreSQL (dev: Docker container `postgreszh`, port 5435)
- **Migrations:** `dotnet ef database update` (managed in Infrastructure layer)
- **Reference:** `docs/DESARROLLO.md`

---

## 🚀 Development Workflow

### Local Environment Setup
```powershell
# Start PostgreSQL
docker-compose up -d

# Restore & migrate database
cd backend/src
dotnet ef database update

# Run API
dotnet run --project ERP.API

# Run frontend (separate terminal)
cd frontend
npm run dev
```

### Common Commands
- **Test:** `dotnet test` (backend), `npm run test` (frontend)
- **Build:** `dotnet build` (backend), `npm run build` (frontend)
- **Format:** Follow project eslint/stylecop configs
- **Database reset:** `dotnet ef database drop --force && dotnet ef database update`

### Branch Strategy
- `main` — Production-ready (once CD is set up)
- `development` — Integration branch
- `release/*` — Release prep
- `hotfix/*` — Emergency fixes
- **Reference:** `docs/DESARROLLO.md` (Git policy section)

---

## 📝 Code Style & Patterns

### Backend (C# / .NET)
- **Naming:** PascalCase for classes, camelCase for properties/methods
- **DTOs:** Suffix with `Dto` (e.g., `CreateProductDto`, `ProductDetailDto`)
- **Repositories:** Pattern-based, registered in DI container
- **Services:** Application services in `ERP.Application/Services/`
- **Exceptions:** Custom domain exceptions in `ERP.Domain/Exceptions/`
- **Logging:** Use `ILogger<T>` injected via DI
- **API Responses:** Wrap in `Result<T>` or `Result` (success/error, no exceptions in API responses)

### Frontend (TypeScript / React)
- **Naming:** PascalCase for components, camelCase for functions/hooks
- **Components:** Functional components with hooks
- **Folders:** Group by feature/module (not by file type)
- **Hooks:** Custom hooks in `src/hooks/`, naming pattern `use*` (e.g., `useCustomers`)
- **Services:** API client in `src/services/` (fetch/axios wrapper)
- **State:** Store in `src/store/` (Redux slices or context)
- **Types:** Shared interfaces in `src/types/`, module-specific in feature folders
- **Validation:** Zod schemas in `src/schemas/`, validate at form level + API call
- **i18n:** Use `useTranslation()` hook, reference keys as `t('nav.customers')`

---

## 🔗 Essential Reading Order

1. **This file** — Agent context & quick reference
2. **`docs/CONTEXT.md`** — Master index & folder guide
3. **`docs/ARCHITECTURE.md`** — Layer diagrams, module structure, table of modules
4. **`docs/DESARROLLO.md`** — Setup, local dev, PostgreSQL, common issues
5. **`.cursor/rules/erp-unified-rules.mdc`** — Implementation constraints (architecture, validation, ZH Form, i18n, navigation)
6. **`.cursor/rules/saas-navigation-no-sensitive-url.mdc`** — SaaS routing & sessionStorage patterns
7. **Feature docs** — FRONTEND-PANTALLAS.md, SAAS-PLAN-TENANT-FLOW.md, COMPANIES-PLAN-MENU-ADMIN.md (as needed)
8. **`docs/adr/`** — Deep dives on stable architectural decisions

---

## ✅ When Responding to User Requests

1. **Code Generation:** Follow the 4-layer validation pattern, module-based organization, and clean architecture principles
2. **Architecture Questions:** Reference ARCHITECTURE.md, modular structure in `Domain/Modules/{Name}/`
3. **Multi-Tenant Logic:** Always consider TenantId isolation, query filters, and authorization checks
4. **Frontend Routes:** Update `frontend/src/App.tsx` and `frontend/src/nav/navConfig.ts`, use sessionStorage for tenant context
5. **Database Changes:** Add Entity Framework migrations, document in ADR if architectural impact
6. **i18n:** Add keys to `frontend/src/i18n/locales/es.json` (and en.json, qu.json)
7. **Testing:** Write xUnit tests (backend), Jest/Vitest (frontend); validate multi-tenant isolation in tests
8. **Documentation:** Update relevant docs (ARCHITECTURE.md, FRONTEND-PANTALLAS.md, or ADR)

---

## 🐛 Troubleshooting Pointers

- **DLL locked in backend:** Close IDE, run `Remove-Item -Recurse bin/ obj/` in PowerShell
- **CORS errors:** Check `ERP.API/Middleware/CorsMiddleware.cs` and `appsettings.json`
- **401 Unauthorized:** Verify JWT token contains `TenantId` claim; check auth middleware
- **Multi-tenant query issue:** Ensure query filter is applied in `ERP.Infrastructure/Persistence/Filters/`
- **Frontend tenant context missing:** Check `sessionStorage` keys and `companiesTenantDetailNav.ts` helpers
- **Database migration failure:** Review `docs/DESARROLLO.md` — database schema and migration troubleshooting section
- **i18n string not appearing:** Verify key in JSON locale file and component uses `useTranslation()` hook correctly

---

**Master Index:** `docs/CONTEXT.md`  
**Last Updated:** 2026-05-02  
**Project:** ZH Technologies ERP SaaS
