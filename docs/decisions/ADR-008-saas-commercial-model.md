# ADR-008: Modelo SaaS comercial (planes y features)

## Estado
Aceptado

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
- [`docs/SAAS-COMMERCIAL.md`](../SAAS-COMMERCIAL.md)
