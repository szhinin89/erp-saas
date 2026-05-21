# Frontend — Mantenibilidad y deuda `max-lines`

**Fecha:** 2026-05-21  
**Relacionado:** [`FRONTEND_ARCHITECTURE_BASELINE.md`](./FRONTEND_ARCHITECTURE_BASELINE.md)

ESLint aplica `max-lines: 400` (warn, sin contar blanks/comentarios) en `src/modules/**/pages/**/*.{ts,tsx}`.

**Decisión pre-QA:** no dividir archivos en este milestone. La funcionalidad está estable; el exceso es marginal (32–89 líneas). Dividir ahora aumenta riesgo de regresión antes de QA formal.

---

## Inventario de warnings `max-lines` (2026-05-21)

| Archivo | Líneas (aprox.) | Exceso | Prioridad post-QA |
|---------|-----------------|--------|-------------------|
| `modules/compras/suppliers/pages/SuppliersPage.tsx` | 489 | +89 | Media |
| `modules/access/pages/ProfilesPage.tsx` | 476 | +76 | Media (modal RBAC) |
| `modules/ventas/pages/CreateInvoicePage.tsx` | 449 | +49 | Alta (documento largo) |
| `modules/ventas/pages/VentasFacturasPage.tsx` | 437 | +37 | Alta (listado + KPIs) |
| `modules/companies/pages/useCompaniesPage.ts` | 432 | +32 | Media (hook en carpeta pages) |

**Nota:** `useCompaniesPage.ts` coincide con el glob `pages/**`; es lógica de hook, no TSX de pantalla. Candidato a mover a `hooks/useCompaniesPage.ts` sin cambiar comportamiento.

---

## Límites de extracción recomendados (post-QA)

### CreateInvoicePage.tsx

| Extracción propuesta | Contenido | Tipo |
|----------------------|-----------|------|
| `CreateInvoiceCustomerSection.tsx` | Bloque búsqueda cliente + RUC/dirección/email | Presentacional + props |
| `CreateInvoiceLinesTable.tsx` | Tabla ítems + botón agregar línea | Presentacional + callbacks |
| Mantener en página | `useState`, `useAsync`, submit, `ErpPageTemplate` shell | Orquestación |

**No extraer:** cálculos (`calcInvoiceTotals`) — ya viven en `schemas/createInvoiceSchema.ts`.

### VentasFacturasPage.tsx

| Extracción propuesta | Contenido |
|----------------------|-----------|
| `VentasFacturasKpis.tsx` | Bloque 4 KPIs |
| `VentasFacturasTable.tsx` | Tabla + acciones fila (print, ride, retry) |
| Mantener en página | `load`, permisos, `ErpPageTemplate`, filtros |

### SuppliersPage.tsx

| Extracción propuesta | Contenido |
|----------------------|-----------|
| `SuppliersFormModal.tsx` o sección modal existente si aplica | Formulario/modal |
| `SuppliersListSection.tsx` | Listado + tabla |

### ProfilesPage.tsx

| Extracción propuesta | Contenido |
|----------------------|-----------|
| Ya existe `ProfilesPage.css` (`prf-*`) | Mantener estilos |
| `ProfilesRbacModal.tsx` | Modal permisos (bloque grande) |

### useCompaniesPage.ts

| Acción | Detalle |
|--------|---------|
| Mover a `modules/companies/hooks/useCompaniesPage.ts` | Alinea estructura módulo; elimina warning del glob `pages/**` |

---

## Otros warnings ESLint (no bloqueantes)

| Regla | Cantidad | Acción |
|-------|----------|--------|
| `react-refresh/only-export-components` | ~8 | Aceptado — exports auxiliares en archivos de componentes |
| `react-hooks/exhaustive-deps` | ~3 | Revisar caso a caso post-QA; no bloquean build |

---

## Criterios para dividir en el futuro

**Sí dividir cuando:**

- El archivo supera **500** líneas netas o crece en cada feature.
- Hay dos bloques JSX claramente independientes (listado vs modal, KPIs vs tabla).
- La extracción es **presentacional** (props + callbacks, sin nuevo estado global).

**No dividir cuando:**

- Solo se busca silenciar ESLint sin mejora cognitiva.
- La extracción generaría props drilling > 10 campos sin hook local.
- Estamos en ventana de QA / hotfix (riesgo > beneficio).

---

## Veredicto maintainability

| Aspecto | Estado |
|---------|--------|
| Bloqueante release / QA | **No** — warnings only |
| Deuda conocida | **Sí** — 5 archivos documentados |
| Plan post-QA | **Sí** — límites de extracción arriba |
| Riesgo actual | **Bajo** — archivos cohesionados por pantalla |
