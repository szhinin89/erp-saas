# Roadmap — ERP SaaS ZH Technologies

**Nivel 1** (ver jerarquía documental en [`CLAUDE.md`](../CLAUDE.md)). Prioridades y secuencia de evolución del producto. Para estado de delivery ya cerrado ver [`STATUS.md`](./STATUS.md); para módulos y rutas activas ver [`FEATURES.md`](../FEATURES.md); para el acta de congelamiento del ERP Core y la frontera con Platform ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md); para el razonamiento arquitectónico detrás de cada infraestructura FROZEN ver [`docs/adr/README.md`](./adr/README.md).

> Alcance: este roadmap cubre **ERP Core** exclusivamente. Todo lo relacionado a Billing/Subscription/Marketplace/PlatformOperator/CommercialPlan/Entitlements está excluido permanentemente del ERP por `ERP_CORE_FREEZE.md` y vive como referencia histórica en [`docs/archive/SAAS-COMMERCIAL.md`](./archive/SAAS-COMMERCIAL.md) y [`docs/future-platform/`](./future-platform/) — no forma parte de la secuencia de fases de este documento.

---

## Resumen ejecutivo

| | Fase | Estado |
|---|------|--------|
| **Current Phase** | FASE 1 — ERP Core | En cierre de MVP comercial (~85-90%) |
| **Next Phase** | FASE 2 — Accounting Core | En progreso — Fundamentos (Plan de Cuentas/Períodos/Reglas de Contabilización) implementados; Asientos/Posting Engine sin iniciar |
| **Future Evolution** | FASE 3 (completar Electronic Documents) → FASE 4 BI → FASE 5 CRM → FASE 6 RRHH → FASE 7 Producción → FASE 8 Manufactura → FASE 10 IA | Sin código; prioridad de negocio pendiente de decisión del cliente |

Fuera de secuencia de fases (dependencia externa, no entregable de este repo): **FASE 9 — SaaS Platform**, repositorio separado, integra solo vía `/api/integration/v1/*` (`ERP_CORE_FREEZE.md`).

---

## FASE 1 — ERP Core — **CURRENT PHASE**

| Módulo | Estado | Dependencias | Madurez | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|---|
| Organización (Tenant/Company/Sucursales/Establecimientos/PE) | ✅ FROZEN | — | Alta | — | — | Ninguno activo |
| IAM / Access | ✅ FROZEN | Organización | Alta | — | — | Deuda cosmética `subscriber_*` en naming interno, no bloqueante |
| Productos / Items / Pricing | ✅ FROZEN | Organización | Alta | — | — | — |
| Inventario | 🟡 Funcional, mejorable | Items | Media-Alta | Media | Media | Sin flujo de aprobación en Transferencias/Ajustes; sin reversar ajuste |
| Compras | 🟡 Funcional, mejorable | Items, BP-Supplier | Media-Alta | Alta | Media | Sin liquidación de compra (tipo SRI 03); sin recepción física sin factura; sin validación precio OC vs Factura |
| Ventas (Invoice + CxC deuda) | ✅ FROZEN (deliberadamente parcial: sin cobros) | Items, BP-Customer, Pricing | Alta | Alta | Media | **Cobros** es el hueco funcional más visible del MVP comercial actual |
| Caja (CashRegister/CashSession/CashMovement) | 🟡 Funcional, sin cierre formal | Ventas | Media | Media | Baja | Sin ADR/freeze — evolución no gobernada hasta que se cierre |
| Clientes / Proveedores (BP V2) | ✅ FROZEN | — | Alta | — | — | — |
| Facturación Electrónica SRI (solo Invoice) | ✅ FROZEN (v1.0) | Ventas | Alta | Crítica | Alta | Cambios solo bajo las 4 causas de ADR-023 |

### Pendiente para cerrar Fase 1

1. **Módulo de Cobros** — aplicar pago contra `SalesReceivableInstallment`. Hoy `SalesReceivable` (FROZEN) solo registra deuda, nunca cobra — es una decisión de diseño explícita, no un olvido, pero deja el ciclo de venta a crédito incompleto sin este módulo.
2. **Liquidación de compra** (tipo SRI 03) y **recepción física sin factura** — bloquean el flujo de retenciones en compra (FASE 3).
3. **Cierre formal (ADR/freeze) de Caja** — hoy es funcional pero no tiene el mismo nivel de gobernanza que el resto de Fase 1.
4. **Validación SRI en ambiente de producción real** — ya validado en ambiente de Pruebas (`celcer.sri.gob.ec`) con certificado real (ver ADR-023); falta el ambiente productivo.
5. **RLS PostgreSQL wave 2+** — hoy el aislamiento multi-tenant es 100% a nivel de aplicación (`CompanyScopeBehavior` + EF query filters, FROZEN); RLS a nivel de base de datos sigue en ❌ (ver `docs/DATABASE.md#rls`). No bloquea el MVP pero es hardening de defensa en profundidad pendiente.
6. **Permissions cache** — servicio existe, wiring en el hot path de autorización (`RuntimePermissionAuthorizer`) es parcial.

---

## FASE 2 — Accounting Core — **NEXT PHASE**

Diseño aprobado en ADR-026 (`docs/adr/ADR-026-accounting-core.md`, `ACCEPTED`). Fundamentos de dominio, persistencia y CQRS/API ya implementados y auditados (`ERP.Domain/Modules/Accounting`, `ERP.Application/Modules/Accounting`, `ERP.Infrastructure/Accounting`, `AccountingController` en `api/v1/accounting`) — ver detalle en `docs/STATUS.md`. Posting Engine, `JournalEntryLine`, numeración (`JournalEntrySequence`) e integración con Sales/Purchases/Caja/Inventory vía eventos siguen sin iniciar.

| Módulo | Estado | Dependencias | Madurez | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|---|
| Plan de cuentas | 🟡 Fundamentos (CRUD) | Company | Baja-Media | Alta | Media | CRUD completo (`Account`, jerarquía plana sin validación de ciclos todavía). Sigue sin definirse si el contenido inicial es catálogo tenant-editable (patrón `ItemTypeDefinition`) o plantilla estándar NIIF PYMES Ecuador — decisión de producto, no de arquitectura |
| Períodos contables | 🟡 Fundamentos (CRUD) | Plan de cuentas | Baja-Media | Alta | Media | `AccountingPeriod` con `Create`/`Close`/`Lock`; validación de solapamiento vía pre-check en Application, sin `EXCLUDE` constraint en BD (riesgo de carrera de baja probabilidad, ya documentado) |
| Reglas de contabilización | 🟡 Fundamentos (CRUD) | Plan de cuentas | Baja-Media | Media | Media | `PostingRule` como configuración de mapeo pura — todavía sin ningún consumidor (Posting Engine no existe), por lo que hoy no tiene efecto contable real |
| Asientos contables | ❌ No iniciado | Plan de cuentas + todos los documentos transaccionales (Ventas/Compras/Caja) | — | Alta | Alta | Máximo acoplamiento transversal del ERP: motor de generación automática de asientos desde eventos de dominio. Debe apoyarse en Domain Events + Outbox ya FROZEN (ADR-007/008) — nunca reimplementar disparo de eventos por módulo. `JournalEntry` existe solo como tabla/aggregate de identidad, sin líneas ni `Post()`/`Reverse()` |
| Libro diario / Mayor | ❌ No iniciado | Asientos | — | Alta | Media | — |
| Balance / Estado de resultados | ❌ No iniciado | Mayor | — | Alta | Media-Alta | Requiere asientos reales — sin iniciar hasta que exista el Posting Engine |
| Cash & Banks (cuentas bancarias, conciliación) | ❌ No iniciado | Caja, Asientos | — | Media-Alta | Media | Distinto de Caja/POS (Fase 1, ya existe) — no reutilizar esa entidad para bancos |

**Riesgo estructural**: Accounting Core es el módulo de mayor acoplamiento transversal del ERP — todo documento transaccional eventualmente debe generar un asiento. Diseñar antes de codificar (siguiente etapa de trabajo) para no crear disparadores de asientos ad-hoc por módulo, sino un único punto de consumo de eventos de dominio.

**Precondición recomendada**: cerrar los pendientes de Fase 1 (Cobros, Liquidación de compra) antes de completar Accounting, para no generar el motor de asientos contra un dominio transaccional todavía incompleto.

---

## FASE 3 — Electronic Documents (completar) — Future Evolution (corto plazo)

| Módulo | Estado | Dependencias | Madurez | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|---|
| Facturación electrónica (Invoice) | ✅ FROZEN | Ventas | Alta | — | — | Cambios solo bajo ADR-023 |
| Notas de Crédito/Débito | 🟡 Solo XSD/catálogo, sin builder activo | Facturación | Baja | Alta (bloquea devoluciones formales) | Media | Reutilizar pipeline FROZEN `ElectronicDocumentIssuer` — nunca crear uno paralelo |
| Guías de remisión | 🟡 Solo XSD/catálogo | Inventario (traslados) | Baja | Media | Media | Depende de trazabilidad completa de traslado físico en Inventario |
| Retenciones | 🟡 Solo XSD/catálogo | Compras (retención en compra), Ventas (retención recibida) | Baja | Alta (obligación fiscal Ecuador) | Media-Alta | Requiere Liquidación de compra (Fase 1) para el caso de retención sin factura de proveedor |

No requiere ADR de cierre nuevo por sí sola — es extensión aditiva del núcleo ya cerrado por ADR-023, bajo las mismas 4 causas de cambio permitido.

---

## Future Evolution — Fases 4 a 10

| Fase | Módulo | Estado | Dependencias | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|---|
| 4 | Business Intelligence | ❌ No iniciado | Accounting Core + histórico transaccional real | Baja hasta tener data contable consolidada | Depende del alcance (dashboards operativos vs. data warehouse) | Prematuro sin Fase 2 cerrada |
| 5 | CRM | ❌ No iniciado | Business Partners V2 (FROZEN, base sólida para extender a oportunidades/pipeline) | Media (decisión de negocio) | Media | Bajo — se apoya en infraestructura ya cerrada (BP, Audit, Pricing) |
| 6 | RRHH | ❌ No iniciado | Organización / IAM | Baja-Media (decisión de negocio) | Media | Sin dependencias fuertes de otros módulos ERP |
| 7 | Producción | ❌ No iniciado | Inventario (consumo de materiales), Items (BOM/recetas) | Baja (decisión de negocio) | Alta | Requiere extender Items más allá de "clasificación pura sin comportamiento" (regla congelada) — exige ADR nueva, no extensión menor |
| 8 | Manufactura | ❌ No iniciado | Superset de Producción | Baja (decisión de negocio) | Alta | Mismo riesgo que Fase 7 sobre infraestructura CLOSED de Items |
| 10 | IA | Diseño previo (ADR-009/010/012), sin producto | Outbox/eventos poblados por uso real (Fases 1-3) | Baja hasta tener datos reales | Alta | Prematuro sin volumen transaccional real que un read-model de IA pueda consumir |

> **FASE 9 — SaaS Platform** no aparece en esta tabla porque no es una fase de desarrollo de este repositorio: es un producto externo que consume `/api/integration/v1/*` (`ERP_CORE_FREEZE.md`). Su evolución (planes, billing, multi-tenant comercial) se planifica en el repositorio de Platform, no aquí.

Priorización de Fases 4-8 pendiente de decisión de negocio del cliente real (ver `docs/adr/README.md` para el criterio "¿funcionaría sin ZH Platform?" aplicado a cada nueva fase antes de iniciarla).

---

## Hardening y deuda técnica no bloqueante (transversal, cualquier fase)

| Item | Detalle | Bounded context |
|------|---------|-----------------|
| RLS PostgreSQL wave 2+ | Extender políticas a sales/purchasing/accounting cuando exista Accounting Core | Todos |
| Permissions cache | Wire `IPermissionsCacheService` en `RuntimePermissionAuthorizer` hot path | Access/IAM |
| Naming legacy `subscriber_*` | Limpieza mecánica de variables/índices SQL (dato ya migrado a `tenant_id`, ver FASE 2 rename en `STATUS.md`) | Todos |
| Outbox retention / event versioning en producción | ADR-010/011 aceptados, implementación diferida | Domain Events |

## Deferred (no bloqueante, backlog explícito)

- OC — recepción física sin factura (ver FASE 1 pendientes)
- Liquidación de compra (tipo SRI 03) (ver FASE 1/3 pendientes)
- Partitioning `electronic_doc` / `stock_movement` a escala (optimización futura, sin señal de necesidad actual)
- Impersonación operador Platform — audit log (fuera del ERP Core, ver nota de alcance)

---

## Relacionados

- [`STATUS.md`](./STATUS.md) — qué está cerrado y con qué evidencia
- [`FEATURES.md`](../FEATURES.md) — rutas/API activas por módulo
- [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md) — frontera ERP↔Platform, módulos incluidos/excluidos
- [`docs/adr/README.md`](./adr/README.md) — decisiones arquitectónicas y su estado
- [`docs/DEVELOPMENT.md`](./DEVELOPMENT.md) — cómo contribuir de forma segura
