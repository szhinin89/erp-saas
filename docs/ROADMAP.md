# Roadmap — ERP SaaS ZH Technologies

**Nivel 1** (ver jerarquía documental en [`CLAUDE.md`](../CLAUDE.md)). Prioridades y secuencia de evolución del producto. Para estado de delivery ya cerrado ver [`STATUS.md`](./STATUS.md); para módulos y rutas activas ver [`FEATURES.md`](../FEATURES.md); para el acta de congelamiento del ERP Core y la frontera con Platform ver [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md); para el razonamiento arquitectónico detrás de cada infraestructura FROZEN ver [`docs/adr/README.md`](./adr/README.md).

> Alcance: este roadmap cubre **ERP Core** exclusivamente. Todo lo relacionado a Billing/Subscription/Marketplace/PlatformOperator/CommercialPlan/Entitlements está excluido permanentemente del ERP por `ERP_CORE_FREEZE.md` y vive como referencia histórica en [`docs/archive/SAAS-COMMERCIAL.md`](./archive/SAAS-COMMERCIAL.md) y [`docs/future-platform/`](./future-platform/) — no forma parte de la secuencia de etapas de este documento.

**Reestructuración 2026-07-25 (Fase Dashboard 4.0):** este documento pasó de una secuencia de 10 "FASE N" a **7 Etapas** con nombre propio, para alimentar directamente `docs/ProgressDashboard/data/roadmap.json` y las secciones "Roadmap Maestro" / "Fase Actual" / "Próximas Fases" / "Estado Global" del dashboard. Ningún hecho documentado se perdió en la reorganización — la numeración "FASE N" anterior queda referenciada entre paréntesis en cada etapa para trazabilidad. CRM y RRHH no encajaban de forma natural en ninguna de las 7 etapas nuevas y se dejaron deliberadamente en una sección aparte (ver "Otras fases futuras — sin etapa asignada") en vez de forzarlas dentro de una etapa que no las describe con precisión.

**Reconciliación 2026-07-25 (Fase Gobernanza 1.0):** auditoría de este documento contra el código real (no solo contra `docs/STATUS.md`/ADR-026), verificada por lectura directa de `backend/src/ERP.Application/Modules/Accounting/Posting/`. Se corrigió una subestimación real: la Etapa 4 decía "sin iniciar" para el Posting Engine cuando en realidad ya está implementado y probado (partida doble real, 2 consumidores reales conectados vía eventos de dominio — Sales y Purchases). Se confirmó que el resto del documento (Etapa 1, Etapa 2, Etapas 5-7, CRM/RRHH) sí refleja el estado real del código — ningún otro hallazgo material. Detalle completo en las Etapas 3 y 4 más abajo.

---

## Resumen ejecutivo

| | Etapa | Estado |
|---|------|--------|
| **Etapa actual** | Etapa 1 — Completar ERP Core Operativo | En cierre de MVP comercial (~85-90%) |
| **Próxima etapa** | Etapa 2 — Consolidar Plataforma Base / Etapa 3 — Completar Accounting | Etapa 3 en progreso — Fundamentos (Plan de Cuentas/Períodos) implementados; Reglas de Contabilización con consumidor real (ver Etapa 4); Etapa 2 (hardening transversal) pendiente, no bloqueante |
| **Evolución futura** | Etapa 4 (Automatización Contable) → Etapa 5 (Business Intelligence) → Etapa 6 (Automatización) → Etapa 7 (Inteligencia Artificial) | Etapa 4 EN PROGRESO real (Posting Engine + partida doble + 2/4 consumidores implementados y probados; `Post()`/`Reverse()`, numeración y reportes financieros sin iniciar) — corregido 2026-07-25, antes decía "sin código". Etapas 5-7 sin código (salvo diseño previo de IA, ADR-009/010/012); prioridad de negocio pendiente de decisión del cliente para Etapa 6 |

Fuera de secuencia de etapas (dependencia externa, no entregable de este repo): **SaaS Platform**, repositorio separado, integra solo vía `/api/integration/v1/*` (`ERP_CORE_FREEZE.md`).

---

## Etapa 1 — Completar ERP Core Operativo — **ETAPA ACTUAL**

*(antes "FASE 1 — ERP Core")*

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
| Notas de Crédito/Débito *(antes FASE 3)* | 🟡 Solo XSD/catálogo, sin builder activo | Facturación | Baja | Alta (bloquea devoluciones formales) | Media | Reutilizar pipeline FROZEN `ElectronicDocumentIssuer` — nunca crear uno paralelo |
| Guías de remisión *(antes FASE 3)* | 🟡 Solo XSD/catálogo | Inventario (traslados) | Baja | Media | Media | Depende de trazabilidad completa de traslado físico en Inventario |
| Retenciones *(antes FASE 3)* | 🟡 Solo XSD/catálogo | Compras (retención en compra), Ventas (retención recibida) | Baja | Alta (obligación fiscal Ecuador) | Media-Alta | Requiere Liquidación de compra para el caso de retención sin factura de proveedor |

### Pendiente para cerrar Etapa 1

1. **Módulo de Cobros** — aplicar pago contra `SalesReceivableInstallment`. Hoy `SalesReceivable` (FROZEN) solo registra deuda, nunca cobra — es una decisión de diseño explícita, no un olvido, pero deja el ciclo de venta a crédito incompleto sin este módulo.
2. **Liquidación de compra** (tipo SRI 03) y **recepción física sin factura** — bloquean el flujo de retenciones en compra. Aclaración (Fase Gobernanza 1.0, verificado por lectura de código): el módulo `Purchases/PurchaseReception` ya existente **no** cubre este pendiente — es reconciliación/importación de comprobantes electrónicos SRI (TXT/XML) ya emitidos por el proveedor, no recepción física de mercadería sin factura.
3. **Cierre formal (ADR/freeze) de Caja** — hoy es funcional pero no tiene el mismo nivel de gobernanza que el resto de la Etapa 1.
4. **Validación SRI en ambiente de producción real** — ya validado en ambiente de Pruebas (`celcer.sri.gob.ec`) con certificado real (ver ADR-023); falta el ambiente productivo.
5. **Notas de Crédito/Débito, Guías de remisión, Retenciones** — extensión aditiva del núcleo `ElectronicDocuments` ya cerrado por ADR-023, bajo las mismas 4 causas de cambio permitido; no requiere ADR de cierre nuevo por sí sola.

No requiere ADR de cierre nuevo por sí sola — es extensión aditiva del núcleo ya cerrado por ADR-023.

---

## Etapa 2 — Consolidar Plataforma Base

*(antes sección transversal "Hardening y deuda técnica no bloqueante")*

Hardening e infraestructura transversal, no bloqueante para el MVP comercial pero requerido antes de escalar el volumen transaccional o abrir nuevas etapas de negocio (Accounting, BI).

| Item | Detalle | Bounded context |
|------|---------|-----------------|
| RLS PostgreSQL wave 2+ | Extender políticas a sales/purchasing/accounting cuando exista Accounting Core | Todos |
| Permissions cache | Wire `IPermissionsCacheService` en `RuntimePermissionAuthorizer` hot path | Access/IAM |
| Naming legacy `subscriber_*` | Limpieza mecánica de variables/índices SQL (dato ya migrado a `tenant_id`) | Todos |
| Outbox retention / event versioning en producción | ADR-010/011 aceptados, implementación diferida | Domain Events |

**Dependencias**: ninguna bloqueante — transversal a cualquier etapa.

**Riesgos/bloqueadores**: RLS PostgreSQL wave 2+ no implementado (aislamiento multi-tenant hoy 100% a nivel de aplicación vía `CompanyScopeBehavior` + EF query filters, FROZEN — ver `docs/DATABASE.md#rls`); Permissions cache con wiring parcial en el hot path de autorización.

---

## Etapa 3 — Completar Accounting

*(antes "FASE 2 — Accounting Core", parte fundamentos)*

Diseño aprobado en ADR-026 (`docs/adr/ADR-026-accounting-core.md`, `ACCEPTED`, 2026-07-24). Fundamentos de dominio, persistencia y CQRS/API ya implementados y auditados (`ERP.Domain/Modules/Accounting`, `ERP.Application/Modules/Accounting`, `ERP.Infrastructure/Accounting`, `AccountingController` en `api/v1/accounting`) — ver detalle en `docs/STATUS.md` (Fases 1, 1.2-1.4, 2.0-2.2).

**Reconciliado 2026-07-25 (Fase Gobernanza 1.0)** — verificado por lectura directa del código, no solo de la documentación: esta etapa está más avanzada de lo que la versión anterior de este documento indicaba (decía "Reglas de contabilización... sin ningún consumidor real todavía (Posting Engine no existe)", lo cual ya no es cierto — ver Etapa 4).

| Módulo | Estado | Dependencias | Madurez | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|---|
| Plan de cuentas | 🟡 Fundamentos (CRUD) | Company | Baja-Media | Alta | Media | CRUD completo (`Account`, jerarquía plana sin validación de ciclos todavía). Sigue sin definirse si el contenido inicial es catálogo tenant-editable (patrón `ItemTypeDefinition`) o plantilla estándar NIIF PYMES Ecuador — decisión de producto, no de arquitectura |
| Períodos contables | 🟡 Fundamentos (CRUD) | Plan de cuentas | Baja-Media | Alta | Media | `AccountingPeriod` con `Create`/`Close`/`Lock`; validación de solapamiento vía pre-check en Application, sin `EXCLUDE` constraint en BD (riesgo de carrera de baja probabilidad, ya documentado) |
| Reglas de contabilización | 🟢 Fundamentos + modelo de partida doble implementados, **con consumidor real** | Plan de cuentas | Media | Media | Media | `PostingRule`/`PostingRuleLine` (`AccountId`+`Nature`+`PostingAmountKind`) ya consumidos por el Posting Engine real (ver Etapa 4) — verificado leyendo `JournalFactory.cs`: itera `PostingRule.Lines` y genera líneas de asiento reales. Ya no es "configuración sin efecto contable" |

**Nota**: el módulo técnico `Accounting` es nuevo y todavía no está trackeado por el pipeline del dashboard (`explorer-index.json`) — cualquier referencia a él en `roadmap.json` dispara la advertencia de "módulo inexistente" del generador hasta que se incorpore al análisis estático.

---

## Etapa 4 — Automatización Contable

*(antes "FASE 2 — Accounting Core", parte automatización — Posting Engine)*

**Reconciliado 2026-07-25 (Fase Gobernanza 1.0)** — la versión anterior de esta sección decía "Posting Engine... siguen sin iniciar" y "`JournalEntry` existe solo como tabla/aggregate de identidad, sin líneas ni `Post()`/`Reverse()`". Verificado por lectura directa del código (no solo de `docs/STATUS.md`/ADR-026): eso ya no es cierto para el Posting Engine ni para las líneas de asiento — sí sigue siendo cierto para `Post()`/`Reverse()`. No se infla ni se reduce el avance real: cada fila de la tabla siguiente distingue explícitamente lo implementado de lo pendiente, con evidencia de código verificada.

| Componente | Estado | Evidencia verificada | Prioridad | Riesgos |
|---|---|---|---|---|
| Posting Engine (pipeline completo) | ✅ Implementado y probado | `ERP.Application/Modules/Accounting/Posting/`: `PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard` (advisory lock `pg_advisory_xact_lock`)/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`. Tests unitarios + integración PostgreSQL real (Testcontainers) | — | Ninguno activo conocido |
| Asientos con partida doble real (`JournalEntryLine`) | ✅ Implementado y probado | `JournalFactory.cs` construye líneas reales desde `PostingRule.Lines` + `PostingAmountKind` (Subtotal/TaxVat/TaxIce/Discount/GrandTotal; `Retention` resuelve a 0, `PostingFact` todavía no transporta ese monto); `JournalValidator.cs` ya no es NO-OP — valida mínimo 2 líneas, cuenta requerida, un solo monto por línea, sin cuenta duplicada en Débito+Crédito, balance real (`EnsureBalanced()`) | — | Ninguno activo conocido |
| Consumidores reales conectados | 🟡 2 de 4 planeados (Sales, Purchases) | `SalesInvoiceAuthorizedPostingTranslator.cs` / `PurchaseInvoiceConfirmedPostingTranslator.cs` — `INotificationHandler` sobre eventos de dominio ya existentes, fallo logueado sin revertir la operación de negocio | Alta | Caja e Inventory (ver fila siguiente) siguen sin conectar |
| Integración Caja / Inventory | ❌ No iniciado | Sin `IPostingEngine`/`PostingFact` referenciado en `ERP.Application/Modules/Caja` ni `ERP.Application/Modules/Inventory` (verificado, cero resultados) | Alta | Mismo patrón que Sales/Purchases ya validado — riesgo bajo de implementación, solo falta priorizar |
| `Post()` / `Reverse()` | ❌ No iniciado | `JournalEntry.cs` (comentario explícito): "quedan explícitamente fuera de esta fase (ADR-026 §6, §8) — pertenecen a un incremento posterior" | Alta | Máximo acoplamiento transversal del ERP — mismo riesgo estructural de siempre (ver abajo) |
| `JournalEntrySequence` (numeración formal) | ❌ No iniciado | Sin archivo/clase en el código — solo mencionado en comentarios XML como pendiente | Alta | — |
| Libro diario / Mayor (consulta) | ❌ No iniciado | Sin endpoint en `AccountingController` (solo CRUD de accounts/accounting-periods/posting-rules) ni query correspondiente | Alta | Los asientos ya se generan internamente pero no hay forma de consultarlos vía API todavía |
| Balance / Estado de resultados | ❌ No iniciado | Sin código relacionado encontrado | Alta | Requiere Libro Mayor primero |
| Cash & Banks (cuentas bancarias, conciliación) | ❌ No iniciado | Sin código relacionado encontrado | Media-Alta | Distinto de Caja/POS (Etapa 1, ya existe) — no reutilizar esa entidad para bancos |

**Riesgo estructural**: Accounting Core (Etapas 3+4) es el módulo de mayor acoplamiento transversal del ERP — todo documento transaccional eventualmente debe generar un asiento. El Posting Engine ya resolvió esto para Sales/Purchases con un único punto de consumo de eventos de dominio (nunca disparadores ad-hoc por módulo) — extender a Caja/Inventory debe seguir exactamente el mismo patrón ya validado.

**Precondición recomendada**: cerrar los pendientes de Etapa 1 (Cobros, Liquidación de compra) antes de completar `Post()`/`Reverse()` y los reportes (Libro Mayor/Balance), para no construir reportes financieros contra un dominio transaccional todavía incompleto. La parte ya implementada (Posting Engine + Sales/Purchases) no dependía de esta precondición y por eso pudo avanzar en paralelo.

---

## Etapa 5 — Business Intelligence

*(antes "FASE 4 — Business Intelligence")*

| Módulo | Estado | Dependencias | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|
| Business Intelligence | ❌ No iniciado | Accounting Core (Etapas 3+4) + histórico transaccional real | Baja hasta tener data contable consolidada | Depende del alcance (dashboards operativos vs. data warehouse) | Prematuro sin Etapas 3/4 cerradas |

---

## Etapa 6 — Automatización

*(antes "FASE 7 — Producción" y "FASE 8 — Manufactura")*

| Módulo | Estado | Dependencias | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|
| Producción | ❌ No iniciado | Inventario (consumo de materiales), Items (BOM/recetas) | Baja (decisión de negocio) | Alta | Requiere extender Items más allá de "clasificación pura sin comportamiento" (regla congelada) — exige ADR nueva, no extensión menor |
| Manufactura | ❌ No iniciado | Superset de Producción | Baja (decisión de negocio) | Alta | Mismo riesgo que Producción sobre infraestructura CLOSED de Items |

---

## Etapa 7 — Inteligencia Artificial

*(antes "FASE 10 — IA")*

| Módulo | Estado | Dependencias | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|
| IA | Diseño previo (ADR-009/010/012), sin producto | Outbox/eventos poblados por uso real (Etapas 1-4) | Baja hasta tener datos reales | Alta | Prematuro sin volumen transaccional real que un read-model de IA pueda consumir |

---

## Otras fases futuras — sin etapa asignada (pendiente de decisión de negocio)

CRM y RRHH no encajan con precisión semántica en ninguna de las 7 etapas anteriores (no son "automatización" ni "accounting" ni "BI"/"IA") — se documentan aparte para no forzar una categorización que no las describe correctamente.

| Módulo | Estado | Dependencias | Prioridad | Complejidad | Riesgos |
|---|---|---|---|---|---|
| CRM | ❌ No iniciado | Business Partners V2 (FROZEN, base sólida para extender a oportunidades/pipeline) | Media (decisión de negocio) | Media | Bajo — se apoya en infraestructura ya cerrada (BP, Audit, Pricing) |
| RRHH | ❌ No iniciado | Organización / IAM | Baja-Media (decisión de negocio) | Media | Sin dependencias fuertes de otros módulos ERP |

Priorización pendiente de decisión de negocio del cliente (ver `docs/adr/README.md` para el criterio "¿funcionaría sin ZH Platform?" aplicado a cada nueva etapa antes de iniciarla).

> **SaaS Platform** no aparece en esta tabla porque no es una etapa de desarrollo de este repositorio: es un producto externo que consume `/api/integration/v1/*` (`ERP_CORE_FREEZE.md`). Su evolución (planes, billing, multi-tenant comercial) se planifica en el repositorio de Platform, no aquí.

---

## Deferred (no bloqueante, backlog explícito)

- OC — recepción física sin factura (ver Etapa 1, pendientes)
- Liquidación de compra (tipo SRI 03) (ver Etapa 1, pendientes)
- Partitioning `electronic_doc` / `stock_movement` a escala (optimización futura, sin señal de necesidad actual)
- Impersonación operador Platform — audit log (fuera del ERP Core, ver nota de alcance)

---

## Relacionados

- [`STATUS.md`](./STATUS.md) — qué está cerrado y con qué evidencia
- [`FEATURES.md`](../FEATURES.md) — rutas/API activas por módulo
- [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md) — frontera ERP↔Platform, módulos incluidos/excluidos
- [`docs/adr/README.md`](./adr/README.md) — decisiones arquitectónicas y su estado
- [`docs/DEVELOPMENT.md`](./DEVELOPMENT.md) — cómo contribuir de forma segura
- [`docs/ProgressDashboard/data/roadmap.json`](./ProgressDashboard/data/roadmap.json) — misma estructura de 7 etapas, consumida por el dashboard (`tools/dashboard/render-dashboard.ps1`)
