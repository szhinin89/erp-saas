# Flujo comercial: empresa, plan y catálogo SaaS

Este documento fija el modelo mental que sigue el ERP para **planes comerciales** y lo que ve el Super Admin en **Empresas → Plan y módulos**.

## Cadena principal

1. **Empresa (tenant)** contrata un **plan comercial** (`planCode` en suscripción). El plan es una fila del catálogo administrable (`saas_plans` / API `GET /api/superadmin/saas-plans`).

2. Ese **plan comercial** puede estar relacionado internamente con **definiciones SaaS** (`SaasFeatureDefinition`), clasificadas en `SaasFeatureKind`:
   - **Module** — bloque funcional grande (equivalente a “módulo” de producto).
   - **Form** — pantalla o formulario concreto (submódulo / pantalla).
   - **Quota**, **Integration** — otros tipos de feature (límites, integraciones).

   La relación plan ↔ definición se materializa en **plan features** (`SaasPlanFeature`: `featureId`, `isIncluded`, límites opcionales), pero esa matriz **no se administra ya en el flujo operativo de SuperAdmin**.

3. **Restricción por módulos de producto (tenant)**  
   Además del plan, el tenant puede llevar datos de **módulos de producto** habilitados (`TenantSubscriptionCatalog`: claves `catalog`, `accounting`, `saas`, `access`). Si no hay restricción, se consideran habilitados todos los módulos de producto compatibles con permisos.  
   Esto **no sustituye** al plan: primero aplica el plan (catálogo SaaS); la restricción por módulo acota qué **ramas de permisos** del producto pueden usarse para esa empresa. La pantalla **Plan y módulos** no edita esa restricción: solo muestra el estado efectivo en el catálogo (insignia verde).

## Resumen visual en UI

En **Empresas** del front (Super Admin):

- **Plan ↔ menú**: pestaña que muestra el árbol del menú (`GET /api/superadmin/navigation-menu`) para editar estructura y asignar contexto de navegación.

En **Plan y módulos** del front:

- **Plan comercial** (desplegable + franja resumen): qué plan está asignado / se va a guardar.
- **Catálogo módulos y formularios**: lista **de referencia** para visibilidad funcional.
- El control contractual efectivo para operación diaria está centrado en:
  - `planCode` del tenant,
  - `enabledModules` del tenant,
  - menú de sesión filtrado por permisos.

La jerarquía “módulo → submódulo / formulario” en producto se refleja principalmente en navegación + permisos + módulos del tenant.

## Evolución futura

Si se requiere persistir activación **por formulario** a nivel tenant (más allá del plan), haría falta extender el contrato de API y el dominio (p. ej. overrides por `featureId`), hoy parcialmente previsto en entidades como `TenantSubscriptionFeatureOverride` según evolución del módulo de suscripciones.
