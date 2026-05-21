# ADR-002: CQRS con MediatR y FluentValidation

## Estado
Aceptado

## Contexto
Commands/Queries con reglas de validación y pipeline transversal (logging, validación, tenant).

## Decisión
**MediatR** para dispatch; **FluentValidation** por Command/Query; `ValidationBehavior` → HTTP 422.

## Consecuencias
- ✅ Un handler por caso de uso
- ✅ Validación centralizada en Application
- ⚠️ Handlers grandes deben partirse (guardrail ≤150 líneas en `Handle`)

## Referencias
- [`BACKEND_RULES.md`](../../BACKEND_RULES.md)
