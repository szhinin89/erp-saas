# SUBSCRIBER SCOPE — ARCHITECTURE FREEZE

> **Estado: SEALED.**
> Este documento es normativo. Cualquier cambio estructural al SUBSCRIBER scope
> requiere una decisión de arquitectura explícita, documentada en un ADR nuevo,
> y revisión en PR.

---

## MODELO CANÓNICO FINAL

El SUBSCRIBER scope es el **Control Plane del sistema SaaS**. No contiene lógica
operativa del ERP. Su única responsabilidad es la identidad y billing del tenant.

### Tabla canónica SUBSCRIBER

| Tabla | Propósito único | Inmutable |
|---|---|---|
| `subscribers` | Identidad del tenant SaaS | ✅ |
| `subscriber_billing_profile` | Perfil fiscal + recibos + datos SaaS | ✅ |
| `subscriber_subscriptions` | Suscripción activa al plan comercial | ✅ |
| `subscriber_billing_accounts` | Estado de billing (active/suspended/cancelled) | ✅ |
| `saas_billing_invoices` | Facturas de suscripción SaaS | ✅ |
| `saas_billing_invoice_lines` | Líneas de factura SaaS | ✅ |
| `billing_payment_attempts` | Historial de intentos de cobro | ✅ |
| `saas_billing_events` | Audit log de cambios de estado billing | ✅ |
| `payment_provider_customers` | Mapeo subscriber → Stripe customer ID | ✅ |
| `payment_provider_subscriptions` | Espejo de Stripe subscription | ✅ |
| `billing_checkout_sessions` | URL temporal de pago Stripe Checkout | ✅ |
| `processed_webhook_events` | Idempotencia de webhooks Stripe | ✅ |
| `subscription_feature_overrides` | Override de límites por tenant | ✅ |
| `subscription_usages` | Consumo de features metered | ✅ |
| `identity_users` | Usuarios del sistema (global, multi-tenant) | ✅ |
| `refresh_tokens` | Token rotation (RTR) por sesión | ✅ |
| `password_reset_tokens` | OTP de reset de contraseña | ✅ |
| `security_admin_scope_assignments` | Roles administrativos | ✅ |
| `config_global` | Configuración global key-value del tenant | ✅ |
| `config_module` | Configuración por módulo ERP | ✅ |
| `config_feature` | Override de features (prioridad máxima) | ✅ |
| `subscriber_custom_menus` | Menú lateral personalizado | ✅ |

### Tablas ELIMINADAS (prohibido recrear)

| Tabla eliminada | Reemplazada por | Estado |
|---|---|---|
| `billing_settings` | `subscriber_billing_profile` | DROPPED — NO RECREAR |
| `subscriber_billing_profiles` (plural) | `subscriber_billing_profile` | DROPPED — NO RECREAR |
| `tax_rates` | `global.sri_vat_rate` + `global.sri_ice_rate` | DROPPED — NO RECREAR |
| `retention_settings` | `global.sri_retention_code` | DROPPED — NO RECREAR |
| `units_of_measure` | `global.sri_uom` | DROPPED — NO RECREAR |

---

## DOMAIN BOUNDARIES

### SUBSCRIBER PUEDE contener

| Categoría | Ejemplos permitidos |
|---|---|
| Identidad del tenant | subscribers, identity_users |
| Billing SaaS | subscriber_billing_profile, saas_billing_invoices, saas_billing_accounts |
| Suscripción al plan | subscriber_subscriptions, subscription_feature_overrides, subscription_usages |
| Pagos / integración externa | payment_provider_customers, billing_checkout_sessions, processed_webhook_events |
| Configuración de plataforma | config_global, config_module, config_feature |
| Seguridad y autenticación | refresh_tokens, password_reset_tokens, security_admin_scope_assignments |

### SUBSCRIBER NO PUEDE contener

| Categoría prohibida | Motivo |
|---|---|
| Lógica contable / cuentas | COMPANY scope — entidades ERP operativas |
| Lógica fiscal SRI | GLOBAL scope — catálogos regulatorios inmutables |
| Inventario / stock | COMPANY scope — entidades ERP operativas |
| Ventas / compras | COMPANY scope — entidades ERP operativas |
| Productos / catálogos ERP | COMPANY scope — entidades ERP operativas |
| Proveedores / clientes ERP | COMPANY scope — BusinessPartner es subscriber-shared |
| Tasas de impuestos personalizadas | GLOBAL — `global.sri_vat_rate` es la fuente única |
| Tablas de configuración duplicadas | Ya existen `config_global`, `config_module`, `config_feature` |

---

## SINGLE SOURCE OF TRUTH

| Concepto | Fuente única de escritura | Prohibiciones |
|---|---|---|
| Perfil fiscal del tenant | `subscriber_billing_profile` | NO `billing_settings`, NO variantes |
| Identidad del usuario | `identity_users` | NO segunda tabla de usuarios |
| Acceso SaaS | `subscriber_subscriptions` | NO rutas alternativas de suscripción |
| Tasas IVA | `global.sri_vat_rate` | NO `tax_rates` ni equivalentes |
| Unidades de medida | `global.sri_uom` | NO `units_of_measure` ni equivalentes |
| Códigos de retención | `global.sri_retention_code` | NO `retention_settings` ni equivalentes |

---

## PROHIBICIONES DE DUPLICACIÓN

### Entidades prohibidas (drift semántico)

Ningún agente, desarrollador o migración puede crear entidades que sean
**semánticamente equivalentes** a las existentes, aunque tengan nombre diferente:

| Intento de drift | Entidad existente que cubre el concepto |
|---|---|
| `TenantBillingInfo`, `SubscriberFiscalProfile` | `SubscriberBillingProfile` |
| `TenantIdentity`, `SubscriberUser` | `IdentityUser` |
| `SaasSubscription`, `TenantPlan` | `SubscriberSubscription` |
| `CustomTaxRate`, `SubscriberVatRate` | `global.sri_vat_rate` |
| `TenantUOM`, `CustomUnit` | `global.sri_uom` |

### DTOs y Commands prohibidos

| Prohibido | Canónico |
|---|---|
| `BillingSettingsDto` | `SubscriberBillingProfileDto` |
| `UpsertBillingSettingsCommand` | `UpsertSubscriberBillingProfileCommand` |
| Cualquier otro Command de billing alternativo | SOLO `UpsertSubscriberBillingProfileCommand` |

### Repositorios prohibidos

Solo existe y puede existir **UN** repositorio de billing:
`ISubscriberBillingProfileRepository` → implementado por `SubscriberBillingProfileRepository`.

---

## REGLAS DE SELLADO

1. **Architecture Freeze:** Ninguna tabla nueva puede añadirse al SUBSCRIBER scope
   sin un ADR documentado que justifique que no duplica conceptos existentes.

2. **Naming Guard:** Toda entidad nueva en SUBSCRIBER debe ser revisada contra
   la tabla "Prohibiciones de duplicación" antes del merge.

3. **Single Command Rule:** Solo un Command de escritura por concepto.
   Si existe `UpsertSubscriberBillingProfileCommand`, no puede existir
   `UpdateBillingProfile`, `SetTenantBilling`, etc.

4. **No Legacy Fallback:** Ningún código puede tener condicionales `if (legacy)`
   o fallback a estructuras antiguas. El sistema ya no tiene estructuras legacy.

5. **GLOBAL Boundary:** Toda referencia a datos regulatorios (IVA, UOM, retenciones)
   debe apuntar a `global.*` — nunca a tablas per-subscriber.

---

## VERIFICACIÓN AUTOMATIZADA

Para confirmar el estado sellado, ejecutar:

```sql
-- Confirma que tablas legacy NO existen
SELECT COUNT(*) FROM information_schema.tables
WHERE table_name IN ('billing_settings','subscriber_billing_profiles',
                     'tax_rates','units_of_measure','retention_settings');
-- Debe retornar: 0

-- Confirma tabla canónica existe
SELECT COUNT(*) FROM information_schema.tables
WHERE table_name = 'subscriber_billing_profile' AND table_schema = 'public';
-- Debe retornar: 1
```

```powershell
# Confirma cero referencias legacy en codebase
Get-ChildItem backend/src -Recurse -Include "*.cs" |
    Where-Object { $_.FullName -notmatch "Migrations" } |
    Select-String "BillingSettings|subscriber_billing_profiles|tax_rates" |
    Where-Object { $_.Line -notmatch "//" }
# Debe retornar: sin resultados
```

---

Referencia canónica: [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md#subscriber-scope--modelo-canónico-sellado)
