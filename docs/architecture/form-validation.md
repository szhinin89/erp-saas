# Estándar de Validación de Formularios (INMUTABLE)

Todo formulario del ERP implementa validación en dos niveles. El incumplimiento es desviación de arquitectura y bloquea la aprobación del módulo.

---

## 1. Validación local (Frontend)

- **React Hook Form** para el estado del formulario.
- **Zod** como fuente de reglas de validación de interfaz.
- Mensajes en español, orientados a corregir: `"El RUC debe tener 13 dígitos."` — no `"Error de validación."`.
- Objetivo: retroalimentación inmediata antes de hacer la petición HTTP.

## 2. Validación del servidor (Backend)

- **FluentValidation** es la fuente de verdad de las reglas de negocio.
- `ExceptionMiddleware` transforma `ValidationException` → HTTP 422 con estructura de campo.
- Nombres de propiedad en **camelCase** en la respuesta.

Contrato de respuesta 422:

```json
{
  "data": {
    "errors": {
      "taxIdentificationNumber": ["El RUC debe tener 13 dígitos."]
    }
  }
}
```

## 3. Mecanismo estándar para errores HTTP 422 (Frontend)

```ts
applyServerErrors<T>(error, setError)
```

Importar desde `modules/lib/validationErrors.ts`. Está **prohibido**:

- Parsear manualmente strings de error dentro de páginas o componentes.
- Crear condicionales del tipo `if (field === "email") setError(...)`.
- Depender del formato concatenado anterior `"Campo: Mensaje"`.

## 4. Responsabilidades por capa

| Capa | Responsabilidad |
|------|----------------|
| Frontend — Zod | Reglas de formato e interfaz; mensajes inmediatos |
| Frontend — RHF | Estado del formulario; muestra errores bajo el campo |
| Frontend — `applyServerErrors` | Mapea errores 422 estructurados a campos RHF |
| Backend — FluentValidation | Reglas de negocio; fuente de verdad |
| Backend — `ExceptionMiddleware` | Serializa `ValidationException` → 422 con mapa campo→mensajes |

## 5. Architecture Gate — Criterios de cierre de módulo

Un módulo **no puede considerarse cerrado** si incumple cualquiera de los siguientes puntos. La presencia de un incumplimiento es un **FAIL de arquitectura** y debe corregirse antes de aprobar el módulo.

### Frontend

| # | Criterio | Estado |
|---|----------|--------|
| F-V1 | El formulario usa React Hook Form como motor | ✅ obligatorio |
| F-V2 | Existe un schema Zod para todas las validaciones de interfaz | ✅ obligatorio |
| F-V3 | Los errores se muestran debajo del campo correspondiente | ✅ obligatorio |
| F-V4 | Los valores ingresados se conservan cuando hay errores | ✅ obligatorio |
| F-V5 | Los errores HTTP 422 se mapean exclusivamente con `applyServerErrors<T>()` de `modules/lib/validationErrors.ts` | ✅ obligatorio |
| F-V6 | No existe `setError()` manual para interpretar errores del API | ❌ prohibido |
| F-V7 | No existe parseo de strings concatenados `"Campo: Mensaje"` | ❌ prohibido |
| F-V8 | No existen mensajes genéricos como `"Error de validación"`, `"Campo inválido"` o `"Dato incorrecto"` | ❌ prohibido |

### Backend

| # | Criterio | Estado |
|---|----------|--------|
| B-V1 | Toda regla de negocio existe en FluentValidation | ✅ obligatorio |
| B-V2 | `ValidationException` → HTTP 422 via `ExceptionMiddleware` | ✅ obligatorio |
| B-V3 | La respuesta mantiene el mapa `campo → lista de mensajes` (camelCase) | ✅ obligatorio |
| B-V4 | No se devuelven errores de validación como texto plano | ❌ prohibido |
| B-V5 | No se exponen excepciones técnicas al usuario | ❌ prohibido |

Ver también: [error-handling.md](./error-handling.md) (contrato de errores backend↔frontend, reglas E-B/E-F).
