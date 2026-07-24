# ADR-016: Frontend modular por dominio

## Estado
Aceptado

## Contexto
SPA grande con catálogos, i18n (es/en/qu), ZH Form System.

## Decisión
Módulos en `frontend/src/modules/{dominio}/` con `api/`, `schemas/`, `hooks/`, `pages/`. Rutas lazy. Sin lógica HTTP en páginas legacy.

## Consecuencias
- ✅ Ownership por feature team
- ✅ Guardrails anti-patrones (`fetch` directo, schemas duplicados)
- ⚠️ Páginas en `pages/` solo wrappers delgados durante migración

## Referencias
- [`AI-RULES/FRONTEND-RULES.md`](../../AI-RULES/FRONTEND-RULES.md)
