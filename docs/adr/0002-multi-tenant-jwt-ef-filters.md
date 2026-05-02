# ADR 0002: Multi-tenant con JWT y filtros globales en EF Core

**Estado:** Aceptada  
**Fecha:** 2026-05-02  

## Contexto

Cada empresa (tenant) debe ver solo sus datos. Hace falta un mecanismo **consistente** en todas las lecturas/escrituras ORM y una identidad de tenant clara en cada request HTTP.

## Decisión

- Incluir **`TenantId`** en entidades de negocio que correspondan; aislar con **query filters globales** registrados en `ErpDbContext` (evaluación por request, no capturada al compilar el modelo).
- Resolver el tenant activo desde el **JWT** (`tenant_id` u equivalente acordado) vía **`ICurrentTenant`** / servicio de aplicación, inyectado en el contexto de persistencia.
- Autenticación con **JWT** emitido por infraestructura (`JwtService`); controllers protegidos con `[Authorize]`.
- **Soft delete** con `IsActive = false` como convención de negocio salvo excepción explícita de producto.

## Consecuencias

- **Positivas:** menos riesgo de “olvidar” el filtro en una query manual si las entidades nuevas se registran correctamente en el modelo.
- **Negativas:** hay que acordar qué entidades son globales (p. ej. geografía) vs por tenant; errores de configuración del filtro son incidentes graves.
- **Riesgo:** tokens o claims mal emitidos pueden mezclar datos; los tests de integración y revisiones de auth son críticos.
