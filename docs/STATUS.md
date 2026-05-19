# ESTADO DE DESARROLLO — ERP SaaS ZH Technologies

Arquitectura → `ARCHITECTURE.md` | Reglas → `CLAUDE.md` | Funcionalidades → `FEATURES.md` | Tracker detallado → `../PROGRESS.html`

Última actualización: **2026-05-18**

> **Sincronización:** si modificas este archivo o `PROGRESS.html`, actualiza también `PROJECT.md`, `FEATURES.md`, `CONTEXT.md` y `README.md` (`.cursor/rules/docs-progress-status-sync.mdc`).

---

## Cómo retomar el trabajo

```powershell
docker compose up -d
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
cd ERP.API && dotnet run                    # http://localhost:5003  swagger: /swagger
cd ../../../../frontend && npm run dev      # http://localhost:5173

# Tests (desde backend/src/)
dotnet test ERP.Domain.Tests/ERP.Domain.Tests.csproj
dotnet test ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj
dotnet test ERP.Application.Tests/ERP.Application.Tests.csproj   # ⚠ requiere reparación post-refactor
dotnet test ERP.API.Tests/ERP.API.Tests.csproj                     # ⚠ requiere reparación post-refactor
```

> **Credenciales dev:** `backend/src/ERP.API/appsettings.Development.json` (copiado de `.json.example`)  
> PostgreSQL: `Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024`  
> **SRI real:** activar con `"Sri": { "UseRealService": true }` en appsettings (requiere certificado P12 válido)

---

## Resumen de estado

| Área | Estado |
|------|--------|
| Backend módulos core | ✅ Completo |
| Plataforma SaaS / SuperAdmin | ✅ Completo |
| Integración SRI (código) | ✅ Implementado — 🟡 falta validar en ambiente SRI |
| Frontend catálogos e inventario | ✅ Completo |
| Frontend compras (OC, proveedores, facturas, gastos) | ✅ Completo |
| Frontend ventas (facturas, notas crédito/débito) | ✅ Completo |
| Frontend retenciones / guía remisión | ⏳ Placeholder (backend parcial) |
| Tests automatizados | 🚧 Suite en reparación tras refactor a inglés |
| MVP comercial | 🟡 ~85–90 % — bloqueado por validación SRI real |

---

## Avance general (~85–90 % hacia MVP)

El núcleo operativo del ERP está construido. Lo que falta para comercializar es principalmente **validar la facturación electrónica contra el SRI de pruebas** y completar algunos comprobantes secundarios (retenciones, guía de remisión, liquidación de compra).

Para checklist ítem por ítem con porcentajes por sección, abrir **`PROGRESS.html`** en el navegador (actualizado 2026-05-18 v9).

---

## Backend — módulos completados ✅

### Plataforma SaaS / SuperAdmin
- Gestión de tenants, planes, features (`TenantsController`, `SaasPlansAdminController`)
- Menú dinámico por tenant en BD (`ERP.Application/Modules/Navigation/`)
- Menu Builder: árbol, operaciones, historial, validación integridad plan-menú
- Onboarding automático al crear empresa (`ITenantOnboardingService`: perfiles, cliente consumidor final, sucursal y bodega principal)
- Seed de menú global: 9 grupos × 33 ítems; plan `starter` con `menu_config_json` vía `SaasPlansBootstrap` e install SQL `004_plan_menu_config_backfill.sql`
- Perfiles de acceso + permisos granulares (`ERP.Application/Modules/Access/`, `Permissions.cs`)
- Auditoría de usuario (`ERP.Domain/Modules/Audit/`)
- Config jerárquica global → módulo → feature

### Configuración SRI y facturación
- Config empresa + ambiente + certificado P12 (AES-256): `ConfiguracionSriController`
- Secuenciales, establecimiento, punto de emisión, URLs WSDL
- Config RIDE (logo, leyendas, ancho tirilla): `BillingSettingsController`
- Catálogos SRI precargados (`sri_vat_rate`, `sri_doc_type`, `sri_retention_code`, etc.)

### Catálogos base
- Productos (CRUD, variantes, códigos de barra): `ERP.Application/Modules/Products/`
- Categorías, Marcas, Unidades, Aranceles: `ERP.Application/Modules/Products/Catalogs/`
- Tarifas SRI (solo lectura desde `sri_vat_rate`)
- Clientes: `ERP.Application/Modules/Customers/`
- Proveedores (CRUD + validación RUC ecuatoriano): `ERP.Application/Modules/Proveedores/`
- Sucursales + Geografía INEC Ecuador: `ERP.Application/Modules/Branches/`
- Transportistas: `ERP.Application/Modules/Logistics/` + `CarriersController`

### Contabilidad
- Plan de cuentas + Asientos + Config contable por empresa
- Libro diario, mayor general, balance de comprobación
- Asientos automáticos al aprobar compras/gastos
- Caja / bancos (caja chica, extractos, conciliación)

### Logística e Inventario
- Bodegas (CRUD): `ERP.Application/Modules/Bodegas/`
- Inventario (StockActual + InventarioMovimiento): `ERP.Application/Modules/Inventario/`
- Transferencias entre Bodegas: flujo completo
- Ajustes de Inventario: flujo completo
- Kardex valorizado (promedio ponderado): `KardexController` — sync + async via Hangfire
- Parser XML SRI (comprobantes recibidos): `ERP.Infrastructure/Services/SriFacturaParser.cs`

### Compras
- Facturas de compra: Borrador → Validado → Aprobado/Rechazado (stock++ + asiento contable)
- Órdenes de Compra: flujo completo con vinculación a facturas electrónicas
- Gastos: Borrador → Validado → Aprobado/Rechazado
- Retención en la fuente emitida (backend)

### Ventas
- Facturas con facturación electrónica SRI (`InvoicesController`)
- Notas de crédito/débito asociadas a facturas autorizadas
- Retenciones recibidas (registro desde XML)
- RIDE PDF: `RideGeneratorService` (QuestPDF) — `GET /api/ventas/{id}/ride`
- Reintento de envío SRI: `PATCH /api/Ventas/{id}/reintentar` + job Hangfire `SriRetryJob`

### Integración SRI real ✅ (código) / 🟡 (validación pendiente)

Switch en `DependencyInjection.cs` via `Sri:UseRealService`:

| Componente | Archivo | Estado |
|------------|---------|--------|
| Generación XML v1.1.0 | `SriXmlFacturaBuilder` | ✅ |
| Firma XAdES-BES (P12) | `XadesBesSigner` | ✅ |
| SOAP recepción + polling | `SriSoapClient` | ✅ |
| Orquestador real | `SriFacturaElectronicaRealService` | ✅ |
| Modo simulado (dev) | `SriFacturaElectronicaSimuladoService` | ✅ |
| Retenciones emitidas | `SriWithholdingSimulatedService` | 🚧 Simulado |
| Validación XSD oficial | — | ⏳ Pendiente |
| Prueba E2E celcer.sri.gob.ec | — | ⏳ Pendiente |

---

## Frontend — pantallas completadas ✅

Rutas canónicas en **inglés** (`/sales/*`, `/inventory/*`, `/purchases/*`, `/finance/*`, `/settings/*`, `/admin/*`) con redirects legacy desde rutas en español.

| Módulo | Rutas | Pantallas |
|--------|-------|-----------|
| Auth | `/login`, `/select-tenant`, `/forgot-password`, `/reset-password` | Login, TenantSelect, PasswordReset |
| Dashboard | `/dashboard` | DashboardPage (KPIs, actividad, accesos rápidos) |
| Ventas | `/sales/invoices`, `/sales/invoices/new` | VentasFacturasPage, CreateInvoicePage |
| Ventas | `/sales/credit-notes`, `/sales/credit-notes/new` | CreditNotesPage, CreateCreditNotePage |
| Ventas | `/sales/customers` | CustomersPage |
| Compras | `/purchases/invoices`, `/purchases/invoices/new` | ComprasListPage, CrearCompraPage |
| Compras | `/purchases/orders`, `/purchases/orders/new`, `/purchases/orders/:id` | OrdenesCompraListPage, CrearOrdenCompraPage, OrdenCompraDetailPage |
| Compras | `/purchases/suppliers` | SuppliersPage |
| Gastos | `/expenses`, `/expenses/new` | GastosListPage, CrearGastoPage |
| Inventario | `/inventory/adjustments`, `/inventory/transfers`, `/inventory/warehouses` | Ajustes, Transferencias, BodegasPage |
| Inventario | `/inventory/products`, catálogos (`/inventory/brands`, etc.) | ProductsPage, Brands, ProductTypes, Units, Tariffs, CatalogStructure |
| Logística | `/logistics/carriers` | CarriersPage |
| Contabilidad | `/finance/accounts`, `/finance/config` | AccountingPage (plan, diario, mayor, balance) |
| Configuración | `/settings/company`, `/settings/sri`, `/settings/ride`, `/settings/branches` | CompanyConfigPage, SriConfigPage, BillingSettingsPage, BranchesPage |
| Admin | `/admin/users`, `/admin/roles`, `/admin/security` | TenantAccessPage, ProfilesPage, SecuritySettingsPage |
| Reportes | `/reportes/ventas` | SalesReportPage |
| SuperAdmin | `/companies`, `/superadmin/*` | CompaniesPage, PlansPage, MenuPlansHub, OverviewPage |

### Frontend — placeholders (ruta existe, UI pendiente)

| Ruta | Notas |
|------|-------|
| `/sales/withholding-received` | Retenciones recibidas — backend OK |
| `/purchases/withholding-issued` | Retenciones emitidas — backend OK |
| `/purchases/credit-notes` | Notas de crédito proveedor |
| `/inventory/kardex`, `/inventory/stock` | Kardex backend OK; UI pendiente |
| `/cash/bank` | Caja/bancos backend OK |
| `/settings/geography`, `/admin/activity` | Geografía INEC, auditoría actividad |

---

## Estabilización reciente (2026-05-18)

- **Fix crítico:** valores de estado `SalesBill` alineados dominio ↔ handlers ↔ frontend (`Borrador` / `Validado` / `Autorizado` / `Anulado`)
- **Fix migraciones:** stubs manuales eliminados; migración real `AddSalesBillFields` aplicada
- **Menú configuración:** ítems SRI/RIDE/empresa inyectados en bootstrap de navegación
- **Refactor rutas:** 33 rutas, permisos y controladores renombrados a convención inglés (`InvoicesController`, `/sales/*`, etc.)
- **CSS / i18n:** auditoría de clases duplicadas; +30 claves de navegación en `es` / `en` / `qu`

---

## Flujos de estado de documentos

### Orden de Compra
```
Borrador
├──[enviar]──► Enviada ──[aprobar]──► Aprobada
└──[aprobar]────────────────────────► Aprobada
                                          ├──[factura parcial]──► RecibidaParcial ──[completo]──► Cerrada
                                          └──[factura total]───────────────────────────────────► Cerrada
Cualquier estado activo ──[cancelar]──► Cancelada
```
Reglas clave:
- Número: `OC-{secuencial:D4}` único por tenant
- Solo OC `Aprobada` o `RecibidaParcial` puede recibir facturas
- Factura vinculada DEBE estar en estado `Aprobado`
- Diferencia de precio >1% → `Advertencias[]` (no bloquea)
- Stock: la OC NO mueve inventario; se actualiza al aprobar `CompraFactura`

### Ajuste de Inventario
```
Borrador ──[ejecutar]──► Ejecutado  (stock +/− CantidadAjuste)
         ──[cancelar]──► Cancelado  (sin efecto en stock)
```

### Transferencia entre Bodegas
```
Borrador ──[enviar]──► Enviada ──[recibir]──► Recibida
Cualquier estado activo ──[cancelar]──► Cancelada
```

### Factura de Venta (electrónica SRI)
```
Borrador ──[validar]──► Validado ──[emitir/autorizar]──► Autorizado
         ──[anular]────────────────────────────────────► Anulado
ErrorEnvio / Rechazado ──[reintentar]──► (re-proceso SRI)
```

### Compra / Gasto (factura recibida)
```
Borrador ──[validar]──► Validado ──[aprobar]──► Aprobado  (stock++ + asiento contable)
                                  └[rechazar]──► Rechazado
```

---

## Pendiente para MVP comercial

### Crítico
1. **Validar SRI real:** probar XML + firma XAdES + envío SOAP en `celcer.sri.gob.ec` con certificado P12 de pruebas; agregar validación XSD antes de firmar

### Alta prioridad
2. **Frontend retenciones:** comprobante de retención emitido y recibido (`/sales/withholding-received`, `/purchases/withholding-issued`)
3. **Menú por plan:** configurar JSON de menú por plan comercial en SuperAdmin → Planes → Menu Builder (plan `starter` incluye menú de referencia en bootstrap)
4. **Reparar suite de tests:** actualizar referencias post-refactor (`VentasController` → `InvoicesController`, DTOs renombrados)

### Media prioridad
5. **Guía de remisión:** entidad `DeliveryGuide` en backend; falta pantalla (transportistas ya implementados)
6. **Liquidación de compra (tipo 03):** sin entidad ni backend aún
7. **Kardex / stock UI:** backend completo; rutas en placeholder
8. **Stock mínimo y punto de reorden:** campos y alertas en dashboard

### Baja prioridad
9. Traducciones Kichwa/inglés al 100 % (algunos módulos aún con strings hardcoded)
10. Índices compuestos `(TenantId, fecha)` en tablas de alto volumen
11. Particionamiento de `electronic_doc` / `stock_movement` (diferir hasta escala)

---

## Extras diferidos (documentados, no bloqueantes)

- OC: recepción física sin factura (`RecibirOrdenCompraParcial`)
- OC: tolerancia de precio configurable (actualmente hardcodeada al 1%)
- OC: banner de advertencias en frontend al vincular factura con precio discrepante
- SuperAdmin: log de impersonación con motivo obligatorio
- SuperAdmin: JWT de duración distinta para sesión impersonada
- Reporte de autorizaciones SRI fallidas y reintentos

---

## Tests

| Proyecto | Tipo | Estado (2026-05-18) |
|----------|------|---------------------|
| `ERP.Domain.Tests` | Unitario (entidades, VOs) | ✅ 25 passing |
| `ERP.Infrastructure.Tests` | Integración ligera | 🚧 2 passing, 1 failing |
| `ERP.Application.Tests` | Unitario + MediatR | ⚠ No compila (DTOs renombrados) |
| `ERP.API.Tests` | Integración HTTP | ⚠ No compila (`VentasController` → `InvoicesController`) |

> Objetivo histórico: **266 tests**. La suite requiere actualización tras el refactor de mayo 2026 antes de confiar en CI.

Frontend E2E (Playwright):
```powershell
cd frontend
npx playwright install chromium   # solo primera vez
npm run build && npm run test:e2e # vite preview en puerto 4173
```
