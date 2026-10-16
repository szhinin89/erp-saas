# Features — ERP SaaS

## ACCOUNTING-CONTINUITY-03

Reverso histórico común implementado para pago proveedor, cobro cliente y
anulaciones existentes de Compra/Gasto/Retención, con adaptación mínima de Venta/NC.
Estado y validación: [entrega](docs/agent/ACCOUNTING_CONTINUITY_03_DELIVERY.md).

## ACCOUNTING-CONTINUITY-02

EXTEND: Contabilizaciones permite seleccionar hasta 100 obligaciones Pending/Failed,
introducir motivo obligatorio, confirmar y consultar resultados individuales del lote.
El reproceso individual también exige motivo. Se reutiliza accounting.postings.reprocess.
[Entrega](docs/agent/ACCOUNTING_CONTINUITY_02_DELIVERY.md).

Accounting: contrato documental consolidado; implementación parcial. Reglas y capacidades aprobadas únicamente en [Accounting](docs/architecture/accounting.md).

> Estado de delivery por módulo: [`STATUS.md`](STATUS.md) · Arquitectura y rutas API: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)

## Módulos producto (scope: empresa — `company_id`)

**BUG REAL de anulación de compra (2026-10-16):** Cancel informa bloqueo funcional
tras cualquier salida posterior del producto/bodega, incluso revertida, orientando
a devolución o nota de crédito. Entradas posteriores solas no lo bloquean.
Plazo ordinario SRI de retenciones desde política global por vigencia, con historia
conservada y fecha resuelta en UI. [Entrega](docs/agent/PURCHASE_CANCELLATION_DELIVERY.md).

**Cierre funcional 2026-09-12 — LISTO / CERRADO:** Compras (normal/XML, presentación e inventario, costos/Kardex, NC por devolución/descuento), Gastos (manual/XML, catálogos SRI reales, CxP y anulación), CxP y Pagos proveedor (parcial/total, reversa, bloqueo con pagos activos y aislamiento por empresa). Alcance, evidencia, commits, guardrails y pendientes no bloqueantes: [STATUS — cierre funcional](STATUS.md#close-purchases-expenses-payables-ready-01--cierre-funcional-2026-09-12); [matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No implica cierre contable global ni implementación de ampliaciones SRI fuera del alcance.

| Dominio | Rutas / API | Notas |
|---------|-------------|-------|
| Auth & acceso | `/login`, `/api/auth/*`, `/api/me` | JWT + refresh rotation, RBAC |
| Master data — Business Partners (Customer/Supplier) | `/api/master/business-partners*` | BP V2, roles Customer/Supplier — FROZEN (ver `STATUS.md`) |
| Carga Inicial — Clientes (IL-2) — **CLOSED** | `/initial-load/customers`, `/api/v1/initial-load/*` | Plantilla Excel validada con las mismas reglas que la confirmación; lote todo-o-nada en una transacción; reutiliza BP existente (asigna rol Cliente o idempotente, nunca duplica); condición de pago obligatoria por Company (`CompanyBpSalesSettings`); idempotente y seguro ante concurrencia/retry/cancelación. Gate UI real + PostgreSQL PASS (ver `STATUS.md`) |
| Carga Inicial — Proveedores (IL-3) — **CLOSED** | `/initial-load/suppliers`, `/api/v1/initial-load/*` | Solo RUC (04) y Exterior (08); validación con las mismas reglas que la confirmación; datos fiscales SI/NO explícitos sin default; reutiliza BP existente (asigna o reactiva rol Proveedor, idempotente, nunca duplica ni sobrescribe datos fiscales); condición de pago obligatoria por Company (`CompanyBpPurchaseSettings`); lote todo-o-nada, idempotente y seguro ante concurrencia/retry/cancelación. Gate UI real + PostgreSQL PASS (ver `STATUS.md`) |
| Carga Inicial — Inventario (IL-4) — **CLOSED** | `/initial-load/initial-stock`, `/api/v1/initial-load/*` | Saldo inicial al corte (nunca compras ficticias): un documento de apertura por bodega con movimiento propio `InitialBalance` ("Saldo Inicial") y la Fecha de Corte como fecha efectiva; solo ítem+bodega sin historia, sucursal activa y bodega por código; decimales invariantes sin redondeo; lote todo-o-nada, idempotente y seguro ante concurrencia/retry/cancelación; el documento no se anula desde Ajustes. Gate UI real + PostgreSQL PASS (ver `STATUS.md`) |
| Carga Inicial — Cuentas por Pagar (IL-6) — **EN CURSO (IL-6C)** | `/initial-load/initial-payables`, `/api/v1/initial-load/*` | Saldos NETOS pendientes al corte, nunca compras/gastos/pagos/retenciones históricas: `AccountsPayable` con origen `InitialBalance` (tipo SRI real del documento, número normalizado y lote propios, una cuota por el saldo, `OriginId` = fila del lote). IL-6A: modelo + migración, plantilla, validación y preview (proveedor existente con rol Proveedor, tipo SRI activo y de deuda, duplicados normalizados contra CxP de cualquier origen, emisión ≤ corte ≤ hoy, solo USD, corte = `Company.OpeningBalanceDate`, sucursal activa). IL-6B: confirmación todo-o-nada en una transacción con revalidación; saldo a favor de proveedor aplicable a la CxP inicial; la CxP inicial confirmada protege la fecha de apertura; listado/detalle muestran "Saldo inicial". IL-6C: idempotente y seguro ante concurrencia/retry/cancelación. Sin retención |
| Carga Inicial — Cuentas por Cobrar (IL-5) — **CLOSED** | `/initial-load/initial-receivables`, `/api/v1/initial-load/*` | Saldos pendientes al corte, nunca ventas históricas: `SalesReceivable` con origen `InitialBalance` (sin factura; número, emisión, sucursal y lote propios; una cuota por el saldo). IL-5A: plantilla, validación y preview (cliente existente con rol Cliente, duplicados normalizados contra CxC de factura y saldo inicial, emisión ≤ corte ≤ hoy, moneda obligatoria solo USD, corte único e igual a `Company.OpeningBalanceDate`). IL-5B: confirmación todo-o-nada en una transacción con revalidación; listado CxC muestra el saldo inicial con número, cliente, sucursal y emisión propios. IL-5C: idempotente y seguro ante concurrencia/retry/cancelación. |
| Carga Inicial — Apertura contable | `/initial-load` | Posting por lote, ASI y conciliación implementados en sus fases; adopción del contrato uniforme pendiente. Reglas contables exclusivamente en [Accounting](docs/architecture/accounting.md) |
| Catálogo / Ítems | `/api/v1/catalog/*`, `/api/v1/items/*` | 14 entidades, 56 endpoints, CRUD completo, variantes, atributos, SRI lookups. Empaques con presentación base obligatoria para inventariables, validación de factor, bloqueo de empaques usados y advertencias visuales sin inferencia automática. FROZEN v2.0 (rediseño flujo de creación, 2026-07-02) + auditoría por fases 1-9 (2026-07-02) |
| Motor de Pricing v2 | — (consumido internamente vía `IPricingResolver`) | `Item.BaseSalePrice` SSOT + `PricingRule` (reemplaza `ItemPrice`) + `PriceList` con regla general opcional. CLOSED (ADR-021, 2026-07-05) — ver `STATUS.md` |
| Tipos de Ítem | `/api/v1/item-types`, `/inventory/item-types` | Catálogo tenant-editable (reemplaza enum fijo); `items.item_type_id` FK por Guid; sin flags de comportamiento. FROZEN |
| Inventario | `/api/inventory/*` (categorías, líneas, tipos, marcas, tarifas) | Company isolation FROZEN |
| Compras | `Modules/Purchases` | Compra XML con checklist previo a confirmación, bloqueo fail-closed de líneas inventariables sin presentación, asociación proveedor+código+presentación y alerta de costo base extremo contra último/promedio. FASE 5 (2026-10-12): Cantidad/Costo de línea siempre en unidad base con frase "Se registrarán X a $Y c/u", estado de la compra (incompleto/listo/confirmada/anulada), Guardar permanece en la compra, listado con Proveedor/Total y búsqueda por proveedor/RUC. |
| Compras — mixtas y costos capitalizables (FASE 4) — **FUTURE** | Sin endpoints nuevos — auditoría 2026-10-11, sin cambios de código | **No implementar ahora; no es blocker del piloto.** Soportado hoy: solo el flujo manual existente — la línea de servicio/flete se elimina de las líneas operativas y su valor se distribuye entre los productos (`FreightAllocated`/`OtherCostsAllocated` → `TotalLineCost`/`LandedUnitCost`, costo en unidad base). Fuera de soporte completo: factura XML mixta productos + servicios capitalizables. Riesgos conocidos y detalle en `STATUS.md` § FUTURE — Compras mixtas. Evolución futura: servicio normal vs capitalizable; 1..N servicios capitalizables por factura; trazabilidad de asignación línea origen → línea destino; costo proveedor separado del costo de adquisición (landed cost); integración contable según [Accounting](docs/architecture/accounting.md). |
| Pagos a proveedor — anticipo por remanente (ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C, ADR-035) | `POST /api/v1/supplier-payments` (+`confirmUnappliedAmount`), `GET /api/v1/supplier-payments/policy`, `GET /api/v1/finance/supplier-credits*` (origen generalizado) | Pago mayor que la deuda o sin CxP (si la empresa activa `payables.allow_supplier_payment_without_payable`): el remanente, con confirmación explícita, queda como `SupplierCredit` de origen SupplierPayment (mismo libro de saldo que las devoluciones; aplicar/reembolsar sin cambios). La reversa solo procede si el anticipo sigue íntegro. |
| Saldos a favor de proveedor — reembolso | `/api/v1/finance/supplier-credits/*` | Reembolso/reversa operativos implementados. Adaptación de continuidad contable pendiente; contrato en [Accounting](docs/architecture/accounting.md) |
| Ventas | `/api/v1/sales/*`, `/sales` | SalesInvoice + Detail; Draft→Authorize→Cancel; snapshot fiscal IVA/ICE; DocumentSequence SRI; facturación electrónica. FROZEN |
| Devolución de Ventas (SalesReturn) + Nota de Crédito SRI | `/api/v1/sales/returns*`, `/sales/returns` | Devolución parcial/total sobre factura autorizada; Draft→Update→Cancel→Authorize; control de remanente bajo concurrencia (advisory lock); reversión de inventario; reembolso explícito Efectivo/Crédito CxC/mixto; Entity Audit; Nota de Crédito electrónica SRI V1.1.0 + RIDE (ADR-031). **P0-01 — COMPLETED/CLOSED (2026-07-31)**, ver `STATUS.md` |
| Configuración / SRI | `/api/settings/*`, catálogos SRI | Sucursales, geografía, parámetros |
| Sucursales | `/api/v1/settings/branches`, `/settings/branches`, `/settings/branches/:id` | CRUD + soft-disable; organizativas, no fiscales. FROZEN |
| Establecimientos SRI | `/api/v1/settings/establishments`, `/settings/establishments` | Código fiscal SRI (001-999) único por empresa; BranchId opcional; disable bloqueado si tiene PEs activos. FROZEN |
| Puntos de Emisión | `/api/v1/settings/emission-points`, `/settings/emission-points` | Código 001-999 único por Establecimiento; DocumentSequence automático. FROZEN |
| Acceso / Seguridad | `/api/admin/iam`, `/api/security`, `/api/admin/activity`, `/api/v1/admin/access/sessions*`, `/admin/access/sessions`, `/api/v1/admin/iam/company-users/{id}/preferences`, `/api/v1/admin/iam/memberships`, `/api/v1/admin/iam/memberships/revoke`, `/api/v1/admin/iam/memberships/{id}/branches`, `/admin/users` | Perfiles, permisos, auditoría; UserSession (contexto operativo empresa/sucursal/terminal) — ver `docs/IDENTITY.md#usersession-contexto-operativo-del-usuario`; CompanyUserPreferences (sucursal por defecto + modo de login por usuario, UI en `/admin/security`) — ver `docs/IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login`; **`/admin/users` — administración completa de `CompanyUserMembership`** (alta/edición de rol y perfil, sucursales autorizadas, preferencias de login, revocación/reactivación; `UsersPage`, Fase I-C, ver `STATUS.md`); **Security Hardening Fase S1** (ver `STATUS.md`) — `POST /api/v1/auth/register` y `POST /api/v1/auth/password-reset` eliminados (registro anónimo cross-tenant y reset de password sin verificación); alta de usuarios exclusivamente vía `/api/v1/setup/admin` (first-run) + administración de memberships; reset de password exclusivamente vía `forgot-password`/`reset-password` (token por email); endpoints admin de memberships/branches/preferences ahora exigen `IRequiresCompanyContext` (mismo patrón ya usado por alta/revocación de membership) |
| Contabilidad | `/accounting/*`, `/api/v1/accounting/*` | Core y lectura/reportes implementados. Modo operativo por empresa (`/accounting/configuration/operational-mode`) y contabilizaciones durables con reproceso manual individual (`/accounting/postings`) para Compra/Gasto/Retención; demás integraciones, reproceso en bloque y manuales generales pendientes. Única especificación: [Accounting](docs/architecture/accounting.md) |

## Plataforma SaaS (scope: tenant — `tenant_id`)

| Feature | Ruta | Notas |
|---------|------|-------|
| Empresas | `/companies`, `/api/companies` | Sin UUID en URL (sessionStorage `erp.saas.*`) |
| Tenant / onboarding | `Modules/Tenants`, `/api/setup` | Contrato SaaS — ver [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#jerarquía-multiempresa) |

> La capa de planes/billing/entitlements descrita en versiones anteriores de este documento fue **eliminada en FASE 1 — ERP Kernel Cleanup** (2026-06-05). No existen rutas `/superadmin/*` ni `/api/saas/*` activas — ver [`STATUS.md`](STATUS.md) y [`docs/archive/SAAS-COMMERCIAL.md`](docs/archive/SAAS-COMMERCIAL.md) (histórico).

## i18n

Español e Inglés.

### A1 — Items por empresa y naturaleza operativa

Catálogo aislado por Tenant + Company, sin ownership de Branch. SKU, variantes, barcode y códigos de proveedor permiten los mismos valores entre empresas. Product siempre registra inventario; Service no lo utiliza. Control general de Company e individual de Item gobiernan la validación de disponibilidad. Tipos de Ítem siguen siendo clasificación pura. [ADR-040](docs/decisions/ADR-040-item-company-scope-inventory-nature.md).
