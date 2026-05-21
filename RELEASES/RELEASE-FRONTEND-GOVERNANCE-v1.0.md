# Release — Frontend Governance v1.0

**Tag sugerido:** `frontend-governance-v1.0`  
**Fecha:** 2026-05-21  
**Alcance:** Baseline visual y de gobernanza del frontend ERP SaaS (sin feature freeze funcional).

---

## Resumen ejecutivo

El frontend alcanza un **baseline enterprise** para tenant y plataforma:

- Arquitectura de capas sellada: `AppLayout` / `SuperAdminLayout` → `LayoutFrame` → plantillas → contenido.
- Migración visual tenant completada con prefijos CSS gobernados y utilidades `pg-*`.
- SuperAdmin Menu Builder normalizado (`smp-*`, `smb-*`) aislado del tenant.
- ESLint bloquea `style={{}}` estructural en páginas de módulo.
- Documentación alineada con el código real.

Este release **no** cambia contratos API ni comportamiento de negocio; formaliza gobernanza y prepara **QA manual sistemático**.

---

## Hitos incluidos

### Arquitectura y plantillas

- `LayoutFrame` único en shells (tenant + platform).
- `ErpPageTemplate` como default tenant; `PageShell` directo en catálogos tabs.
- `SuperAdminCrudTemplate` en rutas `/superadmin/*`.
- `ReportPageTemplate` como excepción de dominio reportes.

### Migración visual (lotes 2026-05-21)

- Ventas, compras, inventario, contabilidad (5 tabs), catálogo, clientes, sucursales.
- SRI, billing, RBAC perfiles, CreateInvoice / CreateCreditNote.
- Micro-deuda tenant cerrada: CompanyConfig, CreditNotes listado, CrearGasto, Carriers, CrearAjuste.
- Platform: MenuPreview + SuperAdminMenuBuilder CRM (~94 inline → 0 estructural).

### Gobernanza

- `docs/frontend-layout-conventions.md` sincronizado.
- `frontend/src/templates/PAGE-AUDIT.md` inventario clase A/B/C.
- ESLint: `error` en `style` para `src/modules/**/pages/**` (excepción auth).
- Breakpoint oficial documentado: **980px**.

### Documentación baseline (este milestone)

- [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](../docs/FRONTEND_ARCHITECTURE_BASELINE.md)
- [`docs/FRONTEND_MAINTAINABILITY.md`](../docs/FRONTEND_MAINTAINABILITY.md)
- [`docs/FRONTEND_QA_CHECKLIST.md`](../docs/FRONTEND_QA_CHECKLIST.md)
- [`frontend/CHANGELOG.md`](../frontend/CHANGELOG.md)

---

## Validación técnica

```bash
cd frontend
npm run lint   # 0 errors (warnings: max-lines, react-refresh, hooks deps)
npm run build  # OK
```

---

## Deuda conocida (no bloqueante)

| Ítem | Severidad | Plan |
|------|-----------|------|
| 5 archivos `max-lines` > 400 | Baja | Post-QA — ver `FRONTEND_MAINTAINABILITY.md` |
| `react-hooks/exhaustive-deps` (3) | Baja | Revisión incremental |
| i18n hardcode en algunos listados | Media | Fuera alcance visual |
| `ReportPageTemplate` vs `ErpPageTemplate` | Baja | Migración opcional |

---

## Riesgos residuales

- **Drift visual:** mitigado por ESLint + documentación; requiere disciplina en PR.
- **Regresión QA:** foco en tablas, modales y ≤980px en rutas del checklist.
- **Archivos largos:** mantenibilidad aceptable; split diferido post-QA.

---

## Veredicto

| Criterio | Estado |
|----------|--------|
| QA-ready | **Sí** — checklist publicado |
| Enterprise baseline sealed | **Sí** |
| Governance maturity | **Alta** — reglas + inventario + enforcement |
| Maintainability | **Aceptable** — warnings documentados |

---

## Referencias

- Arquitectura monorepo: [`RELEASE-ARCHITECTURE-v1.0.md`](./RELEASE-ARCHITECTURE-v1.0.md)
- Convenciones layout: [`docs/frontend-layout-conventions.md`](../docs/frontend-layout-conventions.md)
