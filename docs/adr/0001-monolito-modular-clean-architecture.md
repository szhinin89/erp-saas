# ADR 0001: Monolito modular + Clean Architecture

**Estado:** Aceptada  
**Fecha:** 2026-05-02  

## Contexto

El ERP se entrega como producto SaaS multi-tenant. El equipo necesita límites claros de dependencias, tests por capa y la posibilidad de **extraer módulos a microservicios** más adelante sin reescribir el dominio.

## Decisión

- Mantener un **monolito modular** en un solo deploy (API + base de datos compartida por tenant vía esquema de datos).
- Organizar el backend en **Clean Architecture** estricta: `ERP.API` → `ERP.Application` → `ERP.Infrastructure` y `ERP.Domain`, sin referencias ascendentes ni lógica de negocio en controllers.
- Ubicar cada módulo de negocio en **carpetas verticales** por capa (`ERP.Domain/Modules/{Modulo}`, etc.), usando **Accounting** como referencia de forma.
- **Sin AutoMapper**: mapeos explícitos en handlers / casos de uso.

## Consecuencias

- **Positivas:** onboarding predecible, pruebas unitarias/integración acotadas, extracción futura de un módulo como servicio con menos sorpresas.
- **Negativas:** más archivos y ceremonia que un CRUD “todo en el controller”; hay que respetar las reglas en `.cursor/rules/erp-unified-rules.mdc`.
- **Riesgo:** el dominio puede crecer; conviene ADRs adicionales cuando un módulo gane fronteras propias (eventos, límites de transacción, etc.).
