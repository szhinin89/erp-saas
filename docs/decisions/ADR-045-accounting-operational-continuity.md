# ADR-045 — Consolidación de la arquitectura de Accounting

Estado: Accepted. Registro arquitectónico; no es especificación funcional.

## Contexto

Las entregas documentales describían criterios incompatibles y obligaban a reconstruir
el comportamiento desde varios documentos. El sistema está en preproducción.

## Decisión arquitectónica

Conservar el core y motor genérico existentes y reunir el contrato funcional completo
en [docs/architecture/accounting.md](../architecture/accounting.md).
Los registros anteriores salen del circuito de especificación contable; las futuras
decisiones funcionales se incorporan directamente a ese documento.

## Consecuencias

Un único documento permite conocer reglas aprobadas, capacidades implementadas y
extensiones pendientes. No se crea otra arquitectura ni se mantienen políticas
contables anteriores por compatibilidad. Esta decisión documental no implementa código.
