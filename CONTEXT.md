# CONTEXT.md — ZH Technologies ERP

Índice maestro. Lee primero este archivo; luego abre solo el enlace que corresponde a tu tarea.

---

## Documentación oficial (`docs/`)

| Qué necesito saber | Archivo |
|--------------------|---------|
| **Objetivo y visión** del producto | **`PROJECT.md`** (raíz) |
| **Reglas de código** para agentes | **`CLAUDE.md`** (raíz) |
| **Estado actual del proyecto** (única fuente) | **`docs/STATUS.md`** |
| **Arquitectura oficial** | **`docs/ARCHITECTURE.md`** |
| **Roadmap y prioridades** | **`docs/ROADMAP.md`** |
| **Reglas de desarrollo** | **`docs/DEVELOPMENT-RULES.md`** |
| **Multi-tenant / company** | **`docs/MULTITENANCY.md`**, **`docs/SCOPES.md`** |
| **Seguridad** | **`docs/SECURITY.md`** |
| **Billing SaaS** | **`docs/BILLING.md`** |
| **Planes comerciales** | **`docs/COMMERCIAL-PLANS.md`** |
| **Gestión de empresas** | **`docs/COMPANY-MANAGEMENT.md`** |
| **Identidad / auth** | **`docs/identity-model.md`**, **`docs/frontend-identity.md`** |
| **Platform vs ERP runtime** | **`docs/platform-runtime-boundaries.md`** |
| **Base de datos** | **`docs/DATABASE/`** |

> Al cambiar arquitectura o delivery: actualizar **`docs/STATUS.md`** y **`docs/ROADMAP.md`** primero.

---

## Árbol del monorepo

```
erp-saas/
├── CLAUDE.md
├── CONTEXT.md
├── PROJECT.md
├── README.md
├── docker-compose.yml
├── docs/
│   ├── ARCHITECTURE.md
│   ├── STATUS.md
│   ├── ROADMAP.md
│   ├── DEVELOPMENT-RULES.md
│   ├── MULTITENANCY.md
│   ├── SCOPES.md
│   ├── SECURITY.md
│   ├── BILLING.md
│   ├── COMMERCIAL-PLANS.md
│   ├── COMPANY-MANAGEMENT.md
│   ├── identity-model.md
│   ├── frontend-identity.md
│   ├── platform-runtime-boundaries.md
│   └── DATABASE/
│       ├── DATABASE-ARCHITECTURE.md
│       ├── MIGRATIONS.md
│       ├── RLS.md
│       └── TABLES.md
├── backend/src/
│   ├── ERP.API/
│   ├── ERP.Application/
│   ├── ERP.Domain/
│   └── ERP.Infrastructure/
└── frontend/
```

---

## Arranque rápido

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
cd ../../../frontend
npm run dev
```

---

## Reglas para agentes

1. Leer **`CLAUDE.md`** y **`docs/DEVELOPMENT-RULES.md`** antes de implementar.
2. Arquitectura → **`docs/ARCHITECTURE.md`**.
3. Estado y qué falta → **`docs/STATUS.md`**.
4. Prioridades → **`docs/ROADMAP.md`**.
5. Schema / RLS / migraciones → **`docs/DATABASE/`**.
6. No crear documentos fuera de la estructura oficial en `docs/`.
