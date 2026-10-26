# Roadmap — ERP ZH V1

Planificación futura, sin autoridad funcional ni autorización de implementación.
Capacidades actuales: [FEATURES](../FEATURES.md). Gates/cierres: [STATUS](../STATUS.md).
Las siete etapas son categorías de planificación; no porcentajes ni certificaciones de cierre.
Platform/Billing/Subscription/Marketplace/Entitlements quedan fuera del ERP Core:
[frontera](../ERP_CORE_FREEZE.md).

## Etapa 1 — Completar ERP Core

- Órdenes de compra, recepción física sin factura y liquidación de compra SRI 03:
  pendientes dentro de los límites de [Compras](architecture/purchases.md#8-pendiente-no-operativo).
- Aprobación de Compras: intención futura de modos Direct/Approval/MultiApproval,
  sin estados, comandos, parámetros ni tablas preventivas. Mantener el agregado de Compra,
  su confirmación y efectos únicos. Niveles, aprobadores, umbrales, reapertura y notificaciones
  requieren definición de negocio. Generalizar workflow solo con al menos dos necesidades reales.
- Gastos: adjuntos, reportes operativos, permisos finos, filtros/exportación y eventual
  aprobación SingleStep deben delimitarse por fase. No volver a planificar emisión de
  retenciones, XML/RIDE ni CxP como inexistentes: ya tienen consumidores operativos.
- Nuevos tipos electrónicos (guías/débito y extensiones sectoriales) requieren alcance
  propio y respetar [ElectronicDocuments](architecture/frozen-infrastructure.md).
- Lotes/series, reservas y anulación de transferencias: límites actuales en
  [Inventario](architecture/inventory.md#8-pendiente-no-operativo); sin obligación V1 implícita.

## Etapa 2 — Consolidar plataforma técnica del ERP

### Configuración jerárquica

Implementado (no planificar de nuevo): catálogo de definiciones, guardrail de escritura,
resolvers tipados, principal/default únicos, concurrencia, historial y branding de Company en
[Backend § Configuración organizacional](architecture/backend.md#configuración-organizacional);
defaults de factura en [Valores por Defecto de Facturación](architecture/frozen-infrastructure.md#valores-por-defecto-de-facturación).

Pendiente, sin autorización de implementación:

- Contextos de consumo de Purchases, Inventory, Cash y Settings (solo existe el de Sales).
- Snapshots interpretativos de bodega, punto de emisión, método de pago, lista de precios,
  ambiente y certificado (`RequiresSnapshot` no tiene consumidor). Conservar FK para
  trazabilidad; no sustituir snapshot por lectura de configuración viva.
- Metadatos de presentación y de acceso por definición; UI del historial de cambios.
- Lista de precios por cliente (`CompanyBpTradingSettings` no la tiene), scopes adicionales
  (caja, usuario, perfil, tercero), preferencias de usuario/formulario separadas de la
  configuración de negocio y branding por sucursal, solo ante necesidad real.

No migrar precisión hacia OrgSettings: [CompanyPrecisionPolicy](architecture/data-standards.md)
es la autoridad vigente. PaymentTerm/CreditTerm y defaults del tercero conservan su
[decisión diferenciada](decisions/ADR-033-payment-term-ssot-and-document-schedules.md).
No reabrir secuencias, fiscalidad ni otras infraestructuras CLOSED desde este diseño.

### Convergencia UX y adopción técnica

Intenciones todavía no autorizadas para implementar, procedentes de la auditoría UX:
convergencia de tabs/notices/confirmaciones, inputs DS, acciones y paginación; metadatos
de menú e i18n; ayuda contextual fiscal; consumidores de pickers; accesibilidad y plantillas
de página. Antes de cada fase comprobar vigencia del hallazgo y autorización sobre CLOSED.
Las cifras y ubicaciones del HEAD de septiembre no describen el árbol actual.
Idempotencia financiera, identidad de empresa, clientes HTTP y emisión de retenciones
ya tienen reglas vigentes en [Backend](architecture/backend.md) y [Frontend](architecture/frontend.md);
no se vuelven a abrir como defectos por aparecer en la auditoría anterior.
GOV-002 mantiene su seguimiento únicamente en [WORK_QUEUE](agent/WORK_QUEUE.md).

Otras intenciones: evolución RLS solo tras alcance aprobado, optimizaciones de permisos,
retención de Outbox y particionado por evidencia de carga; ninguna modifica el aislamiento
fail-closed actual. Process Audit, UI de jobs/collector del worker y métricas de carga
requieren fases propias. Operación actual: [runbook](deployment/README.md).

## Etapa 3 — Evolución de Accounting

No se planifican como ausentes el core, manuales, períodos, reglas ni reportes existentes.
Cualquier ampliación parte de [Accounting](architecture/accounting.md), sin segundo motor,
política de compatibilidad ni reconstrucción de hechos comerciales históricos.

## Etapa 4 — Integraciones financieras futuras

Conciliación bancaria y ampliaciones de Cash & Banks requieren alcance propio.
No confundirlas con bancos, pagos, cobros y caja ya implementados.

## Etapa 5 — Business Intelligence

Exploración futura de lectura/analítica sobre hechos reales y contratos del ERP.
Depende de alcance y datos suficientes; no autoriza consultas que omitan tenant/company.

## Etapa 6 — Automatización

Producción/manufactura, BOM y recetas requieren definición de negocio y evaluación
explícita de impacto sobre Ítems e Inventario CLOSED. No hay autorización implícita.

## Etapa 7 — Inteligencia Artificial

Evolución sujeta a [fronteras IA](architecture/ai-foundation.md), contratos de eventos
y volumen real de datos. No introduce dependencias IA en Domain/Application.

## Otras fases futuras — sin etapa asignada

CRM y RRHH dependen de priorización de negocio; no se fuerzan dentro de otra etapa.
La evolución de SaaS Platform corresponde a su producto externo.

## Relacionados

- [Índice normativo](architecture/README.md).
- [Decisiones arquitectónicas](decisions/README.md).
- [Desarrollo](DEVELOPMENT.md).
- [Dashboard](ProgressDashboard/data/roadmap.json): proyección manual de planificación;
  sus datos no acreditan capacidades, gates ni cierre. Reconciliación de la proyección
  pendiente; no regenerarla ni inferir porcentajes desde esta limpieza documental.


### Hipótesis UX pendientes de revalidación por fase

IDs conservados de la auditoría previa; sin afirmar vigencia ni autorizar reaperturas.

| ID | Intención a evaluar |
|---|---|
| P1-01 | Comandos de **creación** de dinero sin idempotencia de servidor: pago a proveedor (`POST /supplier-payments`), cobro CxC (`POST payments/collections`), movimiento de caja (`POST cash-sessions/{id}/movements`). Defensa solo de UI (`saving` de React; en caja el `setSaving(true)` ocurre después del `await message.confirm`) |
| P2-01 | Vínculo NC↔devolución de compra con dos endpoints/representaciones |
| P2-07 | Dos pipelines de RIDE (`/ride` con caché vs `/retentions/{id}/ride/pdf` on-demand) |
| P3-01 | Tabs `.prd-tab-btn` a mano (13 archivos) + `md-detail-tab` |
| P3-02 | `.prd-error-banner` como segundo diseño de error |
| P3-03 | Dos acciones `primary` simultáneas en confirmación de compra y en retención |
| P3-04 | `type="number"` ×4 / `type="date"` ×6 crudos |
| P3-05 | Paginación propia en listados admin |
| P3-06 | Listado `CompanyManagementListPage` y `ReturnableLinesEditor` (ventas) con `<table>` |
| P3-07 | Descripciones de menú hardcodeadas (`MENU_DESCRIPTION_BY_ROUTE_PREFIX`) |
| P3-08 | ZH-HELP solo en Ventas/Compras; ayudas en `title` largos |
| P3-09 | `<select>` crudos (37) |
| P4-01 | `ZHConfirmModal` directo (11) vs `message.confirm` |
| P4-02 | `ZHFormAlert` directo (6) |
| P4-03 | DS sin consumidores: `ZHActionNotice`, `ZHInlineHint`, `ZHHelpDrawer` |
| P4-04 | `font-size` px mágicos (47), radios/sombras literales |
| P4-05 | Dos toggles de password, avisos locales del wizard de socios, `sf-cash-session-notice`, `prd-draft-banner` |
| P4-06 | HTTP en páginas de auth y `VariantsSection` fuera de `api/` |
| P4-07 | `ConfigContext` decide por rol (`isAdminRole`) en vez de permiso |
| P4-08 | `ErpPageTemplate` y `PageShell` sin decisión de plantilla canónica |


### Extensiones de presentación, entrega y trazabilidad

- RIDE: resolución jerárquica avanzada de plantillas; verificar por fase la integración
  de metadata/cache de Retención antes de formular una deuda actual.
- Communications: monitor, reenvío UI, canales adicionales, vencimiento y manejo durable
  de payload sensible cifrado con eliminación segura requieren fases propias. Un propósito
  reservado no acredita productor ni plantilla; Auth conserva tokens y seguridad.
- Secuencias: auditoría granular de captura/configuración y necesidad de series por
  ambiente requieren evidencia y aprobación; no se crean tabla ni clave alternativa ahora.
