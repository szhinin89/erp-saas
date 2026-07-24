# ADR-018: Infraestructura centralizada de mensajes visuales

- **Estado**: Aceptado
- **Fecha**: 2026-06-29
- **Autor**: Sebastian Zhinin

## Contexto

El ERP SaaS tenía múltiples implementaciones de mensajes visuales distribuidas por los módulos: 3 componentes de toast distintos (MasterDataPartnerToast, WarehouseToastManager, inline zh-toast), 3 stores Zustand duplicados, 2 modales de confirmación (ZHConfirmModal, WarehouseConfirmModal), 3 sistemas de alerta inline (ZHPageNotice, pf-error, pf-alert sin CSS), y colores hardcodeados en CSS.

Cada módulo implementaba su propia variante, con diferencias en auto-dismiss (3s vs 4s), iconos, CSS classes, y patrones de invocación. No existía política de duplicados ni cola.

## Problema

- Inconsistencia visual entre módulos.
- Código duplicado con lógica idéntica copiada/pegada.
- Sin cola ni deduplicación — cada módulo mostraba un solo toast.
- Acoplamiento directo: cada página importaba Zustand stores.
- Sin posibilidad de cambiar la implementación sin tocar todos los consumidores.

## Solución adoptada

Arquitectura en 3 capas con fachada pública:

1. **API pública** (`lib/messages/`) — fachada `message.*` que es el único punto de entrada para módulos.
2. **Store interno** (`lib/messages/_internal/messageStore.ts`) — Zustand como detalle de implementación encapsulado.
3. **Componentes globales** (`components/zh/ZHToast.tsx`, `ZHGlobalDialogs.tsx`) — montados una vez en AppLayout, leen el store interno.

Características:
- Cola FIFO con máximo configurable (3).
- Deduplicación por política (`reset-timer`).
- Confirm/Prompt basados en Promise.
- Configuración centralizada en `messageDefaults.ts`.
- Catálogo de mensajes comunes (`MSG`).
- Regla ESLint que bloquea imports del store interno desde módulos.

## Alternativas descartadas

| Alternativa | Razón de descarte |
|---|---|
| **React Context** | Requiere provider wrapping, no invocable fuera del árbol de componentes |
| **Librería externa (react-toastify, sonner)** | Añade dependencia innecesaria; el sistema es simple y controlado |
| **Event emitter pattern** | Pierde tipado fuerte y debuggability de Zustand |
| **Exportar el store como API pública** | Acopla consumidores a Zustand; impide cambiar la implementación |

## Consecuencias

### Positivas
- Un solo punto de entrada para todo el ERP.
- Cambiar Zustand por otra solución no requiere tocar ningún módulo.
- Cola, deduplicación y configuración centralizadas.
- Tests unitarios validan el comportamiento crítico.
- ESLint previene regresiones automáticamente.

### Negativas / trade-offs
- `message.confirm()` es Promise-based — requiere `await` en el flujo del llamador.
- Los módulos que ya usaban `ZHConfirmModal` con estado local propio (Sales, Purchases) conservan ese patrón por compatibilidad funcional.

## Restricciones permanentes

- Los módulos **nunca** importan `_internal/messageStore`.
- Solo 5 tipos de mensaje: `success`, `error`, `warning`, `info`, `confirm`.
- Toda configuración visual vive en `messageDefaults.ts` y `design-tokens.css`.
- La API pública `message.*` está congelada como contrato estable.
