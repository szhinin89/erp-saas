# Changelog — Frontend ERP SaaS

Formato basado en [Keep a Changelog](https://keepachangelog.com/).  
Versiones de **gobernanza visual** documentadas en [`RELEASES/`](../RELEASES/).

---

## [Frontend Governance v1.0] — 2026-05-21

### Added

- Documentación baseline: `docs/FRONTEND_ARCHITECTURE_BASELINE.md`, `FRONTEND_MAINTAINABILITY.md`, `FRONTEND_QA_CHECKLIST.md`.
- Release formal (histórico): `docs/archive/RELEASE-FRONTEND-GOVERNANCE-v1.0.md`.
- CSS platform: `menu-preview-sim.css` (`smp-*`), extensiones `smb-*` en `menu-plan-composer.css`.
- Utilidades globales: `pg-mt-4`, `pg-td-center` en `legacy-pages.css`.

### Changed

- Migración visual completa tenant (shells, templates, prefijos por dominio, `pg-*`).
- SuperAdmin Menu Builder: eliminación de inline estructural (~94 → 0 en CRM).
- `docs/frontend-layout-conventions.md` alineado al estado real.
- `PAGE-AUDIT.md` actualizado (inventario, gobernanza, platform).
- ESLint: prohibición `style={{}}` en `src/modules/**/pages/**` (error).

### Fixed

- `BranchFormModal` className duplicado (build).
- Micro-deuda inline: CompanyConfig, CreditNotes, CrearGasto, Carriers, CrearAjuste, SalesReport tooltip.

### Documented

- Excepciones inline válidas (auth, modales base, AccountTreeSelect, MenuPreview indent).
- Breakpoints: oficial 980px; excepciones 1024/760/640.
- Deuda `max-lines` — sin split pre-QA (`FRONTEND_MAINTAINABILITY.md`).

### Security / behaviour

- Sin cambios en auth, stores, APIs ni lógica de negocio en este milestone.

---

## [Unreleased]

### Planned (post-QA)

- Extracción opcional: `CreateInvoicePage`, `VentasFacturasPage`, `SuppliersPage`, `ProfilesPage`.
- Mover `useCompaniesPage.ts` a `hooks/`.
- Revisión `react-hooks/exhaustive-deps` residual.
