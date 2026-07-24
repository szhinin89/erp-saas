# ADR-013: CQRS con MediatR y FluentValidation

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
- [`AI-RULES/BACKEND-RULES.md`](../../AI-RULES/BACKEND-RULES.md)
