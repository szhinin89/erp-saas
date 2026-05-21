# ADR-007: Monolito modular (no microservicios)

## Estado
Aceptado

## Contexto
MVP SaaS con equipo pequeño; necesidad de consistencia transaccional (SRI, inventario, contabilidad).

## Decisión
**Modular monolith**: módulos verticales por carpeta (`ERP.Domain/Modules/*`) sin dependencias cruzadas entre Application modules. Comunicación vía contratos/MediatR.

## Consecuencias
- ✅ Deploy único, transacciones ACID
- ✅ Evolución futura a extracted services por módulo si hace falta
- ⚠️ Disciplina estricta en boundaries (Architecture.Tests)

## Referencias
- [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
