# ADR-008: Modelo SaaS comercial (planes y features)

## Estado
Superseded — eliminado del ERP Core en FASE 1 (ver [`docs/STATUS.md`](../STATUS.md) y [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)). `SaasFeatureDefinition`/`saas_plan_features`/`SuperAdmin` no existen en el código actual. Posible reintroducción en una futura Platform externa — ver [`docs/future-platform/`](../future-platform/).

## Contexto
Producto vendido por planes con módulos/formularios habilitables por tenant.

## Decisión
- `SaasFeatureDefinition` + `saas_plan_features` en BD
- SuperAdmin configura planes; menú filtrado por entitlements
- Nuevo módulo/pantalla debe asignarse a planes explícitamente

## Consecuencias
- ✅ Catálogo comercial en datos, no hardcode
- ⚠️ Checklist obligatorio al crear módulos (ver reglas agente)

## Referencias
- [`docs/archive/SAAS-COMMERCIAL.md`](./SAAS-COMMERCIAL.md)
