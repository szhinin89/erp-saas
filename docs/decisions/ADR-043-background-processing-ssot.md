# ADR-043 — Orquestación recurrente única

Estado: Accepted. Sustituye la infraestructura recurrente anterior y solo esa responsabilidad.

## Decisión y justificación

Un catálogo de identidad, opciones tipadas, runner único, scopes frescos, locks PostgreSQL,
lotes explícitos y checkpoints durables evitan registros, wrappers y cursores paralelos.
API y Worker son hosts separables de la misma infraestructura. La recuperación fiscal
pertenece a ElectronicDocuments desde que existe el documento; Retentions solo recupera
la transmisión inicial previa a su registro. Sales recovery permanece create-only.

## Límites y dependencias

No cambia reglas fiscales, estados, entrega SMTP ni contratos de negocio de ADR-036/039.
Sin Dashboard ni UI/AdminCore. No autoriza operar workers ni aplicar migraciones sustituidas.
Contrato implementado único: [Backend](../architecture/backend.md#background-processing).
Operación: [deployment](../deployment/README.md); métricas: [METRICS](../observability/METRICS.md).
Gates y límites de aceptación: [STATUS](../../STATUS.md#background-processing-y-despliegue).
